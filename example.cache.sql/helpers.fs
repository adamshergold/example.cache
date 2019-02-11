namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open Example.Serialisation

open Example.Sql

module Helpers =
    
    let Setup (logger:ILogger) (cacheTable:string) (maxIdSize:int) (connection:IDbConnection) =
    
        let createTable =
            
            let text =
                if connection.ConnectorType.Equals("sqlite",System.StringComparison.OrdinalIgnoreCase) then
                    sprintf "CREATE TABLE IF NOT EXISTS %s ( Id INTEGER PRIMARY KEY, CKey VARCHAR(%d) NOT NULL, Body BLOB, Expiry DATETIME, Revision BIGINT NOT NULL )" cacheTable maxIdSize
                elif connection.ConnectorType.Equals("sqlserver",System.StringComparison.OrdinalIgnoreCase) then
                    sprintf
                        "IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '%s' AND TABLE_SCHEMA = 'dbo') BEGIN CREATE TABLE %s ( Id INT IDENTITY(1,1) NOT NULL, CKey VARCHAR(%d), Body VARBINARY(max), Expiry DATETIME, Revision BIGINT ) END"
                            cacheTable cacheTable maxIdSize
                elif connection.ConnectorType.Equals("mysql",System.StringComparison.OrdinalIgnoreCase) then
                    sprintf "CREATE TABLE IF NOT EXISTS %s ( Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, CKey VARCHAR(%d) NOT NULL, Body BLOB, Expiry DATETIME, Revision BIGINT NOT NULL )" cacheTable maxIdSize
                else
                    failwithf "Do not know how to initialise table for connector-type '%s'" connection.ConnectorType
                    
            connection.CreateCommand text Seq.empty
        
        let createIndex =
            let text =
                if connection.ConnectorType.Equals("sqlite",System.StringComparison.OrdinalIgnoreCase) then
                    Some <| sprintf "CREATE INDEX CKey_Index ON %s ( CKey )" cacheTable
                elif connection.ConnectorType.Equals("mysql",System.StringComparison.OrdinalIgnoreCase) then
                    Some <| sprintf "CREATE INDEX CKey_Index ON %s ( CKey )" cacheTable
                elif connection.ConnectorType.Equals("sqlserver",System.StringComparison.OrdinalIgnoreCase) then
                    Some <| sprintf "CREATE INDEX CKey_Index ON %s ( CKey )" cacheTable
                else
                    failwithf "Do not know how to initialise table for connector-type '%s'" connection.ConnectorType
            
            text |> Option.map ( fun text -> connection.CreateCommand text Seq.empty )
            
        let nRecordsAffected =
            createTable.ExecuteNonQuery()
            
        if nRecordsAffected > 0 && createIndex.IsSome then 
            createIndex.Value.ExecuteNonQuery() |> ignore
                        
    
    let Purge (logger:ILogger) (cacheTable:string) (connection:IDbConnection) =
        
            let exists =
                
                let cmd =
                    
                    let text =
                        if connection.ConnectorType.Equals("sqlite", System.StringComparison.OrdinalIgnoreCase) then
                            sprintf "SELECT * FROM sqlite_master WHERE name ='%s' and type='table'" cacheTable
                        else if connection.ConnectorType.Equals("mysql", System.StringComparison.OrdinalIgnoreCase) then 
                            sprintf "SELECT * FROM information_schema.TABLES WHERE TABLE_NAME='%s' AND TABLE_SCHEMA=DATABASE()" cacheTable
                        else
                            sprintf "SELECT * FROM information_schema.TABLES WHERE TABLE_NAME='%s' AND TABLE_SCHEMA='dbo'" cacheTable
                            
                    connection.CreateCommand text Seq.empty
                    
                using( cmd.ExecuteReader() ) <| fun reader ->
                    reader.HasRows

            let recordsAffected =
                if exists then                     
                    let cmd =
                        connection.CreateCommand
                            (sprintf "DELETE FROM %s" cacheTable)
                            Seq.empty
                        
                    using( cmd.ExecuteReader() ) <| fun reader ->    
                        reader.RecordsAffected
                else
                    0
                    
            recordsAffected
                
    let Clean (logger:ILogger) (cacheTable:string) (connection:IDbConnection) =
    
        let expiry =
            System.DateTime.UtcNow
            
        let cmd =
            connection.CreateCommand
                (sprintf "DELETE FROM %s WHERE Expiry < @Expiry" cacheTable)
                [ ("@Expiry",box(expiry)) ]
            
        let recordsAffected =
            using( cmd.ExecuteReader() ) <| fun reader ->    
                reader.RecordsAffected
                
        recordsAffected
        
    let Keys (logger:ILogger) (cacheTable:string) (connection:IDbConnection) =
    
        let cmd =
            
            let expiry =
                System.DateTime.UtcNow
                
            connection.CreateCommand
                (sprintf "SELECT CKey FROM %s AS t WHERE Expiry > @Expiry AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable cacheTable)
                (Seq.singleton ("@Expiry",box(expiry)))
                        
        let keys =
            using( cmd.ExecuteReader() ) <| fun reader ->
                let rec impl () =
                    seq {
                        if reader.Read() then
                            yield reader.GetString(0)
                            yield! impl() }
                impl() |> Array.ofSeq
    
        keys
        
    let Remove (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (ks:string[]) =
        
        let cmd =
            
            let inClause =
                ks |> Seq.mapi ( fun idx _ -> sprintf "@p%d" idx ) |> String.concat ","
            
            let ps =
                ks |> Seq.mapi ( fun idx k -> sprintf "@p%d" idx, box(k) ) 
                
            connection.CreateCommand
                (sprintf "DELETE FROM %s WHERE CKey IN ( %s )" cacheTable inClause)
                ps

        using( cmd.ExecuteReader() ) <| fun reader ->
            reader.RecordsAffected        

            
    let Insert<'V> (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (serde:ISerde) (contentType:string) (ttl:int option) (kvs:(string*'V)[]) =

        let body (v:'v) =
            use ms =
                new System.IO.MemoryStream()
        
            use stream =
                SerdeStreamWrapper.Make(ms)
            
            serde.Serialise contentType stream v
            
            ms.ToArray()
            
        using( connection.BeginTransaction() ) <| fun transaction ->    
            let cmd =
                
                let sb = System.Text.StringBuilder()
                
                sb.Append( "INSERT INTO " ).Append( cacheTable ).Append("( CKey, Body, Expiry, Revision ) VALUES ") |> ignore
                
                for i = 0 to kvs.Length-2 do
                    sb.Append( sprintf "(@k%d,@b%d,@e%d,@r)," i i i) |> ignore
                   
                sb.Append( sprintf "(@k%d,@b%d,@e%d,@r)" (kvs.Length-1) (kvs.Length-1) (kvs.Length-1) ) |> ignore
                
                let revision =
                    System.DateTime.UtcNow.Ticks
                    
                let ps =
                    let expiry =
                        ttl |> Option.map ( fun ttl -> System.DateTime.UtcNow.AddSeconds( (float) ttl ) )
                        
                    kvs
                    |> Array.mapi ( fun idx (k,v) ->
                        seq {
                            yield (sprintf "@k%d" idx, box(k) )
                            yield (sprintf "@b%d" idx, box(body v) )
                            yield (sprintf "@e%d" idx, if expiry.IsSome then box(expiry.Value) else box(System.DBNull.Value) )
                        } )
                    |> Seq.concat
                    |> Seq.append (Seq.singleton ( "@r", box(revision) ) )
                        
                connection.CreateCommand
                    (sb.ToString())
                    ps
                    
            let nRecordsAffected =
                cmd.ExecuteNonQuery() 
    
            transaction.Commit()
            
            nRecordsAffected
        
    let TryGet<'V> (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (serde:ISerde) (contentType:string) (keys:seq<string>) : (string*'V)[] =
        
        let inClause =
            keys |> Seq.mapi ( fun i k -> sprintf "@P%d" i ) |> String.concat ","
            
        let ps =
            keys |> Seq.mapi ( fun i k -> (sprintf "@P%d" i),box(k)) 
            
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT CKey, Body FROM %s AS t WHERE CKey IN ( %s ) AND Revision = ( SELECT MAX(Revision) FROM %s WHERE CKey = t.CKey )" cacheTable inClause cacheTable)
                ps

        let extractRow =
            
            let bufferSize = 1024L
            let buffer : byte[] = Array.create ((int)bufferSize) ((byte)0)

            fun (reader:System.Data.Common.DbDataReader) ->
                
                let id =
                    reader.GetString(0)
                    
                use ms =
                    new System.IO.MemoryStream()
        
                let contentPresent =
                    if reader.FieldCount > 1 then
                        let mutable offset = 0L
                        let mutable read = 0L
                        let mutable complete = false
                        while not complete do 
                            read <- reader.GetBytes( 1, offset, buffer, 0, buffer.Length )
                            offset <- offset + read
                            complete <- (read < bufferSize)
                            if read > 0L then ms.Write( buffer, 0, (int) read )
                        true
                    else
                        false
                
                if contentPresent then
                    
                    ms.Seek( 0L, System.IO.SeekOrigin.Begin ) |> ignore
                    
                    use stream =
                        SerdeStreamWrapper.Make( ms )
                        
                    let result =
                        serde.DeserialiseT<'V> contentType stream
        
                    id, Some result    
                else
                    id, None                

        try
            let result =
                using( cmd.ExecuteReader() ) <| fun reader ->
                    let rec impl () =       
                        seq {
                            if reader.Read() then
                                let id, content = extractRow reader
                                if content.IsSome then yield (id,content.Value)
                                yield! impl()
                        }
                    
                    impl() |> Array.ofSeq
                        
            result 
        with
        | _ as ex ->
            logger.LogError( "{Exception}", ex.Message )
            reraise()