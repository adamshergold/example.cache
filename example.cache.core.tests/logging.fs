namespace Example.Cache.Core.Tests

open Microsoft.Extensions.Logging 

open Xunit.Abstractions 

open Serilog 

module Logging =

    type Options = {
        Level : Microsoft.Extensions.Logging.LogLevel
        OutputHelper : ITestOutputHelper option
        Template : string
    }
    with 
        static member Default = {
            Level = LogLevel.Trace
            OutputHelper = None
            Template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message} {Properties}{NewLine}{Exception}"
        }

    let ParseLevel (ll:Microsoft.Extensions.Logging.LogLevel) = 
        match ll with  
        | Microsoft.Extensions.Logging.LogLevel.Trace -> Serilog.Events.LogEventLevel.Verbose
        | Microsoft.Extensions.Logging.LogLevel.Debug -> Serilog.Events.LogEventLevel.Debug
        | Microsoft.Extensions.Logging.LogLevel.Information -> Serilog.Events.LogEventLevel.Information
        | Microsoft.Extensions.Logging.LogLevel.Warning -> Serilog.Events.LogEventLevel.Warning
        | Microsoft.Extensions.Logging.LogLevel.Error -> Serilog.Events.LogEventLevel.Error
        | Microsoft.Extensions.Logging.LogLevel.Critical -> Serilog.Events.LogEventLevel.Fatal
        | _ -> Serilog.Events.LogEventLevel.Information 
        
    
    let CreateLogger (oh:ITestOutputHelper) =
        
        let options = {
            Options.Default
                with
                    OutputHelper = Some oh
        }

        let levelSwitch = 
            Serilog.Core.LoggingLevelSwitch() 

        levelSwitch.MinimumLevel <- options.Level |> ParseLevel
        
        let config =
            Serilog.LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                
        let config =
            config.WriteTo.TestOutput(
                oh, levelSwitch.MinimumLevel, options.Template )
            
        let lf = 
            new LoggerFactory()
                
        lf.AddSerilog( config.CreateLogger() ) |> ignore   
        
        lf.CreateLogger("Cache.Tests")

