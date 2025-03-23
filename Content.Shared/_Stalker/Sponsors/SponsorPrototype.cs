using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Sponsors;

[Prototype("sponsor"), Serializable]
public sealed class SponsorPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(serverOnly: true)]
    public Dictionary<int, float> RepositoryWeight = new();

    [DataField(serverOnly: true)]
    public Dictionary<int, List<EntProtoId>> RepositorySponsorItems = new();

    [DataField(serverOnly: true)]
    public Dictionary<string, List<ShopCategory>> PersonalShopCategories = new();

    [DataField]
    public List<EntProtoId> ContribItems = new();
}
