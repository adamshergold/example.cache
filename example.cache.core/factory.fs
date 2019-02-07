namespace Example.Cache.Core

open Microsoft.Extensions.Logging

open Example.Cache

//type Factory( logger: ILogger ) =
//    
//    let creators =
//        new System.Collections.Generic.List<ICacheCreator>()
//        
//    static member Make( logger ) =
//        new Factory( logger ) :> ICacheFactory
//        
//    member this.Register creator =
//        creators.Add( creator )
//        this :> ICacheFactory
//        
//    member this.TryCreate (name:string) (spec:ICacheSpecification) =
//        creators |> Seq.tryPick ( fun creator -> creator.TryCreate logger name spec  )
//        
//    interface ICacheFactory
//        with
//            member this.Register creator =
//                this.Register creator
//                
//            member this.TryCreate name spec =
//                this.TryCreate name spec