namespace Example.Cache

open Example.Serialisation

type ICache =
    inherit System.IDisposable
    
    abstract Id : string with get
    
    abstract Keys : unit -> string[]
    
    abstract Get : key:string -> ITypeSerialisable
    
    abstract Set : key:string -> ITypeSerialisable -> unit
    
    abstract Remove : key:string -> bool
    
    abstract Housekeep : unit -> Async<unit>
    
    [<CLIEvent>]
    abstract OnGet : IEvent<string>
    
    [<CLIEvent>]
    abstract OnSet : IEvent<string*ITypeSerialisable>

    [<CLIEvent>]
    abstract OnRemove : IEvent<string>
    
    
         
    