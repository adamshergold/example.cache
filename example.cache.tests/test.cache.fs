namespace Example.Cache.Tests

open Microsoft.Extensions.Logging

open Xunit
open Xunit.Abstractions 

open Example.Cache
open Example.Cache.Core.Tests

type ImplementationCreator = {
    Name : string 
    Creator : ILogger -> string -> ITestCache
}
with 
    static member Make( name, creator ) = 
        { Name = name; Creator = creator }

    override this.ToString() = this.Name
    
type CacheTesting() =
    
    static member Implementations
        with get () =
            seq {
                yield [| ImplementationCreator.Make( "memory", (fun logger name -> Memory.Cache.Make( logger, name, Memory.Options.Default)) ) |]
            }
            
type CacheShould( oh: ITestOutputHelper ) = 

    let logger =
        Logging.CreateLogger oh

    let generator =
        Random.Generator.Make()
        
    [<Theory>]
    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
    member this.``BeCreateable`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name
            
        Assert.Equal( 0, sut.Keys().Length )
        Assert.True( sut.Name.Length > 0 )

//    [<Theory>]
//    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
//    member this.``CanCreateSetAndGet`` (creator:ImplementationCreator) =
//        
//        use sut =
//            creator.Creator logger creator.Name 
//        
//        let v = TestType.Random generator 
//        
//        sut.Set v.Id v
//        
//        let v' = sut.Get v.Id
//        
//        Assert.Equal( v', v )
//        
//        Assert.True( sut.Remove v.Id ) 
//        
//        Assert.ThrowsAny<System.Exception>( fun () -> sut.Get v.Id |> ignore ) |> ignore
//        
//        // we do not count a failed 'Get' - it won't fire
//        Assert.Equal( 1, sut.Statistics.Get )
//        Assert.Equal( 1, sut.Statistics.Set )
//        Assert.Equal( 1, sut.Statistics.Remove )
//        
//    [<Theory>]
//    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
//    member this.``ExpiryAndCleanWorksForSimpleCase`` (creator:ImplementationCreator) =
//        
//        use sut =
//            creator.Creator logger creator.Name 
//        
//        let v = TestType.Example
//        
//        sut.Set v.Id v
//        
//        sut.Clean() |> Async.RunSynchronously
//        
//        sut.Dispose()
//        
//        Assert.True( true )
//        
        
