using Content.Shared._Stalker.Shop.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Sponsors;

[Prototype("personalShop"), Serializable]
public sealed class PersonalShopPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField(serverOnly: true)]
    public string Username = null!;
    
    [DataField(serverOnly: true)]
    public List<CategoryInfo> Categories { get; } = new();
}
