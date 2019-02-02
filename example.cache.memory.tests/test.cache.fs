namespace Example.Cache.Memory.Tests

open Xunit
open Xunit.Abstractions

open Example.Cache
open Example.Cache.Core.Tests

type CacheShould( oh: ITestOutputHelper ) =
    
    let logger =
        Logging.CreateLogger oh
        
    let generator =
        Random.Generator.Make()
        
    [<Fact>]
    member this.``BeCreateableAndReportEmpty`` () =
        
        let sut =
            let options = Memory.Options.Default
            Memory.Cache<string,TestType>.Make( logger, "test", options )
            
        Assert.Equal( 0, sut.Keys().Length )
        
        Assert.Equal( 0, sut.Statistics.Get )
        Assert.Equal( 0, sut.Statistics.Set )
        Assert.Equal( 0, sut.Statistics.Remove )
        Assert.Equal( 0, sut.Statistics.Hit )
        
    [<Fact>]
    member this.``OperatesWithNoEvictionsWhenSizeIsLargeEnough`` () =
        
        let maxItems =
            1000
            
        let sut =
            let options = { Memory.Options.Default with MaxSize = Some( (int)( (float)maxItems * 1.2 ) ) }
            
            Memory.Cache<string,TestType>.Make( logger, "test", options )
            
        let items =
            Seq.init maxItems ( fun _ -> TestType.Random generator ) |> Seq.distinctBy ( fun tt -> tt.Id ) |> Array.ofSeq

        let nItems = items.Length

        let evictions = ref 0             
        sut.OnEvicted.Add( fun k -> System.Threading.Interlocked.Increment( evictions) |> ignore )

        items |> Seq.iter ( fun (t:TestType) ->
                sut.Set t.Id t )
            
        Assert.Equal( 0, sut.Statistics.Get )            
        Assert.Equal( nItems, sut.Statistics.Set )
        Assert.Equal( nItems, sut.Count )

        items                    
        |> Seq.iter ( fun (t:TestType) ->
            sut.Get t.Id |> ignore )
        
        Assert.Equal( nItems, sut.Statistics.Get )
        Assert.Equal( 0, sut.Statistics.Remove )
        Assert.Equal( 0, !evictions )
        

    [<Fact>]
    member this.``OperatesWithEvictionsWhenSizeIsConstrained`` () =
        
        let cacheSize = 10
        
        let maxItems =
            100
            
        let sut =
            let options = { Memory.Options.Default with MaxSize = Some cacheSize }
            
            Memory.Cache<string,TestType>.Make( logger, "test", options )
            
        let items =
            Seq.init maxItems ( fun _ -> TestType.Random generator ) |> Seq.distinctBy ( fun tt -> tt.Id ) |> Array.ofSeq

        let nItems = items.Length

        let evictions = ref 0             
        sut.OnEvicted.Add( fun k -> System.Threading.Interlocked.Increment( evictions) |> ignore )

        items |> Seq.iter ( fun (t:TestType) ->
                sut.Set t.Id t )
            
        Assert.Equal( 0, sut.Statistics.Get )            
        Assert.Equal( nItems, sut.Statistics.Set )
        Assert.True( sut.Count <= cacheSize )

        let nContains =
            items                    
            |> Seq.fold ( fun acc (t:TestType) ->
                acc + if sut.Exists t.Id then 1 else 0 ) 0
        
        Assert.Equal( cacheSize, nContains )
        Assert.Equal( nItems - cacheSize, !evictions )
