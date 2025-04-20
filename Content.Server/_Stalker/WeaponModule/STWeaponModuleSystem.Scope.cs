using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Stalker.Scoping;
using Content.Shared._Stalker.WeaponModule;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Stalker.WeaponModule;

public sealed partial class STWeaponModuleSystem
{
    private void SetGunScope(Entity<STWeaponModuleComponent> entity, BaseContainer container)
    {
        if (!entity.Comp.ScopeEffect.HasValue)
            return;

        var effect = entity.Comp.ScopeEffect.Value;

        ScopeComponent scope = EnsureComp<ScopeComponent>(container.Owner);
        scope.Zoom = effect.Zoom;
        scope.AllowMovement = effect.AllowMovement;
        scope.Offset = effect.Offset;
        scope.Delay = effect.Delay;
        scope.RequireWielding = effect.RequireWielding;
        scope.UseInHand = effect.UseInHand;
        Dirty(container.Owner, scope);
    }
    private void DelGunScope(Entity<STWeaponModuleComponent> entity, BaseContainer container)
    {
        if (!TryComp<ScopeComponent>(container.Owner, out var scope) && scope is null)
            return;

        RemCompDeferred<ScopeComponent>(container.Owner);
        Dirty(entity);
    }
}
