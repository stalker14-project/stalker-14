using Robust.Shared.Prototypes;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Shop;

namespace Content.Shared._Stalker.Shop;

[RegisterComponent]
public sealed partial class ShopComponent : Component
{
    [DataField("currencies")]
    public HashSet<ProtoId<STCurrencyPrototype>> Currencies = new();

    [DataField("shopPreset")]
    public ProtoId<ShopPresetPrototype> ShopPreset = default!;

    [DataField("categories")]
    public Dictionary<string, ShopCategory> Categories = new();
}
