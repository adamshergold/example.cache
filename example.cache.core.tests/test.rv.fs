namespace Example.Cache.Tests

open Xunit
open Xunit.Abstractions 

open Example.Cache.Core
                                            
type RVShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options

    [<Fact>]
    member this.``ConstantWorks`` () =
        
        let sut = RV.Make<_>(42)
        
        Assert.False( sut.HasExpired )
        Assert.False( sut.NeedsRefresh )
        Assert.Equal( 42, sut.Value) 
    
