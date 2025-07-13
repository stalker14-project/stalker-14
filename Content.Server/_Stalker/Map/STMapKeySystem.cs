using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Map.Components;

namespace Content.Server._Stalker.Map;

public sealed class STMapKeySystem : EntitySystem
{
    public bool TryGet(string key, [NotNullWhen(true)] out Entity<MapComponent>? entity)
    {
        entity = null;

        var query = EntityQueryEnumerator<MapComponent, STMapKeyComponent>();
        while (query.MoveNext(out var uid, out var mapComponent, out var mapKeyComponent))
        {
            if (mapKeyComponent.Value != key)
                continue;

            entity = (uid, mapComponent);
            return true;
        }

        return false;
    }
}
