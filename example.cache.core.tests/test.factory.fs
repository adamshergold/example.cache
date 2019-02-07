namespace Example.Cache.Core.Tests

open Xunit
open Xunit.Abstractions 

open Example.Cache
//                                            
//type FactoryShould( oh: ITestOutputHelper ) = 
//
//    let logger =
//        Logging.CreateLogger oh
//
//    [<Fact>]
//    member this.``CreateMemoryCache`` () =
//        
//        let sut = Core.Factory.Make( logger )
//        
//        sut.Register Memory.Creator.Value |> ignore
//        
//        let v = sut.TryCreate "test" Memory.Specification.Default
//        
//        Assert.True( false )
