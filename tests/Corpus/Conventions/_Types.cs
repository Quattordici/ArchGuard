// Types the cases use. Compiled into every case; must produce nothing itself.
namespace App.Application
{
    public interface IRequestHandler<TRequest, TResponse> { }

    public interface IRequestHandler<TRequest> { }
}

namespace App.Domain
{
    public interface IDomainEvent { }
}

namespace Vendor.Orm
{
    public class DbContext { }
}

namespace Vendor.Web
{
    public abstract class ControllerBase { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ApiControllerAttribute : System.Attribute { }

    public sealed class AreaApiControllerAttribute : System.Attribute { }
}
