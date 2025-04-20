using Content.Client.Weapons.Ranged.Systems;

namespace Content.Client.Weapons.Ranged.Components;

/// <summary>
/// Visualizer for gun muzzle presence
/// </summary>
[RegisterComponent, Access(typeof(GunSystem))]
public sealed partial class STMuzzleVisualsComponent : Component
{
    /// <summary>
    /// What RsiState we use.
    /// </summary>
    [DataField("muzzleState")] public string? MuzzleState;
}
