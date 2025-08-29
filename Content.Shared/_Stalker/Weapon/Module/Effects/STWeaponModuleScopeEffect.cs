using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Weapon.Module.Effects;

[DataDefinition, Serializable, NetSerializable]
public partial struct STWeaponModuleScopeEffect()
{
    [DataField, ViewVariables]
    public float Zoom = 1f;

    /// <summary>
    /// How much to offset the user's view by when scoping.
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

    public static STWeaponModuleScopeEffect Merge(STWeaponModuleScopeEffect effectA, STWeaponModuleScopeEffect effectB)
    {
        return new STWeaponModuleScopeEffect
        {
            Zoom = MathF.Max(effectA.Zoom, effectB.Zoom),
            Offset = MathF.Max(effectA.Offset, effectB.Offset),
            AllowMovement = effectA.AllowMovement && effectB.AllowMovement,
            RequireWielding = effectA.RequireWielding && effectB.RequireWielding,
            UseInHand = effectA.UseInHand && effectB.UseInHand,
            Delay = effectA.Delay,
        };
    }
}
