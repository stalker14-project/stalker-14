using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
/// Used for clothing that reduces damage when worn.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedArmorSystem))]
public sealed partial class ArmorComponent : Component
{
    /// <summary>
    /// The base damage reduction
    /// </summary> 
    [DataField("modifiers", required: true)]  // Stalker-Changes
    public DamageModifierSet BaseModifiers = default!;  // Stalker-Changes

    /// <summary>
    /// The current damage reduction, after applying armor levels
    /// </summary>
    [ViewVariables]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// The armor levels that modify the base modifiers
    /// </summary>
    [DataField("armorLevels")] // Stalker-Changes
    public STArmorLevels? STArmorLevels = default!; // Stalker-Changes

    [DataField("armorClass", required: false)] // Stalker-Changes
    public int? ArmorClass; // Stalker-Changes

    [DataField("hidden")] // Stalker-Changes
    public bool Hidden; // Stalker-Changes

    [DataField("hiddenExamine")] // Stalker-Changes
    public bool HiddenExamine;  // Stalker-Changes
    /// <summary>
    /// A multiplier applied to the calculated point value
    /// to determine the monetary value of the armor
    /// </summary>
    [DataField]
    public float PriceMultiplier = 1;
}

/// <summary>
/// Event raised on an armor entity to get additional examine text relating to its armor.
/// </summary>
/// <param name="Msg"></param>
[ByRefEvent]
public record struct ArmorExamineEvent(FormattedMessage Msg);
