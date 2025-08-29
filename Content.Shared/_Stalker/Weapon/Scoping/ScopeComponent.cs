using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Weapon.Scoping;

/// <remarks>
/// Portions of this file are derived from the RMC-14 project, specifically from
/// https://github.com/RMC-14/RMC-14/tree/481a21c95148f5a7bff6ed1609324c836663ca30/Content.Shared/_RMC14/Scoping.
/// These files have been modified for use in this project.
/// The original code is licensed under the MIT License.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STSharedScopeSystem))]
public sealed partial class ScopeComponent : Component
{
    /// <summary>
    /// The entity that's scoping.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? User;

    /// <summary>
    /// Value to which zoom will be set when scoped in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Zoom = 1f;

    /// <summary>
    /// How much to offset the user's view by when scoping.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Offset = 15;

    /// <summary>
    /// If set to true, the user's movement won't interrupt the scoping action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowMovement;

    [DataField, AutoNetworkedField]
    public EntProtoId ScopingToggleAction = "CTActionToggleScope";

    [DataField, AutoNetworkedField]
    public EntityUid? ScopingToggleActionEntity;

    [DataField, AutoNetworkedField]
    public bool RequireWielding;

    [DataField, AutoNetworkedField]
    public bool UseInHand;

    [DataField, AutoNetworkedField]
    public Direction? ScopingDirection;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public EntityUid? RelayEntity;
}
