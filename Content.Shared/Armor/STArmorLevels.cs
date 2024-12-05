using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared.Armor;

[DataDefinition, Serializable, NetSerializable, Virtual]
public partial class STArmorLevels
{
    // Generic
    [DataField("nonPvPPhysical")]
    public int NonPvPPhysicalAdjust = 0;

    [DataField("piercing")]
    public int PiercingAdjust = 0;

    [DataField("radiation")]
    public int RadiationAdjust = 0;

    [DataField("environment")]
    public int EnvironmentAdjust = 0;

    // Exact
    [DataField("heat")]
    public int HeatAdjust = 0;

    [DataField("caustic")]
    public int CausticAdjust = 0;

    [DataField("shock")]
    public int ShockAdjust = 0;

    [DataField("psy")]
    public int PsyAdjust = 0;

    [DataField("compression")]
    public int CompressionAdjust = 0;


    public DamageModifierSet ApplyLevels(DamageModifierSet baseModifiers)
    {
        var newModifiers = new DamageModifierSet();
        newModifiers = ApplyLevelToGroup(baseModifiers, NonPvPPhysicalAdjust, new[] { "Blunt", "Slash" });
        newModifiers = ApplyLevelToGroup(newModifiers, PiercingAdjust, new[] { "Piercing" });
        newModifiers = ApplyLevelToGroup(newModifiers, RadiationAdjust, new[] { "Radiation" });
        newModifiers = ApplyLevelToGroup(newModifiers, EnvironmentAdjust, new[] { "Heat", "Caustic", "Shock", "Compression", "Psy" });

        newModifiers = ApplyLevelToGroup(baseModifiers, HeatAdjust, new[] { "Heat" });
        newModifiers = ApplyLevelToGroup(newModifiers, CausticAdjust, new[] { "Caustic" });
        newModifiers = ApplyLevelToGroup(newModifiers, ShockAdjust, new[] { "Shock" });
        newModifiers = ApplyLevelToGroup(newModifiers, PsyAdjust, new[] { "Psy" });
        newModifiers = ApplyLevelToGroup(newModifiers, CompressionAdjust, new[] { "Compression" });
        return newModifiers;
    }

    private DamageModifierSet ApplyLevelToGroup(DamageModifierSet modifiers, int level, string[] damageTypes)
    {
        foreach (var damageType in damageTypes)
        {
            if (modifiers.Coefficients.TryGetValue(damageType, out var coefficient))
            {
                modifiers.Coefficients[damageType] = MathF.Round(Math.Clamp(coefficient + (level * -0.025f), 0f, 1f), 2);
            }

            if (modifiers.FlatReduction.TryGetValue(damageType, out var flatReduction))
            {
                modifiers.FlatReduction[damageType] = MathF.Round(flatReduction + flatReduction * (level * 0.5f), 2);
            }
        }
        return modifiers;
    }
}
