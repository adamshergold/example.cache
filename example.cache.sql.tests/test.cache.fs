namespace Example.Cache.Sql.Tests

open Microsoft.Extensions.Logging

open Xunit
open Xunit.Abstractions

open Example.Sql

open Example.Cache
open Example.Cache.Sql
open Example.Cache.Core.Tests

type CacheShould( oh: ITestOutputHelper ) =
    
    let logger =
        Logging.CreateLogger oh
    
    let metrics =
        Metrics.CreateMetrics()
        
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
                //[| box <| DbConnectionSpecification.SqlServer( SqlServerSpecification.Make( "Server=tcp:server-example-20192.database.windows.net,1433;Initial Catalog=example;Persist Security Info=False;User ID=example-admin;Password=;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;") ) |]
            |]
       
    interface System.IDisposable
        with
            member this.Dispose () =
                factory.Dispose()
                
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``VolumeTest`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec 
       
        let cacheName =
            "VolumeTest"
            
        let sut =
            let options = Sql.Specification.Default
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )

        sut.Purge() |> ignore
        
        let nItems = 100000
        
        let batchSize =
            if connection.ConnectorType.Equals("sqlite",System.StringComparison.OrdinalIgnoreCase) then 250 else 1000
            
        let items =
            Seq.init nItems ( fun idx ->
                let tt = TestType.RandomWithId generator idx
                tt.Id, tt ) |> Array.ofSeq
        
        let batches =
            Array.chunkBySize batchSize items
            
        batches |> Seq.iteri ( fun idx batch ->
            logger.LogInformation( "Processing batch {BatchIndex} with {nItems} items", idx, batch.Length )
            sut.SetKeys batch )
        
        Metrics.DumpMetrics metrics logger
        
        Assert.True( true )
        
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``BeCreateableAndReportEmpty`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "BeCreateableAndReportEmpty"
            
        let sut =
            let options = Sql.Specification.Default
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )
            
        sut.Purge() |> ignore
        
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
            let options = Sql.Specification.Default
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )
            
        sut.Purge() |> ignore
        
        let tt =
            TestType.Random generator
            
        sut.SetKeys <| Array.singleton (tt.Id,tt)
        
        Assert.True( sut.Exists tt.Id )
        
        let vs = sut.TryGetKeys [| tt.Id |]
        
        Assert.True( vs.Length = 1 )
        Assert.True( vs.[0].IsSome )
        
        Assert.Equal( tt, vs.[0].Value )
        
        Assert.True( sut.Remove tt.Id )
        
        let vs = sut.TryGetKeys [| tt.Id |]
        
        Assert.True( vs.Length = 1 )
        Assert.True( vs.[0].IsNone )
        
        Assert.Equal( 0, sut.Purge() )
        
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``AllowSimpleSetWithExpiry`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "AllowSimpleSetWIthExpiry"
            
        let sut =
            let options =
                { Sql.Specification.Default with TimeToLiveSeconds = Some 1 }
                
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )
            
        sut.Purge() |> ignore
        
        let tt =
            TestType.Random generator
            
        sut.SetKeys <| Array.singleton (tt.Id,tt)

        // sleep well over the expiry time
        System.Threading.Thread.Sleep( 2000 )
        
        sut.Clean() |> Async.RunSynchronously
        
        // should have been tidied-up!
        let vs = sut.TryGetKeys [| tt.Id |]
        
        Assert.True( vs.Length = 1 )
        Assert.True( vs.[0].IsNone )
    
    
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``InsertManyAndGetAsync`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "InsertManyAndGetAsync"
            
        let testItems =
            Array.init (generator.NextInt(100,500)) ( fun idx -> TestType.RandomWithId generator idx )
            
        let sut =
            let options =
                Sql.Specification.Default
                
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )

        sut.Purge() |> ignore
        
        testItems |> Array.map ( fun tt -> (tt.Id,tt) ) |> sut.SetKeys

        let ids =
            Seq.initInfinite ( fun _ -> testItems.[ generator.NextInt(0,testItems.Length-1) ].Id ) |> Seq.truncate (testItems.Length/2) |> Array.ofSeq
            
        let results =
            sut.TryGetKeys ids 
            
        Assert.Equal( ids.Length, results.Length )
        
        
        
                