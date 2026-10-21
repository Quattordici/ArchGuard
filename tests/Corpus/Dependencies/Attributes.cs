// Attributes on every kind of target, and types and enum values in attribute arguments.
using App.Infrastructure;

namespace App.Domain.Cases;

[InfraMarker] // ARCH001 T:App.Domain.Cases.Marked T:App.Infrastructure.InfraMarkerAttribute "InfraMarker"
public class Marked
{
    [InfraMarker] public int Field; // ARCH001 F:App.Domain.Cases.Marked.Field T:App.Infrastructure.InfraMarkerAttribute "InfraMarker"

    public void Parameter([InfraMarker] int value) { } // ARCH001 M:App.Domain.Cases.Marked.Parameter(System.Int32) T:App.Infrastructure.InfraMarkerAttribute "InfraMarker"

    [return: InfraMarker] // ARCH001 M:App.Domain.Cases.Marked.Return T:App.Infrastructure.InfraMarkerAttribute "InfraMarker"
    public int Return() => 0;

    public void TypeParameter<[InfraMarker] T>() { } // ARCH001 M:App.Domain.Cases.Marked.TypeParameter``1 T:App.Infrastructure.InfraMarkerAttribute "InfraMarker"
}

[TypeOnly(typeof(Db))] // ARCH001 T:App.Domain.Cases.TypeArgument T:App.Infrastructure.Db "Db"
public class TypeArgument { }

[Value(InfraKind.A)] // ARCH001 T:App.Domain.Cases.EnumArgument T:App.Infrastructure.InfraKind "InfraKind"
public class EnumArgument { }

[System.Obsolete("allowed")]
[TypeOnly(typeof(Entity))]
public class Allowed { }
