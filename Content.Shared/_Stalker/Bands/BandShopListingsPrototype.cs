using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.ViewVariables;

namespace Content.Shared._Stalker.Bands
{
    [Prototype("stBandShopListings"), Serializable, NetSerializable] // Corrected prototype name to match YAML
    public sealed class BandShopListingsPrototype : IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField]
        public List<BandShopItem> Items { get; private set; } = new();
    }

    [DataDefinition, Serializable, NetSerializable]
    public sealed partial class BandShopItem
    {
        [DataField(required: true)]
        public string ProductEntity { get; set; } = default!;

        [DataField(required: true)]
        public int Price { get; set; } = 0;
    }
}