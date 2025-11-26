using Content.Shared.Damage;

namespace Content.Server._Stalker.RangedDamage;
/// <summary>
/// Applies ranged damage on targets in range after timeToDamage.
/// </summary>
[RegisterComponent]
public sealed partial class RangedDamageComponent : Component
{
    [DataField("damage")]
    public DamageSpecifier? Damage;

    [DataField("timeToDamage")]
    public float TimeToDamage = 5f;

    [DataField("range")]
    public float Range = 1f;

    [DataField("interruptDoAfters")]
    public bool InterruptDoAfters;

    [DataField("ignoreResistances")]
    public bool IgnoreResistances;

    // Deal damage on spawn without extensive triggers
    [DataField("activateOnSpawn")]
    public bool ActivateOnSpawn;

    // To determine if needed to delete this component on trigger
    [DataField("deleteSelfOnTrigger")]
    public bool DeleteSelfOnTrigger;
}
