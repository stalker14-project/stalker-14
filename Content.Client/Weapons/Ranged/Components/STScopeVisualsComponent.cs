using Content.Client.Weapons.Ranged.Systems;

namespace Content.Client.Weapons.Ranged.Components;

/// <summary>
/// Visualizer for gun scope presence
/// </summary>
[RegisterComponent, Access(typeof(GunSystem))]
public sealed partial class STScopeVisualsComponent : Component
{
    /// <summary>
    /// What RsiState we use.
    /// </summary>
    [DataField("scopeState")] public string? ScopeState;
}
