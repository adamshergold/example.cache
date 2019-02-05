namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open Example.Serialisation

open Example.Cache
open Example.Cache.Core
    
type Cache<'V when 'V :> ITypeSerialisable>( logger: ILogger, name:string, serde:ISerde, connection:IDbConnection, options:Options ) =

    let cacheTable_Name =
        sprintf "cache_%s" name
    
    let cacheTable_MaxIdSize = 128
    
    let cacheTable_now =
        if connection.ConnectorType.Equals("sqlite",System.StringComparison.OrdinalIgnoreCase) then
            "DATETIME('now')"
        else
            "NOW()"
         
    let inlineCache =
        let options = { Memory.Options.Default with TimeToLiveSeconds = Some 5 }
        Memory.Cache<RV<'V>>.Make( logger, sprintf "inline-%s" name, options )
        
    let statistics =
        Core.Statistics.Make()
        
    let onSet = new Event<string>()
    
    let onGet = new Event<string>()
    
    let onRemove = new Event<string>()
    
    let onEvicted = new Event<string>()

    let mutable isSetup = false
    
    member val Name = name
    
    member val Statistics = statistics
    
    static member Make<'V>( logger, name, serde, connection, options:ICacheOptions ) =
        match options with
        | :? Sql.Options as options ->
            new Cache<'V>( logger, name, serde, connection, options ) :> IEnumerableCache<'V>
        | _ ->
            failwithf "Invalid options type passed to Sqlite constructor!"
        
    member this.Check () =
        
        connection.Check()
        
        if not isSetup then
            lock this <| fun _ ->
                this.Setup()
                isSetup <- true
        
        
    member this.Dispose () =
        inlineCache.Dispose()
        
        
    member this.Setup () =
        
        let nRecordsAffected =
            Helpers.Setup logger cacheTable_Name cacheTable_MaxIdSize connection 

        logger.LogDebug( "SqlCache::Setup - {nRecordsAffected} records affected", nRecordsAffected )

                
    member this.Purge () =
        
        logger.LogDebug( "SqlCache::Purge - Called")
        
        lock this <| fun _ ->
            
            inlineCache.Purge() |> ignore
            
            let nRemoved =
                Helpers.Purge logger cacheTable_Name connection
            
            logger.LogDebug( "SqlCache::Clean - Purged {ItemsRemoved} items from store", nRemoved )
            
            nRemoved
        
        
    member this.Clean () =
        
        async {
            logger.LogDebug( "SqlCache::Clean - Called")
            
            this.Check()
            
            let nCleaned =
                Helpers.Clean logger cacheTable_Name connection
                
            logger.LogDebug( "SqlCache::Clean - Removed {ItemsRemoved} items from store", nCleaned )
            
            do! inlineCache.Clean()
        }
        
        
    member this.Keys () =
        
        logger.LogDebug( "SqlCache::Keys - Called" )
        
        this.Check()
        
        Helpers.Keys logger cacheTable_Name cacheTable_now connection
                   
                    
    member this.Set (k:string) (v:ITypeSerialisable) =
        
        this.Check()
        
        let nRecordsAffected =
            Helpers.Insert logger cacheTable_Name connection serde options.ContentType options.TimeToLiveSeconds k v 

        logger.LogDebug( "SqlCache::Set - {nRecordsAffected} for key {Key}", nRecordsAffected, k )
                
            
    member this.Exists (k:string) =
        
        logger.LogDebug( "SqlCache::Exists - Called with key {Key}", k )
        
        this.Check()
        
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT COUNT(*) FROM %s WHERE Id = @Id" cacheTable_Name)
                (Seq.singleton ("@Id",box(k)))

        using( cmd.ExecuteReader() ) <| fun reader ->
            if reader.Read() then
                reader.GetInt32(0) > 0
            else
                false
                
                
    member this.TryGet (k:string) =
        logger.LogDebug( "SqlCache::TryGet - Called with key {Key}", k )

        let retrieve (k:string) =
            logger.LogDebug( "SqlCache::TryGet - Retrieve called for key {Key}", k )
            this.Check()
            Helpers.TryGet logger cacheTable_Name connection serde options.ContentType k 

        let refreshingValue =
            lock inlineCache <| fun _ ->
                match inlineCache.TryGet k with
                | Some rv ->
                    rv
                | None ->
                    let rv =
                        RV<_>.Make(
                            None,
                            (fun () -> retrieve k),
                            Some (1000*options.InlineRefeshIntervalSeconds),
                            Some (1000*options.InlineTimeToLiveSeconds) )
                    inlineCache.Set k rv
                    rv

        let result =
            refreshingValue.Value
            
        logger.LogDebug( "SqlCache::TryGet - {Result} for key {Key}", (if result.IsSome then "result found" else "no result"), k )
           
        result   

    
    member this.Remove (k:string) =
        logger.LogDebug( "SqlCache::Remove - Called with key {Key}", k )

        lock this <| fun _ ->
            
            inlineCache.Remove k |> ignore
            
            this.Check()
            
            let recordsAffected =
                Helpers.Remove logger cacheTable_Name connection k
    
            recordsAffected > 0
                    
                    
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()

    
    interface IEnumerableCache<'V>
        with
            member this.Count
                with get () = 0 
                
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
                
            member this.TryGet k =
                this.TryGet k
                
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