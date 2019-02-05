namespace Example.Cache.Core.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Cache
                                            
type InterfacesShould( oh: ITestOutputHelper ) = 

    let logger =
        Logging.CreateLogger oh
           

             