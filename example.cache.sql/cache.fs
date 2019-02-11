namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open App.Metrics

open Example.Serialisation

open Example.Cache
open Example.Cache.Core

open Example.Sql
    
type Cache<'V when 'V :> ITypeSerialisable>( logger: ILogger, name:string, metrics:IMetrics, serde:ISerde, connection:IDbConnection, spec:Specification ) =

    let timer =
        Metrics.CreateTimer "cache-requests" (Some name)
        
    let cacheTable_Name =
        sprintf "cache_%s" name
    
    let cacheTable_MaxIdSize = 128
    
    let inlineCache =
        let spec = { Memory.Specification.Default with TimeToLiveSeconds = Some 5 }
        Memory.Cache<'V>.Make( logger, sprintf "inline-%s" name, spec )
        
    let statistics =
        Core.Statistics.Make()
        
    let onSet = new Event<string>()
    
    let onGet = new Event<string>()
    
    let onRemove = new Event<string>()
    
    let onEvicted = new Event<string>()

    let mutable isSetup = false
    
    member val Name = name
    
    member val Statistics = statistics
    
    static member Make<'V>( logger, name, metrics, serde, connection, spec:ICacheSpecification ) =
        match spec with
        | :? Sql.Specification as spec ->
            new Cache<'V>( logger, name, metrics, serde, connection, spec ) :> IEnumerableCache<'V>
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

        logger.LogTrace( "SqlCache::Setup - {nRecordsAffected} records affected", nRecordsAffected )

                
    member this.Purge () =
        
        logger.LogTrace( "SqlCache::Purge - Called")
        
        lock this <| fun _ ->
            
            inlineCache.Purge() |> ignore
            
            this.Check()
            
            let nRemoved =
                Helpers.Purge logger cacheTable_Name connection
            
            logger.LogTrace( "SqlCache::Clean - Purged {ItemsRemoved} items from store", nRemoved )
            
            nRemoved
        
        
    member this.Clean () =
        
        async {
            logger.LogTrace( "SqlCache::Clean - Called")
            
            this.Check()
            
            let nCleaned =
                Helpers.Clean logger cacheTable_Name connection
                
            logger.LogTrace( "SqlCache::Clean - Removed {ItemsRemoved} items from store", nCleaned )
            
            do! inlineCache.Clean()
        }
        
        
    member this.Keys () =
        
        logger.LogTrace( "SqlCache::Keys - Called" )
        
        this.Check()
        
        Helpers.Keys logger cacheTable_Name connection
                   
                    
    member this.Set (kvs:(string*'V)[]) =
        
        let tags = new MetricTags( "action", "set" )
        using( metrics.Measure.Timer.Time( timer, tags ) ) <| fun timerContext ->
            
            this.Check()
            
            let nRecordsAffected =
                Helpers.Insert logger cacheTable_Name connection serde spec.ContentType spec.TimeToLiveSeconds kvs 
    
            if nRecordsAffected <> kvs.Length then
                failwithf "Mismatch between number of items provided ('%d') and number written ('%d')" kvs.Length nRecordsAffected
                
            logger.LogTrace( "SqlCache::Set - {nRecordsAffected} {nKeys}", nRecordsAffected, kvs.Length)
                
            
    member this.Exists (k:string) =
        
        logger.LogTrace( "SqlCache::Exists - Called with key {Key}", k )
        
        this.Check()
        
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT COUNT(*) FROM %s WHERE CKey = @CKey" cacheTable_Name)
                (Seq.singleton ("@CKey",box(k)))

        using( cmd.ExecuteReader() ) <| fun reader ->
            if reader.Read() then
                reader.GetInt32(0) > 0
            else
                false
                
                
    member this.TryGet (keys:string[]) =
        
        this.Check()
        
        let tryGetInline =
            inlineCache.TryGet keys
            
        let missingKeys =
            tryGetInline
            |> Seq.mapi ( fun idx v ->
                if v.IsSome then None else Some keys.[idx] )
            |> Seq.choose id
            
        let tryGetFromStore =
            Helpers.TryGet<'V> logger cacheTable_Name connection serde spec.ContentType missingKeys

        //tryGetFromStore |> Map.iter ( fun k v -> inlineCache.Set k v )
        inlineCache.Set tryGetFromStore
        
        let asMap = tryGetFromStore |> Map.ofSeq
        
        Array.init keys.Length (fun idx ->
            let prev = tryGetInline.[idx]
            if prev.IsSome then prev else asMap.TryFind keys.[idx] )
    
    
    member this.Remove (ks:string[]) =
        logger.LogTrace( "SqlCache::Remove - Called with {nKey} keys", ks.Length )

        this.Check()
        
        lock this <| fun _ ->

            inlineCache.Remove ks |> ignore
            
            let recordsAffected =
                Helpers.Remove logger cacheTable_Name connection ks
    
            recordsAffected 
                 
                    
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
                
            member this.Set kvs =
                this.Set kvs
                
            member this.TryGet keys =
                this.TryGet keys
                
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