namespace Example.Cache.Memory

open Microsoft.Extensions.Logging

open Example.Cache
open Example.Cache.Core

[<AutoOpen>]
module private MemoryCacheImpl =
    
    type LRUItem<'K> = {
        Key : 'K
        CreatedAt : System.DateTime 
    }
    with 
        static member Make( k, ca ) = 
            { Key = k; CreatedAt = ca } 
               
    type CacheItem<'K,'V> = {
        Item : RV<'V>
        LRUNode : System.Collections.Generic.LinkedListNode<LRUItem<'K>>
    }
    with 
        static member Make( item, n ) = 
            { Item = item; LRUNode = n }

type Cache<'V> ( logger: ILogger, name:string, spec:Specification ) =
    
    let makeLRUNode (k:string) = 
        System.Collections.Generic.LinkedListNode( LRUItem<'K>.Make(k,System.DateTime.UtcNow) )
    
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
    
    let items =
        if spec.InitialCapacity.IsSome then 
            new System.Collections.Generic.Dictionary<string,CacheItem<string,'V>>( spec.InitialCapacity.Value )
        else
            if spec.MaxSize.IsSome then 
                new System.Collections.Generic.Dictionary<string,CacheItem<string,'V>>( spec.MaxSize.Value )
            else
                new System.Collections.Generic.Dictionary<string,CacheItem<string,'V>>()
                
    let lru = 
        new System.Collections.Generic.LinkedList<LRUItem<string>>() 
        
    let onSet =
        new Event<string>()
    
    let onGet =
        new Event<string>()
    
    let onRemove =
        new Event<string>()

    let onEvicted =
        new Event<string>()
        
    let remove (k:string) = 
        if items.ContainsKey k then
            let ci = items.Item(k)
            lru.Remove(ci.LRUNode)
            let result = items.Remove(k)
            onRemove.Trigger( k )
            result
        else 
            false 
    
    let tryGet (k:string) =
        
        statistics.Get()
        
        let exists, item =
            items.TryGetValue k

        if not exists then
            None
        else
            statistics.Hit()
            
            lru.Remove( item.LRUNode )
            
            items.Remove( k ) |> ignore
            
            let newItem =                        
                CacheItem<_,_>.Make( item.Item, makeLRUNode k ) 
    
            items.Add( k, newItem )
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
            items.Clear()
            
    member this.Purge () =
        withWriteLock <| fun _ ->
            let count = items.Count
            items.Clear()
            lru.Clear()
            count
            
    member this.Clean () =
        async {
            return ()
        }
        
    member this.Exists (k:string) =
        logger.LogTrace( "MemoryCache::Exists({CacheName}) - Called for {Key}", this.Name, k )
        withReadLock <| fun _ ->
            items.ContainsKey( k )
            
    member this.Keys () =
        logger.LogTrace( "MemoryCache::Keys({CacheName}) - Called", this.Name )
        withReadLock <| fun _ ->
            items.Keys |> Array.ofSeq
            
    member this.Flush () =

        logger.LogTrace( "MemoryCache::Flush({CacheName}) - Called", this.Name )
        
        let removeLast () =  
            if lru.Last <> null then
                let keyToRemove = lru.Last.Value.Key
                let result = remove keyToRemove 
                onEvicted.Trigger( keyToRemove )
                result
            else    
                false

        let oversizedRemoved = 
            if spec.MaxSize.IsSome then 
                let n = ref 0
                withWriteLock <| fun () ->
                    while items.Count > spec.MaxSize.Value do
                        if removeLast() then System.Threading.Interlocked.Increment( n ) |> ignore else ()
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
                        if removeLast() then System.Threading.Interlocked.Increment( n ) |> ignore else ()
                !n     
            else 
                0
                    
        logger.LogTrace( "MemoryCache::Flush({CacheName}) - Removed {Removed} items", this.Name, (oversizedRemoved + expiredRemoved) )
        
        oversizedRemoved + expiredRemoved           
        
    member this.Set (k:string) (v:'V) =
        logger.LogTrace( "MemoryCache::Set({CacheName}) - Called with key {Key}", this.Name, k )
        statistics.Set()
        withWriteLock <| fun _ ->        
            let exists, item = 
                items.TryGetValue k                                    

            let newItem =                        
                if exists then
                    // if exists, remove from the node from LRU list - O(1)
                    lru.Remove( item.LRUNode )
                    // remove key from cache - O(1)
                    items.Remove( k ) |> ignore
                    CacheItem<_,_>.Make( item.Item, makeLRUNode k ) 
                else 
                    CacheItem<_,_>.Make( RV<_>.Constant( v ), makeLRUNode k )                     
                        
            // put the item into the cache                        
            items.Add( k, newItem )
            // add the node to front of LRU list
            lru.AddFirst( newItem.LRUNode )
            
        let nFlushed = this.Flush()
        onSet.Trigger( k)

    member this.TryGetKeys (keys:string[]) =
        logger.LogTrace( "MemoryCache::Get({CacheName}) - Called with {nKeys} keys", this.Name, keys.Length )
        withWriteLock <| fun _ ->
            keys |> Array.map tryGet 
        
    member this.Remove (k:string) =
        logger.LogTrace( "MemoryCache::Remove({CacheName}) - Called with key {Key}", this.Name, k )
        statistics.Remove()
        withWriteLock <| fun _ ->
            remove k 
                
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()
          
    interface IEnumerableCache<'V>
        with
            member this.Count
                with get () = items.Count 
                
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
                
            member this.Set k v =
                this.Set k v
                
            member this.TryGetKeys keys =
                this.TryGetKeys keys
                
            member this.Remove k =
                this.Remove k
                
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

//type Creator () =
//    static member Value
//        with get () =
//            { new ICacheCreator
//                with
//                    member this.TryCreate<'V> logger name spec =
//                        match spec with
//                        | :? Specification as spec ->
//                            Some( new Cache<'V>( logger, name, spec ) :> ICache<'V> )
//                        | _ ->
//                            None }

    