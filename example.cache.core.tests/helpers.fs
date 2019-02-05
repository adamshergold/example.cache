namespace Example.Cache.Core.Tests 

open Example.Serialisation 
open Example.Serialisation.Json
open Example.Serialisation.Binary

module Helpers = 
    
    let ToChunks n (s:seq<'t>) = seq {
        let pos = ref 0
        let buffer = Array.zeroCreate<'t> n
    
        for x in s do
            buffer.[!pos] <- x
            if !pos = n - 1 then
                yield buffer |> Array.copy
                pos := 0
            else
                incr pos
    
        if !pos > 0 then
            yield Array.sub buffer 0 !pos
    }

    let Serde () =
    
        let options =   
            SerdeOptions.Default
         
        let serde = 
            Serde.Make( options )
            
        serde                 
        
    let DefaultSerde = 
        Serde() 
                
