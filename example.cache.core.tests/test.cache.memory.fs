namespace Example.Cache.Tests

open Xunit
open Xunit.Abstractions 

open Example.Cache.Core
                                            
type MemoryShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options

    [<Fact>]
    member this.``BeCreateable`` () =
        
        use sut =
            
            let options =
                MemoryCacheOptions.Default
            
            MemoryCache.Make( logger, "test", options )
            
        Assert.True( true )        

    [<Fact>]
    member this.``CanCreateSetAndGet`` () =
        
        use sut =
            
            let options =
                MemoryCacheOptions.Default
            
            MemoryCache.Make( logger, "test", options )
        
        let nGet = ref 0
        let nSet = ref 0
        let nRemoved = ref 0
        
        sut.OnSet.Add( fun (k,v_) -> System.Threading.Interlocked.Increment( nSet ) |> ignore )
        sut.OnGet.Add( fun k -> System.Threading.Interlocked.Increment( nGet ) |> ignore )
        sut.OnRemove.Add( fun k -> System.Threading.Interlocked.Increment( nRemoved ) |> ignore )
        
        let v = Person.Example
        
        sut.Set v.Name v
        
        let v' = sut.Get v.Name
        
        Assert.Equal( v', v )
        
        Assert.True( sut.Remove v.Name ) 
        
        Assert.ThrowsAny<System.Exception>( fun () -> sut.Get v.Name |> ignore ) |> ignore
        
        // we do not count a failed 'Get' - it won't fire
        Assert.Equal( 1, !nGet )
        Assert.Equal( 1, !nSet )
        Assert.Equal( 1, !nRemoved )
        
    [<Fact>]
    member this.``HousekeepingWorks`` () =
        
        let sut =
            
            let options =
                MemoryCacheOptions.Default
            
            MemoryCache.Make( logger, "test", options )
        
        let v = Person.Example
        
        sut.Set v.Name v
        
        sut.Housekeep() |> Async.RunSynchronously
        
        sut.Dispose()
        
        Assert.True( true )
        
        
