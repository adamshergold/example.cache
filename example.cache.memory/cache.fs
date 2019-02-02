namespace Example.Cache.Memory

open Microsoft.Extensions.Logging

open Example.Serialisation

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
        Item : 'V
        LRUNode : System.Collections.Generic.LinkedListNode<LRUItem<'K>>
    }
    with 
        static member Make( item, n ) = 
            { Item = item; LRUNode = n }

type Options = {
    InitialCapacity : int option
    MaxSize : int option
    TimeToLiveSeconds : int option
}
with
    static member Default = {
        InitialCapacity = None
        MaxSize = None
        TimeToLiveSeconds = Some 60
    }
    
    interface ICacheOptions
    
type Cache<'K,'V when 'K : comparison> ( logger: ILogger, name:string, options:Options ) =
    
    let makeLRUNode (k:'K) = 
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
        if options.InitialCapacity.IsSome then 
            new System.Collections.Generic.Dictionary<'K,CacheItem<'K,'V>>( options.InitialCapacity.Value )
        else
            if options.MaxSize.IsSome then 
                new System.Collections.Generic.Dictionary<'K,CacheItem<'K,'V>>( options.MaxSize.Value )
            else
                new System.Collections.Generic.Dictionary<'K,CacheItem<'K,'V>>()
                
    let lru = 
        new System.Collections.Generic.LinkedList<LRUItem<'K>>() 
        
    let onSet =
        new Event<'K>()
    
    let onGet =
        new Event<'K>()
    
    let onRemove =
        new Event<'K>()

    let onEvicted =
        new Event<'K>()
        
    let remove (k:'K) = 
        if items.ContainsKey k then
            let ci = items.Item(k)
            lru.Remove(ci.LRUNode)
            let result = items.Remove(k)
            onRemove.Trigger( k )
            result
        else 
            false 
    
    member val Name = name
    
    member val Statistics = statistics
    
    static member Make<'K,'V>( logger, name, options:ICacheOptions ) =
        match options with
        | :? Options as options ->
            new Cache<'K,'V>( logger, name, options ) :> IEnumerableCache<'K,'V>
        | _ ->
            failwithf "Invalid options type passed to Memory constructor!"
        
    member this.Dispose () =
        lock this <| fun _ ->
            items.Clear()
            
    member this.Clean () =
        async {
            return ()
        }
        
    member this.Exists (k:'K) =
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
            if options.MaxSize.IsSome then 
                let n = ref 0
                withWriteLock <| fun () ->
                    while items.Count > options.MaxSize.Value do
                        if removeLast() then System.Threading.Interlocked.Increment( n ) |> ignore else ()
                !n     
            else 
                0
                
        let expiredRemoved = 
        
            let now =   
                System.DateTime.UtcNow  
                
            let lastExpired ttlSeconds = 
                (now - lru.Last.Value.CreatedAt).TotalSeconds > (float) ttlSeconds 
                
            if options.TimeToLiveSeconds.IsSome then 
                let n = ref 0 
                withWriteLock <| fun () ->
                    while lru.Count > 0 && lastExpired options.TimeToLiveSeconds.Value do
                        if removeLast() then System.Threading.Interlocked.Increment( n ) |> ignore else ()
                !n     
            else 
                0
                    
        logger.LogTrace( "MemoryCache::Flush({CacheName}) - Removed {Removed} items", this.Name, (oversizedRemoved + expiredRemoved) )
        
        oversizedRemoved + expiredRemoved           
        
    member this.Set (k:'K) (v:'V) =
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
                    CacheItem<_,_>.Make( v, makeLRUNode k )                     
                        
            // put the item into the cache                        
            items.Add( k, newItem )
            // add the node to front of LRU list
            lru.AddFirst( newItem.LRUNode )
            
        let nFlushed = this.Flush()
        onSet.Trigger( k)

    member this.Get (k:'K) =
        logger.LogTrace( "MemoryCache::Get({CacheName}) - Called with key {Key}", this.Name, k )        
        statistics.Get()
        withReadLock <| fun _ ->
            
            let exists, item =
                items.TryGetValue k

            if not exists then
                failwithf "No item in cache for key - %A" k
            else
                statistics.Hit()
                
                lru.Remove( item.LRUNode )
                
                items.Remove( k ) |> ignore
                
                let newItem =                        
                    CacheItem<_,_>.Make( item.Item, makeLRUNode k ) 
        
                items.Add( k, newItem )
                lru.AddFirst( newItem.LRUNode )
                        
                onGet.Trigger(k)
                
                newItem.Item                            

    member this.Remove (k:'K) =
        logger.LogTrace( "MemoryCache::Remove({CacheName}) - Called with key {Key}", this.Name, k )
        statistics.Remove()
        withWriteLock <| fun _ ->
            remove k 
                
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()
                
    interface IEnumerableCache<'K,'V>
        with
            member this.Count
                with get () = items.Count 
                
            member this.Statistics =
                this.Statistics :> IStatistics
                
            member this.Name =
                this.Name

            member this.Exists k =
                this.Exists k
                            
            member this.Clean () =
                this.Clean()
                
            member this.Keys () =
                this.Keys()
                
            member this.Set k v =
                this.Set k v
                
            member this.Get k =
                this.Get k
                
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
