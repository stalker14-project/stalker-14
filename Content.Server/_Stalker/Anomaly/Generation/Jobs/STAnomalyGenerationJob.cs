using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Stalker.Anomaly.Generation.Components;
using Content.Shared._Stalker.Anomaly.Data;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Anomaly.Generation.Jobs;

public sealed partial class STAnomalyGenerationJob : Job<STAnomalyGenerationJobData>
{
    private static readonly ProtoId<TagPrototype> TagGenerationIntersectionSkip = "STAnomalyGenerationIntersectionSkip";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public readonly STAnomalyGenerationOptions Options;

    private readonly EntityLookupSystem _entityLookup;
    private readonly TagSystem _tag;
    private readonly TransformSystem _transform;
    private readonly MapSystem _map;

    private readonly FrozenDictionary<EntProtoId, int> _anomalySizes;

    private readonly Dictionary<Vector2i, TileRef> _tileCoordinates = new();
    private readonly Dictionary<Vector2i, STAnomalyGenerationTile> _tileCoordinatesSpawn = new();

    public STAnomalyGenerationJob(STAnomalyGenerationOptions options,  double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        Options = options;

        // Include IoC
        IoCManager.InjectDependencies(this);

        // Include entity systems
        _entityLookup = _entityManager.System<EntityLookupSystem>();
        _tag = _entityManager.System<TagSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _map = _entityManager.System<MapSystem>();

        // Hashing
        _anomalySizes = GetHashAnomalySize();
    }

    private FrozenDictionary<EntProtoId, int> GetHashAnomalySize()
    {
        var dictionary = new Dictionary<EntProtoId, int>();

        foreach (var anomalyEntry in Options.AnomalyEntries)
        {
            var entityPrototype = _prototype.Index<EntityPrototype>(anomalyEntry.ProtoId);
            var componentRegistry = entityPrototype.Components;

            if (!componentRegistry.TryGetComponent("Fixtures", out var component))
                continue;

            if (component is not FixturesComponent fixturesComponent)
                continue;

            if (!fixturesComponent.Fixtures.TryGetValue("fix1", out var fixture))
                continue;

            // Here we get the radius of the anomaly, we need to subtract 0.5,
            // which would not take into account the skeleton of the anomaly itself,
            // in fact we turn the volumetric object into an abstract point.
            var size = (int)Math.Max(Math.Round(fixture.Shape.Radius) - 1, 0);

            dictionary.Add(anomalyEntry.ProtoId, size);
        }

        return dictionary.ToFrozenDictionary();
    }

    protected override async Task<STAnomalyGenerationJobData?> Process()
    {
        var result = new STAnomalyGenerationJobData();

        await LoadTiles();
        await RemoveByBlockers();

        for (var i = 0; i < Options.TotalCount; i++)
        {
            var anomaly = GetRandomAnomalyEntry(Options, _random);
            if (anomaly is null)
                continue;

            for (var j = 0; j < 100; j++)
            {
                await MakeOperation();

                var (coords, tile) = _random.Pick(_tileCoordinatesSpawn);
                var entity = await TrySpawn(anomaly.Value, coords);

                if (entity == EntityUid.Invalid)
                    continue;

                // Remove spawn coords from maps
                _tileCoordinatesSpawn.Remove(coords);
                _tileCoordinates.Remove(coords);

                // Anomaly don't spawn in anomalies
                foreach (var takenCoord in GetAnomalyTiles(anomaly.Value, coords))
                {
                    if (!_tileCoordinatesSpawn.ContainsKey(takenCoord))
                        continue;

                    _tileCoordinatesSpawn.Remove(takenCoord);
                }

                result.SpawnedAnomalies.Add(entity);
                break;
            }
        }

        return result;
    }

    private async Task RemoveByBlockers()
    {
        var entities = _entityManager.EntityQueryEnumerator<STAnomalyGeneratorSpawnBlockerComponent, TransformComponent>();
        while (entities.MoveNext(out _, out var blocker, out var transform))
        {
            if (transform.MapID != Options.MapId)
                continue;

            var position = _transform.GetWorldPosition(transform);
            var coordinates = new Vector2i((int) position.X, (int) position.Y);
            var size = blocker.Size;
            var box2 = new Box2i(coordinates.X - size, coordinates.Y - size, coordinates.X + size + 1, coordinates.Y + size);

            for (var x = box2.Left; x < box2.Right; x++)
            {
                for (var y = box2.Bottom; y < box2.Top; y++)
                {
                    await MakeOperation();
                    if (!_tileCoordinates.TryGetValue(new Vector2i(x, y), out _))
                        continue;

                    _tileCoordinates.Remove(new Vector2i(x, y));
                }
            }
        }
    }

    private async Task LoadTiles()
    {
        var grids = _mapManager.GetAllGrids(Options.MapId);
        foreach (var grid in grids)
        {
            foreach (var tileRef in _map.GetAllTiles(grid, grid))
            {
                await MakeOperation();
                if (TileSolidAndNotBlocked(tileRef))
                {
                    _tileCoordinates.TryAdd(tileRef.GridIndices, tileRef);
                    _tileCoordinatesSpawn.TryAdd(tileRef.GridIndices, new STAnomalyGenerationTile(tileRef));
                }

                if (TileSolidAndNotBlocked(tileRef, uid => _tag.HasTag(uid, TagGenerationIntersectionSkip)))
                {
                    _tileCoordinates.TryAdd(tileRef.GridIndices, tileRef);
                }
            }
        }
    }

    private bool TileSolidAndNotBlocked(TileRef tile, Func<EntityUid, bool>? predicate = null)
    {
        return tile.GetContentTileDefinition().Sturdy &&
               !IsTileBlocked(tile, CollisionGroup.LowImpassable, predicate: predicate);
    }

    private static STAnomalyGeneratorAnomalyEntry? GetRandomAnomalyEntry(STAnomalyGenerationOptions options, IRobustRandom random)
    {
        var sumRation = 0f;
        foreach (var entry in options.AnomalyEntries)
        {
            sumRation += entry.Weight;
        }

        var roll = random.NextFloat(0, sumRation);

        sumRation = 0f;
        foreach (var entry in options.AnomalyEntries)
        {
            sumRation += entry.Weight;
            if (roll <= sumRation)
                return entry;
        }

        return options.AnomalyEntries.LastOrDefault();
    }
}
