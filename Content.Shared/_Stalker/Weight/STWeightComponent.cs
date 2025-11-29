using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STWeightComponent : Component
{
    /// <summary>
    /// The total weight of the entity, which is calculated
    /// by recursive passes over all children with this component
    /// </summary>
    [ViewVariables]
    public float Total => Self + InsideWeight;

    [ViewVariables]
    public float TotalMaximum => Maximum * MaximumModifier;

    [DataField, ViewVariables]
    public float InsideWeight;

    [DataField, ViewVariables, AutoNetworkedField]
    public float WeightThrowModifier = 0.1f;

    /// <summary>
    /// This allows you to adjust the strength of
    /// the throw so that small objects are not thrown harder,
    /// but large objects are thrown weaker
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float WeightThrowMinStrengthModifier = 1f;

    [DataField, ViewVariables, AutoNetworkedField]
    public float MovementSpeedModifier = 1f;

    [DataField, ViewVariables, AutoNetworkedField]
    public float MaximumModifier = 1f;

    /// <summary>
    /// <see cref="STWeightComponent.Total"/> weight at which the entity stops completely,
    /// yes this code has a linear deceleration schedule,
    /// possible improvements in the future
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Maximum = 200f;

    /// <summary>
    /// <see cref="STWeightComponent.Total"/> weight at which the entity begins to slow down.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Overload = 100f;

    [ViewVariables]
    public float TotalOverload => Overload * MaximumModifier;

    /// <summary>
    /// Entity's own weight
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Self = 0.05f;
}
