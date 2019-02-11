namespace Example.Cache

open Microsoft.Extensions.Logging

type IStatistics =
    abstract Get : int with get
    abstract Set : int with get
    abstract Hit : int with get
    abstract Contains : int with get
    abstract Remove : int with get

type ICacheOptions =
    interface end
    
type ICache<'V> =
    inherit System.IDisposable
    
    abstract Name : string with get
    
    abstract Purge : unit -> int
    
    abstract Clean : unit -> Async<unit>
    
    abstract Exists : key:string -> bool
    
    abstract TryGet : keys:string[] -> ('V option)[]
    
    abstract Set : (string*'V)[] -> unit
    
    abstract Remove : string[] -> int
    
    abstract Statistics : IStatistics with get
    
    [<CLIEvent>]
    abstract OnGet : IEvent<string>
    
    [<CLIEvent>]
    abstract OnSet : IEvent<string>

    [<CLIEvent>]
    abstract OnRemove : IEvent<string>

    [<CLIEvent>]
    abstract OnEvicted : IEvent<string>

type IEnumerableCache<'V> =
    inherit ICache<'V>
    
    abstract Keys : unit -> string[]
    
    abstract Count : int with get
    
type ICacheSpecification = interface end
    