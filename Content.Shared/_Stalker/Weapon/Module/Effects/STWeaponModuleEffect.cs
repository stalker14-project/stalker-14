using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Weapon.Module.Effects;

[DataDefinition, Serializable, NetSerializable]
public partial struct STWeaponModuleEffect()
{
    [DataField, ViewVariables]
    public float FireRateModifier = 1f;

    [DataField, ViewVariables]
    public float AngleDecayModifier = 1f;

    [DataField, ViewVariables]
    public float AngleIncreaseModifier = 1f;

    [DataField, ViewVariables]
    public float MaxAngleModifier = 1f;

    [DataField, ViewVariables]
    public float MinAngleModifier = 1f;

    [DataField, ViewVariables]
    public float ProjectileSpeedModifier = 1f;

    [DataField, ViewVariables]
    public int SoundGunshotVolumeAddition = 0;

    [DataField, ViewVariables]
    public float FarshotSoundDecrease = 1f;

    [DataField, ViewVariables]
    public SelectiveFire AdditionalAvailableModes = SelectiveFire.Invalid;

    public static STWeaponModuleEffect Merge(STWeaponModuleEffect effectA, STWeaponModuleEffect effectB)
    {
        return new STWeaponModuleEffect
        {
            FireRateModifier = effectA.FireRateModifier * effectB.FireRateModifier,
            AngleDecayModifier = effectA.AngleDecayModifier * effectB.AngleDecayModifier,
            AngleIncreaseModifier = effectA.AngleIncreaseModifier * effectB.AngleIncreaseModifier,
            MaxAngleModifier = effectA.MaxAngleModifier * effectB.MaxAngleModifier,
            MinAngleModifier = effectA.MinAngleModifier * effectB.MinAngleModifier,
            ProjectileSpeedModifier = effectA.ProjectileSpeedModifier * effectB.ProjectileSpeedModifier,
            SoundGunshotVolumeAddition = effectA.SoundGunshotVolumeAddition + effectB.SoundGunshotVolumeAddition,
            FarshotSoundDecrease = effectA.FarshotSoundDecrease + effectB.FarshotSoundDecrease,
            AdditionalAvailableModes = effectA.AdditionalAvailableModes | effectB.AdditionalAvailableModes,
        };
    }
}
