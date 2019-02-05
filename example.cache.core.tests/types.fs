namespace Example.Cache.Core.Tests

open Example.Cache
open Example.Serialisation
open Example.Serialisation.Json
open NodaTime

type TestType = {
    Id : string
    DateOfBirth : LocalDate
}
with
    static member Random (generator:Random.Generator) =
        let rs = generator.NextString(32)
        let posNeg = if generator.NextBool() then -1.0 else 1.0
        let rd = LocalDate( generator.NextInt(1970,2000),generator.NextInt(1,12),generator.NextInt(1,28))
        { Id = rs; DateOfBirth = rd }
            
    static member Example =
        { Id = "John Smith"; DateOfBirth = LocalDate( 1975, 1, 1) }
       
    member this.Clone () =
        { Id = this.Id; DateOfBirth = this.DateOfBirth }
        
    interface ITypeSerialisable
    
    static member JSON_Serialiser
        with get () =
            { new ITypeSerde<TestType>
                with
                    member this.TypeName with get () = "TestType"
                    
                    member this.ContentType with get () = "json"
                    
                    member this.Serialise (serde:ISerde) (stream:ISerdeStream) (v:TestType) =
                        
                        use js = JsonSerialiser.Make( serde, stream, this.ContentType )
                        
                        js.WriteStartObject()

                        js.WriteProperty serde.Options.TypeProperty
                        js.WriteValue this.TypeName
                        
                        js.WriteProperty "Id"
                        js.WriteValue v.Id
                        
                        js.WriteProperty "DateOfBirth"
                        js.Serialise v.DateOfBirth
                        
                        js.WriteEndObject()

                        
                    member this.Deserialise (serde:ISerde) (stream:ISerdeStream) =
                        
                        use jds =
                            JsonDeserialiser.Make( serde, stream, this.ContentType, this.TypeName )
                            
                        jds.Handlers.On "Id" jds.ReadString
                        jds.Handlers.On "DateOfBirth" jds.ReadLocalDate
                            
                        jds.Deserialise()
                        
                        {
                            Id = jds.Handlers.TryItem<_>( "Id" ).Value
                            DateOfBirth = jds.Handlers.TryItem<_>( "DateOfBirth" ).Value
                        } 
                 }
            
type ITestCache = IEnumerableCache<TestType>
