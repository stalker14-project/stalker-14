using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Stalker.Shop.Prototypes;

[Prototype("STcurrency")]
public sealed class STCurrencyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// The entity representing this currency (e.g. stack of roubles)
    /// </summary>
    [DataField("entity", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EntityId { get; } = string.Empty;

    /// <summary>
    /// Display name for UI
    /// </summary>
    [DataField("name")]
    public string Name { get; } = string.Empty;

    /// <summary>
    /// Icon to display in shop interface
    /// </summary>
    [DataField("icon")]
    public string Icon { get; } = string.Empty;

    /// <summary>
    /// Can this currency be used for purchases?
    /// </summary>
    [DataField("canBuy")]
    public bool CanBuy { get; } = true;

    /// <summary>
    /// Can this currency be received from sales?
    /// </summary>
    [DataField("canSell")]
    public bool CanSell { get; } = true;
}
