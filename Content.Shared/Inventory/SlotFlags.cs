using Robust.Shared.Serialization;

namespace Content.Shared.Inventory;

/// <summary>
///     Defines what slot types an item can fit into.
/// </summary>
[Serializable, NetSerializable]
[Flags]
public enum SlotFlags
{
    NONE = 0,
    PREVENTEQUIP = 1 << 0,
    HEAD = 1 << 1,
    EYES = 1 << 2,
    EARS = 1 << 3,
    MASK = 1 << 4,
    OUTERCLOTHING = 1 << 5,
    TORSO = 1 << 6,  // Stalker-Changes-UI
    INNERCLOTHING = 1 << 7,
    NECK = 1 << 8,
    BACK = 1 << 9,
    BELT = 1 << 10,
    GLOVES = 1 << 11,
    IDCARD = 1 << 12,
    POCKET = 1 << 13,
    LEGS = 1 << 14, // Stalker-Changes-UI
    FEET = 1 << 15,
    SUITSTORAGE = 1 << 16,
    ARTIFACT = 1 << 17, // Stalker-Changes-UI
    BACK2 = 1 << 18, // Stalker-Changes-UI
    CLOAK = 1 << 19, // Stalker-Changes-UI
    All = ~NONE,

    WITHOUT_POCKET = All & ~POCKET
}
