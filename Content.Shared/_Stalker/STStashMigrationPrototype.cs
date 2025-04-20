using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker;

[Prototype("STStashMigration")]
public sealed class STStashMigrationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    [DataField]
    public Dictionary<EntProtoId, EntProtoId> Mapping = [];
}
