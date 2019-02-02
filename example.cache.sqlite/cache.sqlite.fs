namespace Example.Cache.Sqlite

open Microsoft.Extensions.Logging

open Microsoft.Data.Sqlite

open Example.Serialisation

open Example.Cache
open Example.Cache.Core 

type Options = {
    TimeToLiveSeconds : int option
}
with
    static member Default = {
        TimeToLiveSeconds = None
    }
    
    interface ICacheOptions
//    
//type Cache( logger: ILogger, name:string, options:Options ) =
//
//    let cacheTable_Name =
//        sprintf "cache_%s" name
//    
//    let cacheTable_MaxIdSize = 128
//    
//    let statistics =
//        Statistics.Make()
//        
//    let connection =
//        new SqliteConnection( "Data Source=:memory:")
//        
//    let onSet = new Event<string*ITypeSerialisable>()
//    
//    let onGet = new Event<string>()
//    
//    let onRemove = new Event<string>()
//
//    member val Name = name
//    
//    member val Statistics = statistics
//    
//    static member Make( logger, name, options:ICacheOptions ) =
//        match options with
//        | :? Options as options ->
//            new Cache( logger, name, options ) :> IEnumerableCache
//        | _ ->
//            failwithf "Invalid options type passed to Sqlite constructor!"
//        
//    member this.Check () =
//        if not <| connection.State.Equals(System.Data.ConnectionState.Open) then
//            connection.Open()
//        
//    member this.Dispose () =
//        connection.Close()
//        connection.Dispose()
//        
//    member this.Setup () =
//        
//        logger.LogDebug( "SqliteCache::Setup - Called")
//        
//        this.Check()
//        
//        let cmd =
//            new SqliteCommand( sprintf "CREATE TABLE IF NOT EXISTS %s ( Id VARCHAR(%d) PRIMARY KEY, Body TEXT, Expiry DATETIME, Revision INTEGER )" cacheTable_Name cacheTable_MaxIdSize, connection )
//        
//        let recordsAffected =
//            using( cmd.ExecuteReader() ) <| fun reader ->    
//                reader.RecordsAffected
//                
//        ()                
//        
//    member this.Clean () =
//        async {
//            logger.LogDebug( "SqliteCache::Clean - Called")
//            
//            this.Check()
//            
//            let now =
//                System.DateTime.UtcNow
//            
//            let cmd =
//                new SqliteCommand( sprintf "DELETE FROM %s WHERE Expiry < DATETIME('now')" cacheTable_Name, connection )
//                
//            let recordsAffected=
//                using( cmd.ExecuteReader() ) <| fun reader ->    
//                    reader.RecordsAffected
//                
//            logger.LogDebug( "SqliteCache::Clean - Removed {ItemsRemoved} items", recordsAffected )
//        }
//        
//    member this.Keys () =
//        
//        logger.LogDebug( "SqliteCache::Keys - Called")
//        
//        this.Check()
//        
//        let cmd =
//            new SqliteCommand( sprintf "SELECT Id FROM %s AS t WHERE Expiry > DATETIME('now') AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable_Name cacheTable_Name, connection )
//        
//        let keys =
//            using( cmd.ExecuteReader() ) <| fun reader ->
//                let rec impl () =
//                    seq {
//                        if reader.Read() then yield reader.GetString(0) else () }
//                impl() |> Array.ofSeq
//                
//        keys                
//                    
//    member this.Set (k:string) (v:ITypeSerialisable) =
//        logger.LogDebug( "SqliteCache::Set - Called with key {Key}", k )
//        lock this <| fun _ ->
//            failwithf "Not implemented"
//            
//    member this.Get (k:string) =
//        logger.LogDebug( "SqliteCache::Get - Called with key {Key}", k )
//        lock this <| fun _ ->
//            failwithf "Failed to get value with key '%s' from '%s' cache" k name
//
//    member this.Remove (k:string) =
//        logger.LogDebug( "SqliteCache::Remove - Called with key {Key}", k )
//        lock this <| fun _ ->
//            false
//            
//    interface System.IDisposable
//        with
//            member this.Dispose () =
//                this.Dispose()
//                
//    interface IEnumerableCache
//        with
//            member this.Name =
//                this.Name
//            
//            member this.Statistics 
//                with get () = this.Statistics :> IStatistics
//                
//            member this.Keys () =
//                this.Keys()
//                
//            member this.Clean () =
//                this.Clean()
//                
//            member this.Set k v =
//                this.Set k v
//                
//            member this.Get k =
//                this.Get k
//                
//            member this.Remove k =
//                this.Remove k
//                
//            [<CLIEvent>]                    
//            member this.OnSet =
//                onSet.Publish
//                
//            [<CLIEvent>]                    
//            member this.OnGet =
//                onGet.Publish
//                
//            [<CLIEvent>]                    
//            member this.OnRemove =
//                onRemove.Publish                                                                                                                                         