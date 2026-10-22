using System.Text;

namespace Archie.Benchmarks;

/// <summary>
/// A single-project layered codebase (Domain 40%, Application 30%, Infrastructure 30%) of about 250 lines per file, with one block of known
/// violations in every 10th Domain file. Taken from the Phase 0 spike, so the numbers stay comparable with its findings.
/// </summary>
internal static class SyntheticCode
{
    /// <summary><c>(path, text)</c> of every file; <paramref name="files"/> = 400 is about 114k lines.</summary>
    public static IReadOnlyList<(string Path, string Text)> Generate(int files)
    {
        var sources = new List<(string Path, string Text)>(files + 1) { ("Shared.cs", Shared) };
        for (var i = 0; i < files; i++)
        {
            var layer = (i % 10) switch { < 4 => "Domain", < 7 => "Application", _ => "Infrastructure" };
            sources.Add(($"{layer}/M{i / 50}/Entity{i}.cs", FileText(layer, i, withViolations: layer == "Domain" && i % 40 == 0)));
        }

        return sources;
    }

    private static string FileText(string layer, int i, bool withViolations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine($"using Synth.{layer}.Shared;");
        if (withViolations)
            sb.AppendLine("using Synth.Infrastructure.Ports;");
        sb.AppendLine();
        sb.AppendLine($"namespace Synth.{layer}.M{i / 50};");
        sb.AppendLine();
        sb.AppendLine($"public sealed record Value{i}(int Id, string Name, decimal Amount);");
        sb.AppendLine();
        sb.AppendLine($"public interface IEntity{i}Store {{ Task<Entity{i}> FindAsync(int id); IReadOnlyList<Entity{i}> All(); }}");
        sb.AppendLine();
        sb.AppendLine($"public class Entity{i} : EntityBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly List<Value{i}> _values = new();");
        sb.AppendLine("    private readonly Dictionary<int, string> _names = new();");
        sb.AppendLine($"    private readonly IEntity{i}Store _store;");
        sb.AppendLine("    public string Name { get; set; } = \"\";");
        sb.AppendLine("    public decimal Total => _values.Sum(v => v.Amount);");
        sb.AppendLine();
        sb.AppendLine($"    public Entity{i}(int id, string name, IEntity{i}Store store) : base(id) {{ Name = name; _store = store; }}");
        sb.AppendLine();
        for (var m = 0; m < 10; m++)
            sb.Append(Method(i, m));
        if (withViolations)
            sb.Append(Violations);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Method(int i, int m) => $$"""
            public async Task<decimal> Process{{m}}Async(IEnumerable<int> ids, Func<Value{{i}}, bool> filter)
            {
                var total = 0m;
                var builder = new StringBuilder();
                foreach (var id in ids.Where(x => x % {{m + 2}} != 0).OrderBy(x => x))
                {
                    var entity = await _store.FindAsync(id);
                    if (entity is null || !entity.IsActive)
                        continue;
                    var values = entity._values.Where(filter).Select(v => v with { Amount = v.Amount * {{m + 1}} }).ToList();
                    total += values.Sum(v => v.Amount);
                    builder.Append($"{entity.Name}:{values.Count};");
                    _names[id] = entity.Name switch { "" => "unnamed", var n when n.Length > 10 => n[..10], var n => n };
                }
                try
                {
                    Guard.Positive(total, nameof(total));
                    Touch();
                }
                catch (InvalidOperationException ex) when (ex.Message.Length > 0)
                {
                    builder.Clear();
                }
                return Round(total, {{m % 4}});
                static decimal Round(decimal value, int digits) => Math.Round(value, digits);
            }

        """;

    private const string Violations = """

            // ---- known violations (counted by the harness) ----
            public class VBase : InfraBase { }
            public class VPort : IInfraPort { public void Run() { } }
            [InfraMarker] public void Marked() { }
            private InfraService _infraField;
            public InfraService InfraProperty { get; set; }
            public void TakesInfra(InfraService s) { }
            public InfraService ReturnsInfra() => null;
            public List<InfraService> ReturnsInfraList() => null;
            public void Constrained<T>() where T : InfraBase { }
            public void Bodies(object o)
            {
                InfraService local = null;
                var created = new InfraService();
                InfraUtil.Do();
                o.InfraExt();
                var value = InfraUtil.Value;
                var type = typeof(InfraService);
                var cast = (InfraService)o;
                var asCast = o as InfraService;
                if (o is InfraService) { }
                if (o is InfraService matched) { }
                try { } catch (InfraException) { }
                Factory.Create<InfraService>();
                var array = new InfraService[3];
                // not counted:
                var name = nameof(InfraService);
            }
            /// <see cref="InfraService"/>
            public void Documented() { }

        """;

    private const string Shared = """
        using System;

        namespace Synth.Domain.Shared
        {
            public abstract class EntityBase
            {
                protected EntityBase(int id) => Id = id;
                public int Id { get; }
                public bool IsActive { get; protected set; } = true;
                protected void Touch() => IsActive = true;
            }
            public static class Guard
            {
                public static void Positive(decimal value, string name) { if (value < 0) throw new InvalidOperationException(name); }
            }
            public static class Factory
            {
                public static T Create<T>() => default;
            }
        }

        namespace Synth.Application.Shared
        {
            public abstract class EntityBase : Synth.Domain.Shared.EntityBase { protected EntityBase(int id) : base(id) { } }
            public static class Guard
            {
                public static void Positive(decimal value, string name) => Synth.Domain.Shared.Guard.Positive(value, name);
            }
        }

        namespace Synth.Infrastructure.Shared
        {
            public abstract class EntityBase : Synth.Domain.Shared.EntityBase { protected EntityBase(int id) : base(id) { } }
            public static class Guard
            {
                public static void Positive(decimal value, string name) => Synth.Domain.Shared.Guard.Positive(value, name);
            }
        }

        namespace Synth.Infrastructure.Ports
        {
            public class InfraService { public void Run() { } }
            public abstract class InfraBase { }
            public interface IInfraPort { void Run(); }
            public class InfraException : Exception { }
            public sealed class InfraMarkerAttribute : Attribute { }
            public static class InfraUtil
            {
                public static void Do() { }
                public static int Value => 1;
            }
            public static class InfraExtensions
            {
                public static void InfraExt(this object o) { }
            }
        }
        """;
}
