using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Content.Shared.Store;

namespace Content.Shared._Stalker.Shop.Prototypes;

[Prototype("shopPreset")]
public sealed class ShopPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("currencies")]
    public HashSet<ProtoId<CurrencyPrototype>> Currencies = new();

    [DataField("categories")]
    public List<ShopPresetCategory> Categories = new();

    [DataField("itemsForSale")]
    public Dictionary<string, int> SellingItems = new();
}

