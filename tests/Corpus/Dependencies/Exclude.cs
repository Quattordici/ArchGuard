// exclude: App.Domain.Legacy (and everything under it) is not checked as a source. A namespace that only starts with the same text is.
#nullable enable

namespace App.Domain.Legacy.Orders
{
    public class OldOrder : App.Infrastructure.InfraBase
    {
        public Vendor.Orm.OrmContext? Context;
    }
}

namespace App.Domain.LegacyTools
{
    public class NotExcluded : App.Infrastructure.InfraBase { } // ARCH001 T:App.Domain.LegacyTools.NotExcluded T:App.Infrastructure.InfraBase "InfraBase"
}
