namespace Example.Cache.Memory

open Microsoft.Extensions.Logging

open Example.Cache
open Example.Cache.Core

[<AutoOpen>]
module private MemoryCacheImpl =
    
    type LRUEntry<'K> = {
        Key : 'K
        CreatedAt : System.DateTime 
    }
    with 
        static member Make( k, ca ) = 
            { Key = k; CreatedAt = ca } 
               
    type CacheEntry<'K,'V> = {
        Item : RV<'V>
        LRUNode : System.Collections.Generic.LinkedListNode<LRUEntry<'K>>
    }
    with 
        static member Make( item, n ) = 
            { Item = item; LRUNode = n }

type Cache<'V> ( logger: ILogger, name:string, spec:Specification ) =
    
    let makeLRUNode (k:string) = 
        System.Collections.Generic.LinkedListNode( LRUEntry<'V>.Make(k,System.DateTime.UtcNow) )
    
    let slimLock = 
        new System.Threading.ReaderWriterLockSlim() 
    
    let withReadLock fn = 
        slimLock.EnterReadLock()
        try
            fn()
        finally 
            slimLock.ExitReadLock() 
         
    let withWriteLock fn = 
        slimLock.EnterWriteLock()
        try
            fn()
        finally     
            slimLock.ExitWriteLock()
            
    let statistics =
        Statistics.Make()
    
    let entries =
        if spec.InitialCapacity.IsSome then 
            new System.Collections.Generic.Dictionary<string,CacheEntry<string,'V>>( spec.InitialCapacity.Value )
        else
            if spec.MaxSize.IsSome then 
                new System.Collections.Generic.Dictionary<string,CacheEntry<string,'V>>( spec.MaxSize.Value )
            else
                new System.Collections.Generic.Dictionary<string,CacheEntry<string,'V>>()
                
    let lru = 
        new System.Collections.Generic.LinkedList<LRUEntry<string>>() 
        
    let onSet =
        new Event<string>()
    
    let onGet =
        new Event<string>()
    
    let onRemove =
        new Event<string>()

    let onEvicted =
        new Event<string>()
        
    let remove (k:string) = 
        if entries.ContainsKey k then
            let ci = entries.Item(k)
            lru.Remove(ci.LRUNode)
            let result = entries.Remove(k)
            onRemove.Trigger( k )
            1
        else 
            0 
    
    let tryGet (k:string) =
        
        statistics.Get()
        
        let exists, item =
            entries.TryGetValue k

        if not exists then
            None
        else
            statistics.Hit()
            
            lru.Remove( item.LRUNode )
            
            entries.Remove( k ) |> ignore
            
            let newItem =                        
                CacheEntry<_,_>.Make( item.Item, makeLRUNode k ) 
    
            entries.Add( k, newItem )
            lru.AddFirst( newItem.LRUNode )
                    
            onGet.Trigger(k)
            
            newItem.Item.Value
                
    member val Name = name
    
    member val Statistics = statistics
    
    static member Make<'V>( logger, name, spec:ICacheSpecification ) =
        match spec with
        | :? Specification as spec ->
            new Cache<'V>( logger, name, spec ) :> IEnumerableCache<'V>
        | _ ->
            failwithf "Invalid options type passed to Memory constructor!"
        
    member this.Dispose () =
        lock this <| fun _ ->
            entries.Clear()
            
    member this.Purge () =
        withWriteLock <| fun _ ->
            let count = entries.Count
            entries.Clear()
            lru.Clear()
            count
            
    member this.Clean () =
        async {
            return ()
        }
        
    member this.Exists (k:string) =
        logger.LogTrace( "MemoryCache::Exists({CacheName}) - Called for {Key}", this.Name, k )
        withReadLock <| fun _ ->
            entries.ContainsKey( k )
            
    member this.Keys () =
        logger.LogTrace( "MemoryCache::Keys({CacheName}) - Called", this.Name )
        withReadLock <| fun _ ->
            entries.Keys |> Array.ofSeq
            
    member this.Flush () =

        logger.LogTrace( "MemoryCache::Flush({CacheName}) - Called", this.Name )
        
        let removeLast () =  
            if lru.Last <> null then
                let keyToRemove = lru.Last.Value.Key
                let result = remove keyToRemove 
                onEvicted.Trigger( keyToRemove )
                result
            else    
                0

        let oversizedRemoved = 
            if spec.MaxSize.IsSome then 
                let n = ref 0
                withWriteLock <| fun () ->
                    while entries.Count > spec.MaxSize.Value do
                        if removeLast().Equals(1) then System.Threading.Interlocked.Increment( n ) |> ignore else ()
                !n     
            else 
                0
                
        let expiredRemoved = 
        
            let now =   
                System.DateTime.UtcNow  
                
            let lastExpired ttlSeconds = 
                (now - lru.Last.Value.CreatedAt).TotalSeconds > (float) ttlSeconds 
                
            if spec.TimeToLiveSeconds.IsSome then 
                let n = ref 0 
                withWriteLock <| fun () ->
                    while lru.Count > 0 && lastExpired spec.TimeToLiveSeconds.Value do
                        if removeLast().Equals(1) then System.Threading.Interlocked.Increment( n ) |> ignore else ()
                !n     
            else 
                0
                    
        logger.LogTrace( "MemoryCache::Flush({CacheName}) - Removed {Removed} items", this.Name, (oversizedRemoved + expiredRemoved) )
        
        oversizedRemoved + expiredRemoved           
        
    member this.Set (items:seq<CacheItem<'V>>) =
        logger.LogTrace( "MemoryCache::Set({CacheName}) - Called" )
        
        withWriteLock <| fun _ ->
            
            items |> Seq.iter ( fun item ->
                
                statistics.Set()
                
                let itemKey, itemValue = item
                
                let exists, entry = 
                    entries.TryGetValue itemKey                                    
    
                let newItem =                        
                    if exists then
                        // if exists, remove from the node from LRU list - O(1)
                        lru.Remove( entry.LRUNode )
                        // remove key from cache - O(1)
                        entries.Remove( itemKey) |> ignore
                        CacheEntry<_,_>.Make( entry.Item, makeLRUNode itemKey ) 
                    else 
                        CacheEntry<_,_>.Make( RV<_>.Constant( itemValue ), makeLRUNode itemKey )                     
                            
                // put the item into the cache                        
                entries.Add( itemKey, newItem )
                // add the node to front of LRU list
                lru.AddFirst( newItem.LRUNode )
                
                onSet.Trigger( itemKey ) )
                
        this.Flush() |> ignore
            
    member this.TryGet (keys:string[]) =
        logger.LogTrace( "MemoryCache::TryGet({CacheName}) - Called with {nKeys} keys", this.Name, keys.Length )
        withWriteLock <| fun _ ->
            keys |> Array.map ( fun k -> tryGet k |> Option.bind ( fun v -> Some (k,v) ) ) 
                 

    member this.TryGetAsync (keys:string[]) =
        async {
            logger.LogTrace( "MemoryCache::TryGetAsync({CacheName}) - Called with {nKeys} keys", this.Name, keys.Length )
            let result =
                withWriteLock <| fun _ ->
                    keys |> Array.map ( fun k -> tryGet k |> Option.bind ( fun v -> Some (k,v) ) )
            return result
            }
            
    member this.Remove (ks:string[]) =
        logger.LogTrace( "MemoryCache::Remove({CacheName}) - Called with {nKeys} keys", this.Name, ks.Length )
        statistics.Remove()
        withWriteLock <| fun _ ->
            ks |> Seq.fold ( fun acc k -> acc + remove k ) 0 
                
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()
          
    interface IEnumerableCache<'V>
        with
            member this.Count
                with get () = entries.Count 
                
            member this.Statistics =
                this.Statistics :> IStatistics
                
            member this.Name =
                this.Name

            member this.Exists k =
                this.Exists k
                    
            member this.Purge () =
                this.Purge()
                
            member this.Clean () =
                this.Clean()
                
            member this.Keys () =
                this.Keys()
                
            member this.Set kvs =
                this.Set kvs
                
            member this.TryGet keys =
                this.TryGet keys

            member this.TryGetAsync keys =
                this.TryGetAsync keys
                            
            member this.Remove ks =
                this.Remove ks
                
            [<CLIEvent>]                    
            member this.OnSet =
                onSet.Publish
                
            [<CLIEvent>]                    
            member this.OnGet =
                onGet.Publish
                
            [<CLIEvent>]                    
            member this.OnRemove =
                onRemove.Publish

            [<CLIEvent>]                    
            member this.OnEvicted =
                onEvicted.Publish

