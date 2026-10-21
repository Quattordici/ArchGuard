// Dependencies in code: creation, calls (static, instance, extension), member access, method groups, typeof, casts, type checks,
// patterns, catch, explicitly typed locals, arrays, default, lambdas and local functions.
#nullable enable
using App.Infrastructure;

namespace App.Domain.Cases;

public class Bodies
{
    private readonly object _initialized = new Db(); // ARCH001 F:App.Domain.Cases.Bodies._initialized T:App.Infrastructure.Db "Db"

    static Bodies() => Util.Do(); // ARCH001 M:App.Domain.Cases.Bodies.#cctor T:App.Infrastructure.Util "Do"

    public int Accessor => Db.MaxSize; // ARCH001 M:App.Domain.Cases.Bodies.get_Accessor T:App.Infrastructure.Db "MaxSize"

    public object Create() => new Db(); // ARCH001 M:App.Domain.Cases.Bodies.Create T:App.Infrastructure.Db "Db"

    public object CreateGeneric() => new System.Collections.Generic.List<Db>(); // ARCH001 M:App.Domain.Cases.Bodies.CreateGeneric T:App.Infrastructure.Db "Db"

    public void CallStatic() => Util.Do(); // ARCH001 M:App.Domain.Cases.Bodies.CallStatic T:App.Infrastructure.Util "Do"

    public void CallChain() => Db.Open().Save(); // ARCH001 M:App.Domain.Cases.Bodies.CallChain T:App.Infrastructure.Db "Open" ARCH001 M:App.Domain.Cases.Bodies.CallChain T:App.Infrastructure.Db "Save"

    public void CallExtension(Entity entity) => entity.Flush(); // ARCH001 M:App.Domain.Cases.Bodies.CallExtension(App.Domain.Entity) T:App.Infrastructure.DbExtensions "Flush"

    public string ReadProperty(object value) => ((Db)value).Name; // ARCH001 M:App.Domain.Cases.Bodies.ReadProperty(System.Object) T:App.Infrastructure.Db "Db" ARCH001 M:App.Domain.Cases.Bodies.ReadProperty(System.Object) T:App.Infrastructure.Db "Name"

    public void Subscribe(object value) => ((Db)value).Changed += null; // ARCH001 M:App.Domain.Cases.Bodies.Subscribe(System.Object) T:App.Infrastructure.Db "Db" ARCH001 M:App.Domain.Cases.Bodies.Subscribe(System.Object) T:App.Infrastructure.Db "Changed"

    public System.Action MethodGroup() => Util.Do; // ARCH001 M:App.Domain.Cases.Bodies.MethodGroup T:App.Infrastructure.Util "Do"

    public InfraKind EnumValue() => InfraKind.B; // ARCH001 M:App.Domain.Cases.Bodies.EnumValue T:App.Infrastructure.InfraKind "InfraKind" ARCH001 M:App.Domain.Cases.Bodies.EnumValue T:App.Infrastructure.InfraKind "B"

    public System.Type TypeOf() => typeof(Db); // ARCH001 M:App.Domain.Cases.Bodies.TypeOf T:App.Infrastructure.Db "Db"

    public object? As(object value) => value as Db; // ARCH001 M:App.Domain.Cases.Bodies.As(System.Object) T:App.Infrastructure.Db "Db"

    public bool Is(object value) => value is Db; // ARCH001 M:App.Domain.Cases.Bodies.Is(System.Object) T:App.Infrastructure.Db "Db"

    public bool Declaration(object value) => value is Db db && db.Count > 0; // ARCH001 M:App.Domain.Cases.Bodies.Declaration(System.Object) T:App.Infrastructure.Db "Db" ARCH001 M:App.Domain.Cases.Bodies.Declaration(System.Object) T:App.Infrastructure.Db "Count"

    public bool Recursive(object value) => value is Db { Count: > 0 }; // ARCH001 M:App.Domain.Cases.Bodies.Recursive(System.Object) T:App.Infrastructure.Db "Db" ARCH001 M:App.Domain.Cases.Bodies.Recursive(System.Object) T:App.Infrastructure.Db "Count"

    public int Switch(object value) => value switch { Db => 1, _ => 0 }; // ARCH001 M:App.Domain.Cases.Bodies.Switch(System.Object) T:App.Infrastructure.Db "Db"

    public void Catch()
    {
        try
        {
        }
        catch (InfraException) // ARCH001 M:App.Domain.Cases.Bodies.Catch T:App.Infrastructure.InfraException "InfraException"
        {
        }
    }

    public void Locals(System.Collections.Generic.List<object> items)
    {
        Db? local = null; // ARCH001 M:App.Domain.Cases.Bodies.Locals(System.Collections.Generic.List{System.Object}) T:App.Infrastructure.Db "Db"
        foreach (Db item in items) // ARCH001 M:App.Domain.Cases.Bodies.Locals(System.Collections.Generic.List{System.Object}) T:App.Infrastructure.Db "Db"
        {
        }

        Tools.Out(out Db written); // ARCH001 M:App.Domain.Cases.Bodies.Locals(System.Collections.Generic.List{System.Object}) T:App.Infrastructure.Db "Db"
        var inferred = new Entity();
        _ = (local, written, inferred);
    }

    public object Array() => new Db[2]; // ARCH001 M:App.Domain.Cases.Bodies.Array T:App.Infrastructure.Db "Db"

    public object? Default() => default(Db); // ARCH001 M:App.Domain.Cases.Bodies.Default T:App.Infrastructure.Db "Db"

    public void Lambdas()
    {
        var typed = (Db value) => { }; // ARCH001 M:App.Domain.Cases.Bodies.Lambdas T:App.Infrastructure.Db "Db"
        System.Func<Entity, int> inferred = entity => 0;
        _ = (typed, inferred);
    }

    public void LocalFunctions()
    {
        Db Identity(Db value) => value; // ARCH001 M:App.Domain.Cases.Bodies.LocalFunctions T:App.Infrastructure.Db "Db" ARCH001 M:App.Domain.Cases.Bodies.LocalFunctions T:App.Infrastructure.Db "Db"
        _ = Identity(null!);
    }
}
