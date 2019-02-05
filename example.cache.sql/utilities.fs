namespace Example.Cache.Sql

module Utilities =
    
    let TryEnvironmentVariable (defaultTo:string) (name:string) =
        
        let ev =
            System.Environment.GetEnvironmentVariable(name)
        
        if System.String.IsNullOrWhiteSpace( ev ) then defaultTo else ev     
