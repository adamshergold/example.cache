namespace Example.Cache.Sql

open Microsoft.Extensions.Logging

open Microsoft.Data.Sqlite
open MySql.Data.MySqlClient
open System.Data.SqlClient

type DbConnectionFactory( logger: ILogger, initialConnections:seq<string*DbConnectionSpecification> ) as this =
    
    let connections =
        new System.Collections.Generic.Dictionary<string,IDbConnection>()

    do
        initialConnections
        |> Seq.iter ( fun (name,spec) ->
            this.Create name spec |> ignore )
        
    static member Make( logger ) =
        new DbConnectionFactory( logger, Seq.empty )

    static member Make( logger, initials ) =
        new DbConnectionFactory( logger, initials )
    
    member this.Dispose () =
        logger.LogDebug( "DbConnectionFactory::Dispose - {nConnections} existing connections", connections.Count )
        connections.Values |> Seq.iter ( fun dbc -> dbc.Dispose() )
        connections.Clear()
        
    member this.Create (name:string) (spec:DbConnectionSpecification) =
        
        logger.LogDebug( "DbConnectionFactory::Create - Called for {Name} with specification {DbConnectionSpecification}", name, spec )
        
        lock connections <| fun _ ->
            let connector =
                match spec with
                | DbConnectionSpecification.Sqlite(spec) ->
                    DbConnector.Make( new SqliteConnection( spec.ConnectionString ) )
                    
                | DbConnectionSpecification.MySql(spec) ->
                    let connectionString =
                        [
                            "Server", spec.Server
                            "Uid", spec.UserName
                            "Pwd", spec.Password
                            "Database", spec.Database
                        ]
                        |> Seq.map ( fun (k,v) ->
                            sprintf "%s=%s" k v )
                        |> String.concat ";"
                    DbConnector.Make( new MySqlConnection(connectionString) )
                    
                | DbConnectionSpecification.SqlServer(spec)  ->
                    DbConnector.Make( new SqlConnection( spec.Value ) )
                    
            let connection =
                DbConnection.Make( connector )
                
            logger.LogDebug( "DbConnectionFactory::Create - Opening connection" )               
            connection.Check()
            
            connections.Add( name, connection )
            
            connection
        
    member this.Lookup (name:string) =
        logger.LogDebug( "DbConnectionFactory::TryLookup - Called for {Name}", name )
        lock connections <| fun _ ->
            match connections.TryGetValue name with
            | true, c -> c
            | false, _ -> failwithf "Unable to find DbConnection called '%s'" name
            
        
    interface System.IDisposable
        with
            member this.Dispose () =
                this.Dispose()

    interface IDbConnectionFactory
        with
            member this.Create name spec =
                this.Create name spec 
                
            member this.Lookup name =
                this.Lookup name