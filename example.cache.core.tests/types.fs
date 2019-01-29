namespace Example.Cache.Tests

open Example.Serialisation

type Person = {
    Name : string
    DateOfBirth : System.DateTime
}
with
    static member Example =
        { Name = "John Smith"; DateOfBirth = System.DateTime.Today.AddYears(-21) }
        
    interface ITypeSerialisable
    
