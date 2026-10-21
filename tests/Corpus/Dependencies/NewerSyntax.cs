// Constructs the other cases don't spell out: primary constructors, using static and aliases, default parameter values, deconstruction,
// collection expressions, throw expressions and written type arguments of value types.
#nullable enable
using App.Infrastructure;
using static App.Infrastructure.Util;
using DbAlias = App.Infrastructure.Db;

namespace App.Domain.Cases;

public class PrimaryConstructor(Db db) // ARCH001 M:App.Domain.Cases.PrimaryConstructor.#ctor(App.Infrastructure.Db) T:App.Infrastructure.Db "Db"
{
    public object Value => db;
}

public class NewerSyntax
{
    public DbAlias? Alias; // ARCH001 F:App.Domain.Cases.NewerSyntax.Alias T:App.Infrastructure.Db "DbAlias"

    public void UsingStatic() => Do(); // ARCH001 M:App.Domain.Cases.NewerSyntax.UsingStatic T:App.Infrastructure.Util "Do"

    public void DefaultValue(int size = Db.MaxSize) { } // ARCH001 M:App.Domain.Cases.NewerSyntax.DefaultValue(System.Int32) T:App.Infrastructure.Db "MaxSize"

    public void Deconstruction()
    {
        (Db db, int count) = (null!, 1); // ARCH001 M:App.Domain.Cases.NewerSyntax.Deconstruction T:App.Infrastructure.Db "Db"
        _ = (db, count);
    }

    public object CollectionExpression() => new System.Collections.Generic.List<object> { 1 } is [Db] ? 1 : 0; // ARCH001 M:App.Domain.Cases.NewerSyntax.CollectionExpression T:App.Infrastructure.Db "Db"

    public object Throw(object? value) => value ?? throw new InfraException(); // ARCH001 M:App.Domain.Cases.NewerSyntax.Throw(System.Object) T:App.Infrastructure.InfraException "InfraException"

    public void ValueTypeArgument() => Generic<InfraValue>(); // ARCH001 M:App.Domain.Cases.NewerSyntax.ValueTypeArgument T:App.Infrastructure.InfraValue "InfraValue"

    private static void Generic<T>() where T : struct { }
}
