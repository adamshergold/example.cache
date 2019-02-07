namespace Example.Cache.Memory

open Example.Cache

type Specification = {
    InitialCapacity : int option
    MaxSize : int option
    TimeToLiveSeconds : int option    
}
with
    static member Default = {
        InitialCapacity = None
        MaxSize = None
        TimeToLiveSeconds = None
    }

    interface ICacheSpecification
       
    