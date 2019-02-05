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
         
//    let inlineCache =
//        let options = { Memory.Options.Default with TimeToLiveSeconds = Some 5 }
//        Memory.Cache<RV<'V>>.Make( logger, sprintf "inline-%s" name, options )
        
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
        //inlineCache.Dispose()
        ()
        
    member this.Setup () =
        
        logger.LogDebug( "SqlCache::Setup - Called")
        
        let cmd =
            connection.CreateCommand
                (sprintf "CREATE TABLE IF NOT EXISTS %s ( Id VARCHAR(%d) PRIMARY KEY, Body BLOB, Expiry DATETIME, Revision BIGINT )" cacheTable_Name cacheTable_MaxIdSize)
                Seq.empty
        
        let recordsAffected =
            using( cmd.ExecuteReader() ) <| fun reader ->    
                reader.RecordsAffected
                
        ()                
        
    member this.Purge () =
        
        logger.LogDebug( "SqlCache::Purge - Called")
        
        this.Check()
        
        let cmd =
            connection.CreateCommand
                (sprintf "DELETE FROM %s" cacheTable_Name)
                Seq.empty
            
        let recordsAffected=
            using( cmd.ExecuteReader() ) <| fun reader ->    
                reader.RecordsAffected
            
        logger.LogDebug( "SqlCache::Clean - Purge {ItemsRemoved} items", recordsAffected )
        
        recordsAffected
        
    member this.Clean () =
        async {
            logger.LogDebug( "SqlCache::Clean - Called")
            
            this.Check()
            
            let expiry =
                System.DateTime.UtcNow
                
            let cmd =
                connection.CreateCommand
                    (sprintf "DELETE FROM %s WHERE Expiry < @Expiry" cacheTable_Name)
                    [ ("@Expiry",box(expiry)) ]
                
            let recordsAffected =
                using( cmd.ExecuteReader() ) <| fun reader ->    
                    reader.RecordsAffected
                
            logger.LogDebug( "SqlCache::Clean - Removed {ItemsRemoved} items", recordsAffected )
        }
        
    member this.Keys () =
        
        logger.LogDebug( "SqlCache::Keys - Called" )
        
        this.Check()
        
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT Id FROM %s AS t WHERE Expiry > %s AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable_Name cacheTable_now cacheTable_Name)
                Seq.empty
                        
        let keys =
            using( cmd.ExecuteReader() ) <| fun reader ->
                let rec impl () =
                    seq {
                        if reader.Read() then
                            yield reader.GetString(0)
                            yield! impl() }
                impl() |> Array.ofSeq
                
        keys                
                    
    member this.Set (k:string) (v:ITypeSerialisable) =
        logger.LogDebug( "SqlCache::Set - Called with key {Key}", k )
        
        this.Check()
        
        let cmd =
            
            let body =
                
                use ms =
                    new System.IO.MemoryStream()
            
                use stream =
                    SerdeStreamWrapper.Make(ms)
                
                serde.Serialise options.ContentType stream v
                
                ms.ToArray()
            
            let revision =
                System.DateTime.UtcNow.Ticks
                
            if options.TimeToLiveSeconds.IsSome then
                let expiry =
                    System.DateTime.UtcNow.AddSeconds( (float) options.TimeToLiveSeconds.Value )
                connection.CreateCommand
                    (sprintf "INSERT INTO %s ( Id, Body, Expiry, Revision ) VALUES ( @Id, @Body, @Expiry, @Revision )" cacheTable_Name)
                    [ ( "@Id", box(k) ); ( "@Body", box(body) ); ( "@Revision", box(revision)); ( "@Expiry",box(expiry)) ]
            else
                connection.CreateCommand
                    (sprintf "INSERT INTO %s ( Id, Body, Revision ) VALUES ( @Id, @Body, @Revision )" cacheTable_Name)
                    [ ( "@Id", box(k) ); ( "@Body", box(body) ); ( "@Revision", box(revision)) ]
                
            
        using( cmd.ExecuteReader() ) <| fun reader ->
            reader.Close()
            
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
            this.Check()
            
            let cmd =
                connection.CreateCommand
                    (sprintf "SELECT Body FROM %s AS t WHERE Id = @Id AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable_Name cacheTable_Name)
                    (Seq.singleton ("@Id",box(k)))
    
            use ms =
                new System.IO.MemoryStream()
                
            let contentPresent =
                using( cmd.ExecuteReader() ) <| fun reader ->
                    if reader.Read() then
                        if reader.FieldCount > 0 then
                            let bufferSize = 1024L
                            let buffer : byte[] = Array.create ((int)bufferSize) ((byte)0)
                            let mutable offset = 0L
                            let mutable read = 0L
                            let mutable complete = false
                            while not complete do 
                                read <- reader.GetBytes( 0, offset, buffer, 0, buffer.Length )
                                offset <- offset + read
                                complete <- (read < bufferSize)
                                if read > 0L then ms.Write( buffer, 0, (int) read )
                            true
                        else
                            false
                    else
                        false
                        
            if contentPresent then
                
                ms.Seek( 0L, System.IO.SeekOrigin.Begin ) |> ignore
                
                use stream =
                    SerdeStreamWrapper.Make( ms )
                    
                let result =
                    serde.DeserialiseT<'V> options.ContentType stream
                
                Some <| result    
            else
                None
            
        retrieve k
        
//        lock inlineCache <| fun _ ->
//            match inlineCache.TryGet k with
//            | Some rv ->
//                rv.Value
//            | None ->
//                let rv = RV<_>.Make( None, (fun () -> retrieve k), Some options.InlineRefeshIntervalSeconds, Some options.InlineTimeToLiveSeconds )
//                inlineCache.Set k rv
//                rv.Value
           

    member this.Remove (k:string) =
        logger.LogDebug( "SqlCache::Remove - Called with key {Key}", k )

        
        this.Check()
        
        let cmd =
            connection.CreateCommand
                (sprintf "DELETE FROM %s WHERE Id = @Id" cacheTable_Name)
                (Seq.singleton ("@Id",box(k)))

        let recordsAffected =
            using( cmd.ExecuteReader() ) <| fun reader ->
                reader.RecordsAffected

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