// Generic constraints and explicitly written type arguments. Inferred type arguments are not a dependency by themselves.
using App.Infrastructure;

namespace App.Domain.Cases;

public class Holder<T> where T : InfraBase { } // ARCH001 T:App.Domain.Cases.Holder`1 T:App.Infrastructure.InfraBase "InfraBase"

public class GenericMethods
{
    public void Constrained<T>() where T : IInfra { } // ARCH001 M:App.Domain.Cases.GenericMethods.Constrained``1 T:App.Infrastructure.IInfra "IInfra"

    public void Explicit() => Tools.Make<Db>(); // ARCH001 M:App.Domain.Cases.GenericMethods.Explicit T:App.Infrastructure.Db "Db"

    public Entity Allowed() => Tools.Make<Entity>();
}
