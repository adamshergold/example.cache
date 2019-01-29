namespace Example.Cache

open Example.Serialisation

type ICache<'T when 'T :> ITypeSerialisable> = interface end
         
    