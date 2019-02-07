namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open Example.Serialisation

open Example.Sql

module Helpers =
    
    let Setup (logger:ILogger) (cacheTable:string) (maxIdSize:int) (connection:IDbConnection) =
    
        let cmd =
            
            let text =
                if connection.ConnectorType.Equals("sqlserver",System.StringComparison.OrdinalIgnoreCase) then
                    sprintf
                        "IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '%s' AND TABLE_SCHEMA = 'dbo') BEGIN CREATE TABLE %s ( Id VARCHAR(%d) PRIMARY KEY, Body VARBINARY(max), Expiry DATETIME, Revision BIGINT ) END"
                            cacheTable cacheTable maxIdSize
                else
                    sprintf "CREATE TABLE IF NOT EXISTS %s ( Id VARCHAR(%d) PRIMARY KEY, Body BLOB, Expiry DATETIME, Revision BIGINT )" cacheTable maxIdSize
                    
            connection.CreateCommand text Seq.empty
        
        using( cmd.ExecuteReader() ) <| fun reader ->    
            reader.RecordsAffected
                
    
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
                (sprintf "SELECT Id FROM %s AS t WHERE Expiry > @Expiry AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable cacheTable)
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
        
    let Remove (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (k:string) =
        
        let cmd =
            connection.CreateCommand
                (sprintf "DELETE FROM %s WHERE Id = @Id" cacheTable)
                (Seq.singleton ("@Id",box(k)))

        using( cmd.ExecuteReader() ) <| fun reader ->
            reader.RecordsAffected        

            
    let Insert (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (serde:ISerde) (contentType:string) (ttl:int option) (k:string) (v:obj) =
        
        let cmd =
            let body =
                
                use ms =
                    new System.IO.MemoryStream()
            
                use stream =
                    SerdeStreamWrapper.Make(ms)
                
                serde.Serialise contentType stream v
                
                ms.ToArray()
            
            let revision =
                System.DateTime.UtcNow.Ticks
                
            if ttl.IsSome then
                
                let expiry =
                    System.DateTime.UtcNow.AddSeconds( (float) ttl.Value )
                    
                connection.CreateCommand
                    (sprintf "INSERT INTO %s ( Id, Body, Expiry, Revision ) VALUES ( @Id, @Body, @Expiry, @Revision )" cacheTable)
                    [ ( "@Id", box(k) ); ( "@Body", box(body) ); ( "@Revision", box(revision)); ( "@Expiry",box(expiry)) ]
            else
                connection.CreateCommand
                    (sprintf "INSERT INTO %s ( Id, Body, Revision ) VALUES ( @Id, @Body, @Revision )" cacheTable)
                    [ ( "@Id", box(k) ); ( "@Body", box(body) ); ( "@Revision", box(revision)) ]
        
        using( cmd.ExecuteReader() ) <| fun reader ->
            reader.RecordsAffected

            
    let TryGetKeys<'V> (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (serde:ISerde) (contentType:string) (keys:seq<string>) : Map<string,'V> =
        
        let inClause =
            keys |> Seq.mapi ( fun i k -> sprintf "@P%d" i ) |> String.concat ","
            
        let ps =
            keys |> Seq.mapi ( fun i k -> (sprintf "@P%d" i),box(k)) 
            
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT Id, Body FROM %s AS t WHERE Id IN ( %s ) AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable inClause cacheTable)
                ps

        let result =
            using( cmd.ExecuteReader() ) <| fun reader ->

                let bufferSize = 1024L
                let buffer : byte[] = Array.create ((int)bufferSize) ((byte)0)
                
                let extractRow () =
                    
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
                    
                let rec impl () =       
                    seq {
                        if reader.Read() then
                            let id, content = extractRow()
                            if content.IsSome then yield (id,content.Value)
                            yield! impl()
                    }
                
                impl() |> Map.ofSeq
                    
        result                                                    