using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Psyonics.Actions.Dizzy;

[RegisterComponent]
public sealed partial class PsyonicsActionDizzyComponent : BasePsyonicsActionComponent
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public override EntProtoId ActionId { get; set; } = "ActionPsyonicsDizzy";

    // Stalker-TODO: Remove this field after proper systems fix
    [DataField]
    public EntProtoId MutantActionId { get; set; } = "ActionPsyonicsDizzyMutant";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public override int Cost { get; set; } = 5;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Duration = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     The amount of damage
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier? Damage = default!;
}
