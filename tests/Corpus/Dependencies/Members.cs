// Field, property, event, parameter and return types, unwrapping arrays, nullables, tuples and generic arguments.
#nullable enable
using App.Infrastructure;

namespace App.Domain.Cases;

public class Members
{
    private Db _field = null!; // ARCH001 F:App.Domain.Cases.Members._field T:App.Infrastructure.Db "Db"
    private Db? _first, _second; // ARCH001 F:App.Domain.Cases.Members._first T:App.Infrastructure.Db "Db"
    private Db[] _array = []; // ARCH001 F:App.Domain.Cases.Members._array T:App.Infrastructure.Db "Db"
    private InfraValue? _nullable; // ARCH001 F:App.Domain.Cases.Members._nullable T:App.Infrastructure.InfraValue "InfraValue"
    private (int Id, Db Db) _tuple; // ARCH001 F:App.Domain.Cases.Members._tuple T:App.Infrastructure.Db "Db"
    private Repository<Entity>? _generic; // ARCH001 F:App.Domain.Cases.Members._generic T:App.Infrastructure.Repository`1 "Repository"
    private System.Collections.Generic.Dictionary<string, Repository<Db>>? _nested; // ARCH001 F:App.Domain.Cases.Members._nested T:App.Infrastructure.Repository`1 "Repository"
    private Entity? _allowed;

    public Db Property { get; set; } = null!; // ARCH001 P:App.Domain.Cases.Members.Property T:App.Infrastructure.Db "Db"

    public event InfraHandler? FieldEvent; // ARCH001 E:App.Domain.Cases.Members.FieldEvent T:App.Infrastructure.InfraHandler "InfraHandler"

    public event InfraHandler AccessorEvent { add { } remove { } } // ARCH001 E:App.Domain.Cases.Members.AccessorEvent T:App.Infrastructure.InfraHandler "InfraHandler"

    public int this[Db key] => 0; // ARCH001 P:App.Domain.Cases.Members.Item(App.Infrastructure.Db) T:App.Infrastructure.Db "Db"

    public Members(Db db) { } // ARCH001 M:App.Domain.Cases.Members.#ctor(App.Infrastructure.Db) T:App.Infrastructure.Db "Db"

    public Db Load() => null!; // ARCH001 M:App.Domain.Cases.Members.Load T:App.Infrastructure.Db "Db"

    public void Save(int id, Db db) { } // ARCH001 M:App.Domain.Cases.Members.Save(System.Int32,App.Infrastructure.Db) T:App.Infrastructure.Db "Db"

    public void Out(out Db db) => db = null!; // ARCH001 M:App.Domain.Cases.Members.Out(App.Infrastructure.Db@) T:App.Infrastructure.Db "Db"

    public static implicit operator Db(Members members) => null!; // ARCH001 M:App.Domain.Cases.Members.op_Implicit(App.Domain.Cases.Members)~App.Infrastructure.Db T:App.Infrastructure.Db "Db"

    public class Nested
    {
        public Db? Inner; // ARCH001 F:App.Domain.Cases.Members.Nested.Inner T:App.Infrastructure.Db "Db"
    }
}

public delegate Db Factory(Repository<Entity> source); // ARCH001 T:App.Domain.Cases.Factory T:App.Infrastructure.Db "Db" ARCH001 T:App.Domain.Cases.Factory T:App.Infrastructure.Repository`1 "Repository"

public record Snapshot(Db Value); // ARCH001 M:App.Domain.Cases.Snapshot.#ctor(App.Infrastructure.Db) T:App.Infrastructure.Db "Db"
