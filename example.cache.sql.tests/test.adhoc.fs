namespace Example.Cache.Sql.Tests

open Microsoft.Extensions.Logging

open Xunit
open Xunit.Abstractions

open Example.Cache.Core.Tests

type AdhocShould( oh: ITestOutputHelper ) =
    
    let logger =
        Logging.CreateLogger oh

    interface System.IDisposable
        with
            member this.Dispose () =
                ()
    
    [<Fact>]
    member this.``Sequence`` () =

        let result n =
            Seq.init n ( fun idx ->
                let result = sprintf "item %d" idx
                logger.LogInformation( "Result {n} called", idx )
                idx, result ) |> Array.ofSeq
            
        let v1 =
            result 10
            
        v1 |> Seq.iter ( fun v -> () )
        
        let m =
            v1 |> Map.ofSeq
            
        Assert.True( true )
        
                