// Base types and implemented interfaces, including generic arguments of a base type.
using App.Infrastructure;

namespace App.Domain.Cases;

public class Order : InfraBase { } // ARCH001 T:App.Domain.Cases.Order T:App.Infrastructure.InfraBase "InfraBase"

public class Line : DomainBase, IInfra { } // ARCH001 T:App.Domain.Cases.Line T:App.Infrastructure.IInfra "IInfra"

public class Lines : System.Collections.Generic.List<Db> { } // ARCH001 T:App.Domain.Cases.Lines T:App.Infrastructure.Db "Db"

public partial class Split { }

public partial class Split : IInfra { } // ARCH001 T:App.Domain.Cases.Split T:App.Infrastructure.IInfra "IInfra"

public class Allowed : DomainBase, System.IDisposable
{
    public void Dispose() { }
}
