// mustNotDependOn: Domain must not use Vendor.Orm; other layers may.
#nullable enable

namespace App.Domain.Cases
{
    public class UsesOrm
    {
        private Vendor.Orm.OrmContext? _context; // ARCH002 F:App.Domain.Cases.UsesOrm._context T:Vendor.Orm.OrmContext "OrmContext"

        public object Create() => new Vendor.Orm.OrmContext(); // ARCH002 M:App.Domain.Cases.UsesOrm.Create T:Vendor.Orm.OrmContext "OrmContext"
    }
}

namespace App.Application.Cases
{
    public class AllowedToUseOrm
    {
        private Vendor.Orm.OrmContext? _context;
    }
}
