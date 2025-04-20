using Content.Shared.Standing;

namespace Content.Shared._Stalker.Weapon.Evasion;

public sealed class STEvasionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<STEvasionComponent, MapInitEvent>(RefreshEvasionModifiers);
        SubscribeLocalEvent<STEvasionComponent, DownedEvent>(RefreshEvasionModifiers);
        SubscribeLocalEvent<STEvasionComponent, StoodEvent>(RefreshEvasionModifiers);
    }

    public float GetEvasion(Entity<STEvasionComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, logMissing: false) ? ent.Comp.ModifiedEvasion : STEvasionComponent.DefaultEvasion;
    }

    private void RefreshEvasionModifiers<T>(Entity<STEvasionComponent> ent, ref T _)
        where T : notnull
    {
        var ev = new STEvasionRefreshModifiersEvent(ent, ent.Comp.Evasion);
        RaiseLocalEvent(ent.Owner, ref ev);

        ent.Comp.ModifiedEvasion = ev.Evasion;
        Dirty(ent);
    }
}
