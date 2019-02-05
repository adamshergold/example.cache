namespace Example.Cache.Core.Tests 

open Example.Serialisation 
open Example.Serialisation.Json
open Example.Serialisation.Binary

module Helpers = 
    
    let Serde () =
    
        let options =   
            SerdeOptions.Default
         
        let serde = 
            Serde.Make( options )
            
        serde                 
        
    let DefaultSerde = 
        Serde() 
                
