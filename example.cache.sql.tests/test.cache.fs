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
                //[| box <| DbConnectionSpecification.SqlServer( SqlServerSpecification.Make( "Server=tcp:server-example-20192.database.windows.net,1433;Initial Catalog=example;Persist Security Info=False;User ID=example-admin;Password=zkZJkKLxZ7o8;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;") ) |]
            |]
       
    interface System.IDisposable
        with
            member this.Dispose () =
                factory.Dispose()
                
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``VolumeTest-SetThenGet`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec 
       
        let cacheName =
            "VolumeTest"
            
        let sut =
            let options = Sql.Specification.Default
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )

        sut.Purge() |> ignore
        
        let nItems = 10000
        
        let batchSize =
            if connection.ConnectorType.Equals("sqlite",System.StringComparison.OrdinalIgnoreCase) then 250 else 1000
            
        let items =
            Seq.init nItems ( fun idx ->
                let tt = TestType.RandomWithId generator idx
                tt.Id, tt ) |> Array.ofSeq
        
        let batches =
            Array.chunkBySize batchSize items
            
        let elapsed, nFound =
            Helpers.Time <| fun _ ->
                batches |> Seq.iteri ( fun idx batch ->
                    sut.Set batch )
        
        logger.LogInformation( "Batch Set took {Milliseconds}ms", elapsed )
        
        Metrics.DumpMetrics metrics logger

        let elapsed, nFound =
            Helpers.Time <| fun _ ->
                batches
                |> Seq.fold ( fun acc batch ->
                    let keys = batch |> Array.map fst
                    let result = sut.TryGet keys
                    let nFound = result |> Seq.sumBy ( fun r -> if r.IsSome then 1 else 0 ) 
                    acc + nFound ) 0
            
        logger.LogInformation( "TryGet took {Milliseconds}ms", elapsed )
        
        Assert.Equal( nItems, nFound )
                
//        let work () =
//            batches |> Seq.map ( fun batch ->
//                async {
//                    let keys = batch |> Array.map fst
//                    let! result = sut.TryGetAsync keys
//                    let nFound = result |> Seq.sumBy ( fun r -> if r.IsSome then 1 else 0 ) 
//                    return nFound 
//                } )
//                
//        let elapsed, nFound =
//            Helpers.Time <| fun _ ->
//                let result = work() |> Async.Parallel |> Async.RunSynchronously
//                result |> Seq.sum
//            
//        logger.LogInformation( "TryGetAsync (Parallel) took {Milliseconds}ms", elapsed )
//        
//        Assert.Equal( nItems, nFound )

    
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
            
        sut.Set <| Array.singleton (tt.Id,tt)
        
        Assert.True( sut.Exists tt.Id )
        
        let vs = sut.TryGet [| tt.Id |]
        
        Assert.True( vs.Length = 1 )
        Assert.True( vs.[0].IsSome )
        
        Assert.Equal( tt, snd vs.[0].Value )
        
        Assert.Equal( 1, sut.Remove [| tt.Id |] )
        
        let vs = sut.TryGet [| tt.Id |]
        
        Assert.True( vs.Length = 1 )
        Assert.True( vs.[0].IsNone )
        
        Assert.Equal( 0, sut.Purge() )
        
    [<Theory>]
    [<MemberData("ConnectionSpecs")>]
    member this.``AllowSimpleSetWithExpiry`` (spec:DbConnectionSpecification) =
        
        use connection =
            factory.Create "test" spec
            
        let cacheName =
            "AllowSimpleSetWithExpiry"
            
        let sut =
            let options =
                { Sql.Specification.Default with TimeToLiveSeconds = Some 1 }
                
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )
            
        sut.Purge() |> ignore
        
        let tt =
            TestType.Random generator
            
        sut.Set <| Array.singleton (tt.Id,tt)

        // sleep well over the expiry time
        System.Threading.Thread.Sleep( 2000 )
        
        sut.Clean() |> Async.RunSynchronously
        
        // should have been tidied-up!
        let vs = sut.TryGet [| tt.Id |]
        
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
            Array.init (generator.NextInt(10,200)) ( fun idx -> TestType.RandomWithId generator idx )
        
        let keys =
            testItems |> Array.map ( fun tt -> tt.Id )
                    
        let sut =
            let options =
                Sql.Specification.Default
                
            Sql.Cache<TestType>.Make( logger, cacheName, metrics, serde, connection, options )

        // First do Async run 
        sut.Purge() |> ignore
        testItems |> Array.map ( fun tt -> (tt.Id,tt) ) |> sut.Set

        let elapsed, results =
            Helpers.Time <| fun _ ->
                sut.TryGetAsync keys |> Async.RunSynchronously
                
        logger.LogInformation( "TryGetAsync took {Milliseconds}ms to get {nKeys} keys", elapsed, keys.Length )                

        Assert.Equal( keys.Length, results.Length )
        
        
        // Now try with Sync
        sut.Purge() |> ignore
        testItems |> Array.map ( fun tt -> (tt.Id,tt) ) |> sut.Set
        
        let elapsed, results =
            Helpers.Time <| fun _ ->
                sut.TryGet keys 
                
        logger.LogInformation( "TryGet took {Milliseconds}ms to get {nKeys} keys", elapsed, keys.Length )                
            
        Assert.Equal( keys.Length, results.Length )
        
        
        
                