namespace Example.Cache.Tests

open Microsoft.Extensions.Logging

open Xunit
open Xunit.Abstractions 

open Example.Cache
          
type ImplementationCreator = {
    Name : string 
    Creator : ILogger -> string -> IEnumerableCache
}
with 
    static member Make( name, creator ) = 
        { Name = name; Creator = creator }

    override this.ToString() = this.Name
    
type CacheTesting() =
    
    static member Memory (options:Memory.Options) (logger:ILogger) (id:string)  =
        Memory.Cache.Make( logger, id, options )

    static member Sqlite (options:Sqlite.Options) (logger:ILogger) (id:string)  =
        Sqlite.Cache.Make( logger, id, options )
                
    static member Implementations
        with get () =
            seq {
                yield [| ImplementationCreator.Make( "memory", CacheTesting.Memory Memory.Options.Default ) |]
                //yield [| ImplementationCreator.Make( "sqlite", CacheTesting.Sqlite Sqlite.Options.Default ) |]
            }
            
type CacheShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options

    [<Theory>]
    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
    member this.``BeCreateable`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name 
            
        Assert.Equal( 0, sut.Keys().Length )
        Assert.True( sut.Name.Length > 0 )

    [<Theory>]
    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
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
    [<MemberData("Implementations", MemberType=typeof<CacheTesting>)>]
    member this.``HousekeepingWorks`` (creator:ImplementationCreator) =
        
        use sut =
            creator.Creator logger creator.Name 
        
        let v = Person.Example
        
        sut.Set v.Name v
        
        sut.Clean() |> Async.RunSynchronously
        
        sut.Dispose()
        
        Assert.True( true )
        
        
