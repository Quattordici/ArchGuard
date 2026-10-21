// Not dependencies (design §5): using directives, nameof, cref, inferred types, the own layer, allowed layers, code in no layer, the BCL and
// the global namespace.
#nullable enable
using App.Infrastructure;

namespace App.Domain.Cases
{
    /// <summary>Documented with <see cref="Db"/>.</summary>
    public class NotCounted
    {
        public string Name() => nameof(Db);

        public string MemberName() => nameof(Util.Do);

        public object OwnLayer() => new Entity();

        public object Bcl() => new System.Text.StringBuilder();

        public object NoLayer() => new App.Shared.Clock();

        public object GlobalNamespace() => new GlobalThing();
    }
}

namespace App.Application.Cases
{
    public class UsesDomain : App.Domain.DomainBase
    {
        public App.Domain.Entity Entity { get; } = new App.Domain.Entity();
    }
}

namespace App.Infrastructure.Cases
{
    public class UsesApplicationAndDomain
    {
        public App.Application.OrderService? Service;

        public App.Domain.Entity? Entity;
    }
}

namespace App.Api.Cases
{
    public class UsesEverything
    {
        public App.Infrastructure.Db? Db;

        public App.Application.OrderService? Service;

        public App.Domain.Entity? Entity;
    }
}
