using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Content.Shared._Stalker.Bands;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Bands.Components
{
    /// <summary>
    /// Component attached to entities that can open the Band Management UI.
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    // [Access(typeof(SharedBandsSystem))]
    public sealed partial class BandsManagingComponent : Component
    {

        /// <summary>
        /// The shop listings prototype ID to use for listing items and prices.
        /// </summary>
        [DataField("shopListingsProto", required: true)]
        public ProtoId<BandShopListingsPrototype> ShopListingsProto { get; private set; } = default!;
    }
}