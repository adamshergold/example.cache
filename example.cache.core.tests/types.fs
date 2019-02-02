namespace Example.Cache.Core.Tests

open Example.Cache

type TestType = {
    Id : string
    DateOfBirth : System.DateTime
}
with
    static member Random (generator:Random.Generator) =
        let rs = generator.NextString(32)
        let posNeg = if generator.NextBool() then -1.0 else 1.0
        let rd = System.DateTime.Today.AddDays( posNeg * (float)( generator.NextInt(0,3650) ) )
        { Id = rs; DateOfBirth = rd }
            
    static member Example =
        { Id = "John Smith"; DateOfBirth = System.DateTime.Today.AddYears(-21) }
        
type ITestCache = IEnumerableCache<string,TestType>
