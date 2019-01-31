namespace Example.Cache

open Example.Serialisation

type ICache =
    inherit System.IDisposable
    
    abstract Name : string with get
    
    abstract Clean : unit -> Async<unit>
    
    abstract Get : key:string -> ITypeSerialisable
    
    abstract Set : key:string -> ITypeSerialisable -> unit
    
    abstract Remove : key:string -> bool
    
    [<CLIEvent>]
    abstract OnGet : IEvent<string>
    
    [<CLIEvent>]
    abstract OnSet : IEvent<string*ITypeSerialisable>

    [<CLIEvent>]
    abstract OnRemove : IEvent<string>
    
type IEnumerableCache =
    inherit ICache
    
    abstract Keys : unit -> string[]


         
    