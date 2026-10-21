namespace App.WithConfig.Infrastructure
{
    public sealed class Db
    {
    }
}

namespace App.WithConfig.Domain
{
    public sealed class Order
    {
        public Infrastructure.Db Db { get; set; }
    }
}
