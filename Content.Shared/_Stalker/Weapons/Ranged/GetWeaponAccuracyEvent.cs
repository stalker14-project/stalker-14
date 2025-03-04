using Content.Shared.FixedPoint;

namespace Content.Shared._Stalker.Weapons.Ranged;

[ByRefEvent]
public record struct GetWeaponAccuracyEvent(
    FixedPoint2 AccuracyMultiplier
);
