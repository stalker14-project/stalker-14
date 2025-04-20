using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WeaponModule;

/// <summary>
/// Indicates that this entity is a weapon module.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STWeaponModuleComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleEffect Effect;

    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleScopeEffect? ScopeEffect;
}

[DataDefinition, Serializable, NetSerializable]
public partial struct STWeaponModuleScopeEffect()
{
    [DataField, ViewVariables]
    public float Zoom = 1f;

    /// <summary>
    ///     How much to offset the user's view by when scoping.
    /// </summary>
    [DataField, ViewVariables]
    public float Offset = 15;

    /// <summary>
    /// If set to true, the user's movement won't interrupt the scoping action.
    /// </summary>
    [DataField, ViewVariables]
    public bool AllowMovement;

    [DataField, ViewVariables]
    public bool RequireWielding;

    [DataField, ViewVariables]
    public bool UseInHand;

    [DataField, ViewVariables]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);
    public static STWeaponModuleScopeEffect Merge(STWeaponModuleScopeEffect? effectA, STWeaponModuleScopeEffect? effectB)
    {
        // Scope can be only one.
        var scopeEffect = (effectA.HasValue ? effectA :
            (effectB.HasValue ? effectB : null)) ?? default;
        return new STWeaponModuleScopeEffect
        {
            Zoom = scopeEffect.Zoom,
            Offset = scopeEffect.Offset,
            AllowMovement = scopeEffect.AllowMovement,
            RequireWielding = scopeEffect.RequireWielding,
            UseInHand = scopeEffect.UseInHand,
            Delay = scopeEffect.Delay
        };
    }
}

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
            AdditionalAvailableModes = effectA.AdditionalAvailableModes | effectB.AdditionalAvailableModes,
        };
    }
}
