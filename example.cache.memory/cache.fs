namespace Example.Cache.Memory

open Microsoft.Extensions.Logging

open Example.Cache
open Example.Serialisation

[<AutoOpen>]
module private MemoryCacheImpl =
    
    type CacheEntry( item: ITypeSerialisable, ttlSeconds: int option ) =
        
        let expires =
            ttlSeconds |> Option.map ( fun ttl -> System.DateTime.UtcNow.AddSeconds( (float) ttl ) )
            
        member val Item = item
        
        member val Expires = expires
        
        static member Make( item, ttl ) =
            new CacheEntry( item, ttl )
            
            
type Options = {
    InitialCapacity : int
    TimeToLiveSeconds : int option
}
with
    static member Default = {
        InitialCapacity = 512
        TimeToLiveSeconds = Some 60
    }
    
type Cache( logger: ILogger, name:string, options:Options ) =
    
    let items =
        new System.Collections.Generic.Dictionary<string,CacheEntry>( options.InitialCapacity )
        
    let onSet =
        new Event<string*ITypeSerialisable>()
    
    let onGet =
        new Event<string>()
    
    let onRemove =
        new Event<string>()

    member val Name = name
    
    static member Make( logger ) =
        new Cache( logger ) :> IEnumerableCache
        
    member this.Dispose () =
        lock this <| fun _ ->
            items.Clear()
            
    member this.Clean () =
        async {
            logger.LogDebug( "MemoryCache::Clean - Called")
            
            let now =
                System.DateTime.UtcNow
            
            let toRemove =
                lock this <| fun _ ->
                    items.Keys
                    |> Seq.filter ( fun k ->
                        let ce = items.Item(k)
                        ce.Expires.IsSome && ce.Expires.Value < now )
                    |> Array.ofSeq
                    
            toRemove
            |> Seq.iter ( fun k -> this.Remove k |> ignore )                     
            
            logger.LogDebug( "MemoryCache::Clean - Finished")
        }
        
    member this.Keys () =
        lock this <| fun _ ->
            items.Keys |> Array.ofSeq
            
    member this.Set (k:string) (v:ITypeSerialisable) =
        lock this <| fun _ ->
            if items.ContainsKey k then 
                this.Remove k |> ignore
            let ce = CacheEntry.Make( v, options.TimeToLiveSeconds )
            logger.LogDebug( "MemoryCache::Set - Called with key {Key} (expires {Expires})", k, ce.Expires)
            items.Add( k, ce )
            onSet.Trigger( (k,v) )

    member this.Get (k:string) =
        logger.LogDebug( "MemoryCache::Get - Called with key {Key}", k )
        lock this <| fun _ ->
            match items.TryGetValue k with
            | true, v ->
                let result = v.Item
                onGet.Trigger(k)
                result
            | false, _ ->
                failwithf "Failed to get value with key '%s' from '%s' cache" k name

    member this.Remove (k:string) =
        logger.LogDebug( "MemoryCache::Remove - Called with key {Key}", k )
        lock this <| fun _ ->
            let result = items.Remove k
            onRemove.Trigger(k)
            result
                
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()
                
    interface IEnumerableCache
        with
            member this.Name =
                this.Name
                
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