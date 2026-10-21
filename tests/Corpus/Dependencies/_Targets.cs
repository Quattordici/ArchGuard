// The types the cases use. Compiled into every case; produces no diagnostics itself (Infrastructure may use Application and Domain).
#nullable enable

namespace App.Infrastructure
{
    public class Db
    {
        public const int MaxSize = 10;

        public int Count;

        public string Name { get; set; } = "";

        public event System.EventHandler? Changed;

        public static Db Open() => new Db();

        public void Save() => Changed?.Invoke(this, System.EventArgs.Empty);
    }

    public class InfraBase { }

    public interface IInfra { }

    public struct InfraValue { }

    public enum InfraKind { A, B }

    public sealed class InfraException : System.Exception { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class InfraMarkerAttribute : System.Attribute { }

    public static class DbExtensions
    {
        public static void Flush(this App.Domain.Entity entity) { }
    }

    public class Repository<T> { }

    public delegate void InfraHandler();

    public static class Util
    {
        public static void Do() { }
    }
}

namespace App.Application
{
    public class OrderService { }
}

namespace App.Api
{
    public class Controller { }
}

namespace App.Domain
{
    public class Entity { }

    public class DomainBase { }

    public static class Tools
    {
        public static T Make<T>() => default!;

        public static void Out<T>(out T value) => value = default!;
    }

    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class TypeOnlyAttribute(System.Type type) : System.Attribute
    {
        public System.Type Type { get; } = type;
    }

    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class ValueAttribute(object value) : System.Attribute
    {
        public object Value { get; } = value;
    }
}

// Under rootNamespace but in no layer: anyone may use it.
namespace App.Shared
{
    public class Clock { }
}

// Outside the layers; Domain's mustNotDependOn names it.
namespace Vendor.Orm
{
    public class OrmContext { }
}

public class GlobalThing { }
