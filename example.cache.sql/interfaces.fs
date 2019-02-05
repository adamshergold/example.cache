namespace Example.Cache.Sql

type SqliteSpecification = {
    ConnectionString : string 
}
with
    override this.ToString() =
        sprintf "Sqlite(%s)" this.ConnectionString 
        
    static member Default =
        { ConnectionString = "Data Source=:memory:" }

    static member Make( cs ) =
        { ConnectionString = cs }
        
type MySqlSpecification = {
    Server : string
    UserName : string
    Password : string
    Database : string
}
with
    override this.ToString() =
        sprintf "MySqlSpecification(Server=%s,UserName=%s,Password=len(%d),Database=%s" this.Server this.UserName this.Password.Length this.Database
        
    static member Make( server, user, password, db ) =
        { Server = server; UserName = user; Password = password; Database = db }
        
    static member MakeFromEnvironment (prefix:string) =
        {
            Server =
                Utilities.TryEnvironmentVariable "localhost" (sprintf "%s_SERVER" prefix )
                
            UserName =
                Utilities.TryEnvironmentVariable "guest" (sprintf "%s_USERNAME" prefix )

            Password =
                Utilities.TryEnvironmentVariable "guest" (sprintf "%s_PASSWORD" prefix )
                
            Database =
                Utilities.TryEnvironmentVariable "guestdb" (sprintf "%s_DATABASE" prefix )
        }
        

type DbConnectionSpecification =
    | Sqlite of SqliteSpecification
    | MySql of MySqlSpecification
    
type IDbConnection =
    inherit System.IDisposable
    abstract ConnectorType : string with get
    abstract ConnectionString : string with get
    abstract Check : unit -> unit
    abstract CreateCommand : text:string -> ps:seq<string*obj> -> System.Data.Common.DbCommand 
        
type IDbConnectionFactory =
    inherit System.IDisposable
    abstract Create : name:string -> DbConnectionSpecification -> IDbConnection
    abstract Lookup : name:string -> IDbConnection
    