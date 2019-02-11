namespace Example.Cache.Core.Tests

open Microsoft.Extensions.Logging

open App.Metrics

module Metrics =

    let CreateMetrics () = 
    
        let builder = 
            new MetricsBuilder()
            
        let builder = 
            builder.OutputMetrics.AsJson()
                        
        let mOptions = 
            MetricsOptions()
                   
        mOptions.Enabled <- true 
        mOptions.ReportingEnabled <- true 
                
        builder.Configuration.Configure(mOptions).Build()
        
    let DumpMetrics (metrics:IMetrics) (logger:ILogger) =
        
        let root = metrics :?> IMetricsRoot
        
        let snapshot = metrics.Snapshot.Get();

        root.OutputMetricsFormatters |> Seq.iter ( fun formatter ->
            
            use ms = new System.IO.MemoryStream()
            
            formatter.WriteAsync( ms, snapshot ) |> Async.AwaitTask |> ignore
            
            let text = ms.ToArray() |> System.Text.Encoding.UTF8.GetString
            
            logger.LogInformation( "{Metrics}", text ) )
