using Content.Client.Weapons.Ranged.Systems;

namespace Content.Client.Weapons.Ranged.Components;

/// <summary>
/// Visualizer for gun under barrel presence
/// </summary>
[RegisterComponent, Access(typeof(GunSystem))]
public sealed partial class STUnderbarrelVisualsComponent : Component
{
    /// <summary>
    /// What RsiState we use.
    /// </summary>
    [DataField("underbarrelState")] public string? UnderbarrelState;
}
