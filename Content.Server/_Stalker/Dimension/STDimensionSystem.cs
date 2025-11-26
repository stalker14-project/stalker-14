using System.Numerics;
using Content.Shared._Stalker.Dimension;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Dimension;

public sealed class STDimensionSystem : STSharedDimensionSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public void EnterDimension(EntityUid target, ProtoId<STDimensionPrototype> protoId)
    {
        EnterDimension(target, protoId, Vector2.Zero);
    }

    public void EnterDimension(EntityUid target, ProtoId<STDimensionPrototype> protoId, Vector2 worldPos)
    {
        var dimension = GetDimension(protoId);
        var mapId = EnsureComp<MapComponent>(dimension).MapId;

        EnterDimension(target, mapId, worldPos);
    }

    public Entity<STDimensionComponent> GetDimension(ProtoId<STDimensionPrototype> protoId)
    {
        var prototype = _prototype.Index(protoId);

        var query = EntityQueryEnumerator<STDimensionComponent>();
        while (query.MoveNext(out var uid, out var dimensionComponent))
        {
            if (dimensionComponent.Id != prototype.ID)
                continue;

            return (uid, dimensionComponent);
        }

        var mapId = _map.CreateMap();

        if (!_mapLoader.TryLoad(mapId, prototype.MapPath.ToString(), out _))
            Log.Error("Failed loading dimension map");

        if (!_map.IsMapInitialized(mapId))
            _map.DoMapInitialize(mapId);

        var mapUid = _map.GetMapEntityId(mapId);

        var component = EnsureComp<STDimensionComponent>(mapUid);
        component.Id = protoId;

        return (mapUid, component);
    }
}
