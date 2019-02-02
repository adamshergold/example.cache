namespace Example.Cache

//open Example.Serialisation

type IStatistics =
    abstract Get : int with get
    abstract Set : int with get
    abstract Hit : int with get
    abstract Contains : int with get
    abstract Remove : int with get

type ICacheOptions =
    interface end
    
type ICache<'K,'V> =
    inherit System.IDisposable
    
    abstract Name : string with get
    
    abstract Clean : unit -> Async<unit>
    
    abstract Exists : key:'K -> bool
    
    abstract Get : key:'K -> 'V
    
    abstract Set : key:'K -> 'V -> unit
    
    abstract Remove : key:'K -> bool
    
    abstract Statistics : IStatistics with get
    
    [<CLIEvent>]
    abstract OnGet : IEvent<'K>
    
    [<CLIEvent>]
    abstract OnSet : IEvent<'K>

    [<CLIEvent>]
    abstract OnRemove : IEvent<'K>

    [<CLIEvent>]
    abstract OnEvicted : IEvent<'K>

type IEnumerableCache<'K,'V> =
    inherit ICache<'K,'V>
    
    abstract Keys : unit -> 'K[]
    
    abstract Count : int with get
    


         
    