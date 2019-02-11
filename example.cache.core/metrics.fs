namespace Example.Cache.Core

open App.Metrics

module Metrics =

    let CreateTimer (name:string) (context:string option) =
    
        let options = 
            new Timer.TimerOptions()
            
        options.Name <- name
        options.Context <- if context.IsSome then context.Value else options.Context  
        options.MeasurementUnit <- Unit.Requests
        options.DurationUnit <- TimeUnit.Milliseconds
        options.RateUnit <- TimeUnit.Milliseconds
        
        options     