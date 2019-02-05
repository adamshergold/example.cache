namespace Example.Cache.Core.Tests

open Xunit
open Xunit.Abstractions 

open Example.Cache.Core
                                            
type RVShould( oh: ITestOutputHelper ) = 

    let logger =
        Logging.CreateLogger oh

    [<Fact>]
    member this.``ConstantWorks`` () =
        
        let sut = RV.Constant<_>(42)
        
        Assert.False( sut.HasExpired )
        Assert.False( sut.NeedsRefresh )
        Assert.Equal( Some 42, sut.Value) 
    
