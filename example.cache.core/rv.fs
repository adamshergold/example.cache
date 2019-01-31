namespace Example.Cache.Core

type RV<'T>( fn:unit->'T, refreshIntervalMs:int option, ttlMs: int option ) =
    
    let nextRefresh (t:System.DateTime) =
        refreshIntervalMs |> Option.map ( fun ms -> t.AddMilliseconds((float)ms))
        
    let expiresAt (t:System.DateTime) =
        ttlMs |> Option.map ( fun ms -> t.AddMilliseconds((float)ms))
        
    let mutable _v = fn()
    
    let mutable _nextRefresh =
        nextRefresh System.DateTime.UtcNow
        
    let mutable _expiresAt =
        expiresAt System.DateTime.UtcNow
        
    static member Make<'T>( fn, refresh, ttl ) =
        new RV<'T>( fn, refresh, ttl )
        
    static member Make<'T>( v:'T ) =
        new RV<'T>( (fun _ -> v), None, None )
        
    member this.HasExpired
        with get () =
            _expiresAt.IsSome && _expiresAt.Value < System.DateTime.UtcNow 
            
    member this.NeedsRefresh
        with get () =
            _nextRefresh.IsSome && _nextRefresh.Value < System.DateTime.UtcNow
            
    member this.Refresh () =
        lock this <| fun _ ->
            _v <- fn()
            _nextRefresh <- nextRefresh System.DateTime.UtcNow
            _expiresAt <- expiresAt System.DateTime.UtcNow
            
    member this.Value
        with get () =
            if this.HasExpired then
                failwithf "Unable to obtain expired value!"
            else
                if this.NeedsRefresh then
                    this.Refresh()
                
                _v    
                    