using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server._Stalker.NPCs;

[RegisterComponent, Access(typeof(NPCBloodsuckerSystem)), AutoGenerateComponentPause]
public sealed partial class NPCBloodsuckerComponent : Component
{
    [DataField]
    public string TargetKey = "Target";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AttackRadius = 1.5f;

    [DataField("nextTimeUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? NextTimeUpdate = null;

    [DataField]
    public float UpdateCooldown = 0.2f;

    [DataField("startTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadOnly)]
    [AutoPausedField]
    public TimeSpan? StartTime = TimeSpan.FromSeconds(0f);

    [DataField("endTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadOnly)]
    [AutoPausedField]
    public TimeSpan? EndTime = TimeSpan.FromSeconds(0f);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float ReloadTime = 12f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RandomiseReloadTime = 12f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float StunTime = 2f;

    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier DamageOnSuck = default!;

    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier HealOnSuck = default!;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int CurrentStep = 0;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextStepTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsSucking = false;

    [DataField]
    public SoundSpecifier BloodsuckSound = new SoundPathSpecifier("/Audio/_Stalker/Mutants/bloodsucker_eat.ogg");

    [DataField]
    public float ShakeStrength = 11f;
}
