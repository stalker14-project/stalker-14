namespace Content.Shared._Stalker.Weapon.Evasion;

[ByRefEvent]
public record struct STEvasionRefreshModifiersEvent(
    Entity<STEvasionComponent> Entity,
    float Evasion
);
