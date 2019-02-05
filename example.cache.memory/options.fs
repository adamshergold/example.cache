namespace Example.Cache.Memory

open Example.Cache

type Options = {
    InitialCapacity : int option
    MaxSize : int option
    TimeToLiveSeconds : int option
}
with
    static member Default = {
        InitialCapacity = None
        MaxSize = None
        TimeToLiveSeconds = Some 60
    }
    
    interface ICacheOptions