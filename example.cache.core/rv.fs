namespace Example.Cache.Core

type RV<'T>( initialV: 'T option, fn:unit->'T option, refreshIntervalMs:int option, ttlMs: int option ) =
    
    let nextRefresh (t:System.DateTime) =
        refreshIntervalMs |> Option.map ( fun ms -> t.AddMilliseconds((float)ms))
        
    let expiresAt (t:System.DateTime) =
        ttlMs |> Option.map ( fun ms -> t.AddMilliseconds((float)ms))
        
    let mutable _v =
        if initialV.IsSome then initialV else fn()
    
    let mutable _nextRefresh =
        nextRefresh System.DateTime.UtcNow
        
    let mutable _expiresAt =
        expiresAt System.DateTime.UtcNow
        
    static member Make<'T>( iv, fn, refresh, ttl ) =
        new RV<'T>( iv, fn, refresh, ttl )
        
    static member Constant<'T>( v:'T ) =
        new RV<'T>( None, (fun _ -> Some v), None, None )
        
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
                None
            else
                if this.NeedsRefresh then
                    this.Refresh()
                
                _v    
                    