using Content.Shared.Radio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.RadioStalker.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RadioStalkerComponent : Component
{
    [DataField("requiresPower"), ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresPower = false;

    /// <summary>
    /// The raw frequency string the user has tuned to.
    /// Null if not tuned or using a standard channel.
    /// </summary>
    [DataField("currentFrequency"), ViewVariables(VVAccess.ReadWrite)]
    public string? CurrentFrequency;
}
