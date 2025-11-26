namespace Content.Server.Cargo.Components;
using Content.Shared.Actions;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

/// <summary>
/// Any entities intersecting when a shuttle is recalled will be sold.
/// </summary>

[Flags]
public enum BuySellType : byte
{
    Buy = 1 << 0,
    Sell = 1 << 1,
    All = Buy | Sell
}


[RegisterComponent]
public sealed partial class CargoPalletComponent : Component
{
    /// <summary>
    /// Whether the pad is a buy pad, a sell pad, or all.
    /// </summary>
    [DataField]
    public BuySellType PalletType;

    // Stalker-Changes-Start
    /// <summary>
    /// Which entities can be sold by that pallet. If empty, everything can be sold.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist = null;
    // Stalker-Changes-End
}
