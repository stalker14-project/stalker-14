using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Entities.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);
            component.EntProtos.RemoveAt(component.EntProtos.Count - 1); // stalker-changes

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0)
        {
            var copy = component.EntProtos; // stalker-changes-start
            copy.Reverse();
            var proto = copy.FirstOrNull();
            if (proto != null)
            {
                ent = Spawn(proto.Value, coordinates);
                EnsureShootable(ent.Value);
                component.EntProtos.RemoveAt(component.EntProtos.Count - 1);
                component.UnspawnedCount--;
            }
            else
            {
                component.UnspawnedCount--;
                ent = Spawn(component.Proto, coordinates);
                EnsureShootable(ent.Value);
            } // stalker-changes-end
        }

        if (ent != null)
            EjectCartridge(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}
