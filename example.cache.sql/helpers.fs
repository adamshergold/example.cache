namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open Example.Serialisation

open Example.Cache.Sql

module Helpers =
    
    let Setup (logger:ILogger) (cacheTable:string) (maxIdSize:int) (connection:IDbConnection) =
    
        let cmd =
            connection.CreateCommand
                (sprintf "CREATE TABLE IF NOT EXISTS %s ( Id VARCHAR(%d) PRIMARY KEY, Body BLOB, Expiry DATETIME, Revision BIGINT )" cacheTable maxIdSize)
                Seq.empty
        
        using( cmd.ExecuteReader() ) <| fun reader ->    
            reader.RecordsAffected
                
    
    let Purge (logger:ILogger) (cacheTable:string) (connection:IDbConnection) =
        
            let cmd =
                connection.CreateCommand
                    (sprintf "DELETE FROM %s" cacheTable)
                    Seq.empty
                
            let recordsAffected=
                using( cmd.ExecuteReader() ) <| fun reader ->    
                    reader.RecordsAffected
                
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
        
    let Keys (logger:ILogger) (cacheTable:string) (cacheNow:string) (connection:IDbConnection) =
    
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT Id FROM %s AS t WHERE Expiry > %s AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable cacheNow cacheTable)
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

    let TryGet (logger:ILogger) (cacheTable:string) (connection:IDbConnection) (serde:ISerde) (contentType:string) (k:string) =
        
        let cmd =
            connection.CreateCommand
                (sprintf "SELECT Body FROM %s AS t WHERE Id = @Id AND Revision = ( SELECT MAX(Revision) FROM %s WHERE Id = t.Id )" cacheTable cacheTable)
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
                serde.DeserialiseT<'V> contentType stream
            
            Some <| result    
        else
            None        