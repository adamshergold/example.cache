namespace Example.Cache.Sql

open Example.Cache

type Options = {
    TimeToLiveSeconds : int option
    ContentType : string
    InlineRefeshIntervalSeconds : int
    InlineTimeToLiveSeconds : int
    InlineCacheMaxSize : int
}
with
    static member Default = {
        TimeToLiveSeconds = None
        ContentType = "json"
        InlineRefeshIntervalSeconds = 1
        InlineTimeToLiveSeconds = 10
        InlineCacheMaxSize = 1024
    }
    
    interface ICacheOptions