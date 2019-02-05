namespace Example.Cache.Memory.Tests

open Microsoft.Extensions.Logging

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
            Memory.Cache<TestType>.Make( logger, "test", options )
            
        Assert.Equal( 0, sut.Keys().Length )
        
        Assert.Equal( 0, sut.Statistics.Get )
        Assert.Equal( 0, sut.Statistics.Set )
        Assert.Equal( 0, sut.Statistics.Remove )
        Assert.Equal( 0, sut.Statistics.Hit )
        
    [<Theory>]
    [<InlineData(4,5000,5000)>]
    [<InlineData(4,1000,2000)>]
    [<InlineData(4,1000,1000)>]
    [<InlineData(4,1000,100)>]
    [<InlineData(2,1000,100)>]
    [<InlineData(16,1000,1)>]
    [<InlineData(4,1000,10)>]
    [<InlineData(4,100,1)>]
    member this.``WorkSuccessfullyWithMultiReaderAndWritersAndVaryingCapacityAndSize`` (nParallel:int) (nItems:int) (capacity:int) =
        
        let sut =
            let options = { Memory.Options.Default with MaxSize = Some( capacity ) }
            
            Memory.Cache<TestType>.Make( logger, "test", options )

        let testItems =
            Seq.initInfinite ( fun _ -> TestType.Random generator )
            |> Seq.distinctBy ( fun tt -> tt.Id )
            |> Seq.truncate nItems
            |> Array.ofSeq

        let singleWork (idx:int) =
            async {
                
                let idsThatWereSet =                 
                    Array.init nItems <| fun idx -> 
                        let itemToPut = testItems.[ generator.NextInt( 0, testItems.Length-1) ]
                        sut.Set itemToPut.Id itemToPut
                        itemToPut.Id
                    
                idsThatWereSet |> Array.iter ( fun id -> 
                    sut.TryGet id |> ignore )
            }                     
        
        let work =
            Array.init nParallel singleWork
            
        let evictions = ref 0             
        sut.OnEvicted.Add( fun k -> System.Threading.Interlocked.Increment( evictions) |> ignore )
        
        work |> Async.Parallel |> Async.RunSynchronously |> ignore
        
        logger.LogInformation( "{Statistics}", sut.Statistics )
        
        Assert.Equal( nItems * nParallel, sut.Statistics.Get )            
        Assert.Equal( nItems * nParallel, sut.Statistics.Set )
        
        // number of items in the cache should be limited by capacity 
        //Assert.Equal( min nItems capacity, sut.Count )

        // number of evictions + count = nItems?
//        Assert.Equal( nItems, !evictions + sut.Count )
//        
//        Assert.Equal( nItems, sut.Statistics.Get )
//        Assert.Equal( 0, sut.Statistics.Remove )
        
        
