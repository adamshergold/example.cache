namespace Example.Cache.Core.Tests

module Random  =

    type Generator( seed:int32 ) =
    
        let rnd = new System.Random(seed) 
        
        let strChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"
        
        static member Make( seed ) = 
            new Generator( seed )
            
        static member Make() = 
            new Generator( System.DateTime.UtcNow.Millisecond )
        
        member this.NextInt (min:int,max:int) = 
            rnd.Next(min,max+1)
              
        member this.NextChar () = 
            let idx = this.NextInt (0,strChars.Length-1)
            strChars.[idx] 
        
        member this.NextDouble (min,max) = 
            min + ( max - min ) * rnd.NextDouble()
                    
        member this.NextBool () =
            if rnd.NextDouble() < 0.5 then false else true 
                                                      
        member this.NextString (maxLength:int) = 
            let len = this.NextInt(0,maxLength)
            let chars = seq { 0 .. len-1 } |> Seq.map ( fun _ -> this.NextChar() ) |> Array.ofSeq
            new string(chars)                                             
            
                                        
    