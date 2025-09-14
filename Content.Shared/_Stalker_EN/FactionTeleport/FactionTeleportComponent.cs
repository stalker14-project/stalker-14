using System.Linq;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.FactionTeleport;

/// <summary>
/// Place this component on an entity to mark it as a teleport destination for a given faction.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FactionTeleportComponent : Component
{
    /// <summary>
    /// Name of the faction that can use this teleport destination (must match BandsComponent.BandName).
    /// </summary>
    [DataField("factions"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public List<string> Factions = new() { "STStalkerBand" };

    /// <summary>
    /// Optional display color for the name in the UI. Example: "#FFCC00".
    /// If null, the default label color is used.
    /// </summary>
    [DataField("color"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Color? NameColor;
}
