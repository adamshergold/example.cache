namespace Example.Cache.Sqlite

open Microsoft.Extensions.Logging

open Microsoft.Data.Sqlite

open Example.Cache
open Example.Serialisation

type Options = {
    TimeToLiveSeconds : int option
}
with
    static member Default = {
        TimeToLiveSeconds = None
    }
    
type Cache( logger: ILogger, id:string, options:Options ) =

    let connection =
        new SqliteConnection( "Data Source=:memory:")
        
    let onSet = new Event<string*ITypeSerialisable>()
    
    let onGet = new Event<string>()
    
    let onRemove = new Event<string>()

    member val Id = id
    
    static member Make( logger ) =
        new Cache( logger ) :> IEnumerableCache
        
    member this.Dispose () =
        connection.Close()
        connection.Dispose()
        
    member this.Setup () =
        ()
        
    member this.Housekeep () =
        async {
            logger.LogDebug( "MemoryCache::Housekeep - Called")
            
            let now =
                System.DateTime.UtcNow
            
            let toRemove =
                lock this <| fun _ ->
                    Array.empty
                    
            toRemove
            |> Seq.iter ( fun k -> this.Remove k |> ignore )                     
            
            logger.LogDebug( "MemoryCache::Housekeep - Finished")
        }
        
    member this.Keys () =
        Array.empty
        
    member this.Set (k:string) (v:ITypeSerialisable) =
        logger.LogDebug( "MemoryCache::Set - Called with key {Key}", k )
        lock this <| fun _ ->
            failwithf "Not implemented"
            
    member this.Get (k:string) =
        logger.LogDebug( "MemoryCache::Get - Called with key {Key}", k )
        lock this <| fun _ ->
            failwithf "Failed to get value with key '%s' from '%s' cache" k id

    member this.Remove (k:string) =
        logger.LogDebug( "MemoryCache::Remove - Called with key {Key}", k )
        lock this <| fun _ ->
            false
            
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()
                
    interface IEnumerableCache
        with
            member this.Id =
                this.Id
                
            member this.Keys () =
                this.Keys()
                
            member this.Housekeep () =
                this.Housekeep()
                
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