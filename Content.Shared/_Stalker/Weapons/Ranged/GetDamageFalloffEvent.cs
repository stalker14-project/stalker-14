using Content.Shared.FixedPoint;

namespace Content.Shared._Stalker.Weapons.Ranged;

[ByRefEvent]
public record struct GetDamageFalloffEvent(
    FixedPoint2 FalloffMultiplier
);
