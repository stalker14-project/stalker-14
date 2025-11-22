using System.Numerics;
using Content.Shared._Stalker.Dimension;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Dimension;

public sealed class STDimensionSystem : STSharedDimensionSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public void EnterDimension(EntityUid target, ProtoId<STDimensionPrototype> protoId)
    {
        EnterDimension(target, protoId, Vector2.Zero);
    }

    public void EnterDimension(EntityUid target, ProtoId<STDimensionPrototype> protoId, Vector2 worldPos)
    {
        var dimension = GetDimension(protoId);
        if (dimension == null)
            return;

        var mapId = EnsureComp<MapComponent>(dimension.Value.Owner).MapId;

        EnterDimension(target, mapId, worldPos);
    }

    public Entity<STDimensionComponent>? GetDimension(ProtoId<STDimensionPrototype> protoId)
    {
        var prototype = _prototype.Index(protoId);

        var query = EntityQueryEnumerator<STDimensionComponent>();
        while (query.MoveNext(out var uid, out var dimensionComponent))
        {
            if (dimensionComponent.Id != prototype.ID)
                continue;

            return (uid, dimensionComponent);
        }

        if (!_mapLoader.TryLoadMap(
                prototype.MapPath,
                out var map,
                out _,
                DeserializationOptions.Default with { InitializeMaps = true }))
        {
            Log.Error("Failed loading dimension map");
            return null;
        }

        var mapId = map.Value.Comp.MapId;
        var mapUid = _mapSystem.GetMap(mapId);

        var component = EnsureComp<STDimensionComponent>(mapUid);
        component.Id = protoId;

        return (mapUid, component);
    }
}
