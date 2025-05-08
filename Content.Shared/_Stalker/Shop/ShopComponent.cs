using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Shop;

[RegisterComponent]
public sealed partial class ShopComponent : Component
{
    /// <summary>
    /// Id of currency the shop using right now
    /// This will influence on item the shop is trying to find in player's inventory
    /// </summary>
    [DataField]
    public string MoneyId = "Roubles";

    [DataField("shopPresetId")]
    public string ShopPresetPrototype = "DebugShopPreset";

    [DataField("permitId")]
    public EntProtoId? Permit = default!;

    /// <summary>
    /// Made to not renew listings on each UI update
    /// </summary>
    public List<CategoryInfo> ShopCategories = new();

    public Dictionary<ProtoId<SponsorPrototype>, List<CategoryInfo>> ShopSponsorCategories = new();

    public List<CategoryInfo> ContributorCategories = new();

    public Dictionary<string, List<CategoryInfo>> PersonalCategories = new();

    public int CurrentBalance = 0;
}
