namespace Example.Cache.Core

open Example.Cache

type Statistics() =
    
    let get = ref 0
    
    let set = ref 0
    
    let hit = ref 0
    
    let contains = ref 0
    
    let remove = ref 0
    
    static member Make() =
        new Statistics()
        
    member this.Get () =
        System.Threading.Interlocked.Increment( get ) |> ignore

    member this.Set () =
        System.Threading.Interlocked.Increment( set ) |> ignore

    member this.Hit () =
        System.Threading.Interlocked.Increment( hit ) |> ignore
    
    member this.Contains () =
        System.Threading.Interlocked.Increment( contains ) |> ignore

    member this.Remove () =
        System.Threading.Interlocked.Increment( remove ) |> ignore
                    
    interface IStatistics
        with
            member this.Get with get () = !get
            
            member this.Set with get () = !set
            
            member this.Hit with get () = !hit
            
            member this.Contains with get () = !contains
            
            member this.Remove with get () = !remove
            
                                                                          
                                                                          
    