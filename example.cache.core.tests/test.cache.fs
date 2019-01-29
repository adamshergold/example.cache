namespace Example.Cache.Tests

open Microsoft.Extensions.Logging

open Xunit
open Xunit.Abstractions 

open Example.Cache
open Example.Cache.Core
          
type ImplementationCreator = {
    Name : string 
    Creator : ILogger -> string -> ICache
}
with 
    static member Make( name, creator ) = 
        { Name = name; Creator = creator }

    override this.ToString() = this.Name
    
type CacheShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options

    static member Memory (options:MemoryCacheOptions) (logger:ILogger) (id:string)  =
        MemoryCache.Make( logger, id, options )
            
    static member Implementations
        with get () =
            seq {
                yield [| ImplementationCreator.Make( "memory", CacheShould.Memory MemoryCacheOptions.Default ) |]
            }
            
    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``BeCreateable`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name 
            
        Assert.Equal( 0, sut.Keys().Length )
        Assert.True( sut.Id.Length > 0 )

    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``CanCreateSetAndGet`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name 
        
        let nGet = ref 0
        let nSet = ref 0
        let nRemoved = ref 0
        
        sut.OnSet.Add( fun (k,v_) -> System.Threading.Interlocked.Increment( nSet ) |> ignore )
        sut.OnGet.Add( fun k -> System.Threading.Interlocked.Increment( nGet ) |> ignore )
        sut.OnRemove.Add( fun k -> System.Threading.Interlocked.Increment( nRemoved ) |> ignore )
        
        let v = Person.Example
        
        sut.Set v.Name v
        
        let v' = sut.Get v.Name
        
        Assert.Equal( v', v )
        
        Assert.True( sut.Remove v.Name ) 
        
        Assert.ThrowsAny<System.Exception>( fun () -> sut.Get v.Name |> ignore ) |> ignore
        
        // we do not count a failed 'Get' - it won't fire
        Assert.Equal( 1, !nGet )
        Assert.Equal( 1, !nSet )
        Assert.Equal( 1, !nRemoved )
        
    [<Theory>]
    [<MemberData("Implementations")>]

    member this.``HousekeepingWorks`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name 
        
        let v = Person.Example
        
        sut.Set v.Name v
        
        sut.Housekeep() |> Async.RunSynchronously
        
        sut.Dispose()
        
        Assert.True( true )
        
        
