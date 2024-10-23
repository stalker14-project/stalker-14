using Content.Shared.Item.ItemToggle;
using Content.Shared._Stalker;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Components;

/// <summary>
/// Використання для ProximityDetector, переключає компоненти при кидку, покладенні або взяті предмету.
/// </summary>

[RegisterComponent, NetworkedComponent, Access(typeof(ComponentTogglerSystem))]
public sealed partial class StalkerArtifactComponent : Component
{
    /// <summary>
    /// The components to add when activated.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    /// The components to remove when deactivated.
    /// If this is null <see cref="Components"/> is reused.
    /// </summary>
    [DataField]
    public ComponentRegistry? RemoveComponents;

    /// <summary>
    /// If true, adds components on the entity's parent instead of the entity itself.
    /// </summary>
    [DataField]
    public bool Parent;
}