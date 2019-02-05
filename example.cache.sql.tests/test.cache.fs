namespace Example.Cache.Sql.Tests

open Xunit
open Xunit.Abstractions

open Example.Cache
open Example.Cache.Sql
open Example.Cache.Core.Tests
open Microsoft.Data.Sqlite

type CacheShould( oh: ITestOutputHelper ) =
    
    let logger =
        Logging.CreateLogger oh
        
    let generator =
        Random.Generator.Make()
        
    let serde =
        let serde = Helpers.Serde()
        serde.TryRegister TestType.JSON_Serialiser |> ignore
        serde
        
    let factory =
        DbConnectionFactory.Make( logger )
        
    static member ConnectionSpecs
        with get () =
            [|
                [| box <| DbConnectionSpecification.Sqlite( SqliteSpecification.Default ) |]
                //[| box <| DbConnectionSpecification.MySql( MySqlSpecification.Make( "localhost", "example", "example-pw", "example" ) ) |]
            |]
       
    interface System.IDisposable
        with
            member this.Dispose () =
                factory.Dispose()
                
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``BeCreateableAndReportEmpty`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "BeCreateableAndReportEmpty"
            
        let sut =
            let options = Sql.Options.Default
            Sql.Cache<TestType>.Make( logger, cacheName, serde, connection, options )
            
        Assert.Equal( 0, sut.Keys().Length )
        
        Assert.Equal( 0, sut.Statistics.Get )
        Assert.Equal( 0, sut.Statistics.Set )
        Assert.Equal( 0, sut.Statistics.Remove )
        Assert.Equal( 0, sut.Statistics.Hit )
        
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``AllowSimpleSetAndGetAndRemove`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "AllowSimpleSetAndGetAndRemove"
            
        let sut =
            let options = Sql.Options.Default
            Sql.Cache<TestType>.Make( logger, cacheName, serde, connection, options )
            
        let tt =
            TestType.Random generator
            
        sut.Set tt.Id tt
        
        Assert.True( sut.Exists tt.Id )
        
        let v = sut.TryGet tt.Id
        
        Assert.True( v.IsSome )
        
        Assert.Equal( tt, v.Value )
        
        Assert.True( sut.Remove tt.Id )
        
        Assert.True( (sut.TryGet tt.Id).IsNone )
        
        Assert.Equal( 0, sut.Purge() )
        
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``AllowSimpleSetWIthExpiry`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "AllowSimpleSetWIthExpiry"
            
        let sut =
            let options =
                { Sql.Options.Default with TimeToLiveSeconds = Some 1 }
                
            Sql.Cache<TestType>.Make( logger, cacheName, serde, connection, options )
            
        let tt =
            TestType.Random generator
            
        sut.Set tt.Id tt

        // just over 1 second
        System.Threading.Thread.Sleep( 1300 )
        
        sut.Clean() |> Async.RunSynchronously
        
        // should have been tidied-up!
        let v = sut.TryGet tt.Id
        
        Assert.True( v.IsNone )
    
                