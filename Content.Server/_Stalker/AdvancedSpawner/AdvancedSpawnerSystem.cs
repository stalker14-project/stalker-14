using System.Numerics;
using Content.Server.Spawners.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;

namespace Content.Server.Spawners.EntitySystems
{
    public sealed class AdvancedSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnAdvancedSpawnerMapInit);
        }

        private void OnAdvancedSpawnerMapInit(EntityUid uid, AdvancedRandomSpawnerComponent component, MapInitEvent args)
        {
            var spawnList = component.GetFinalizedPrototypes(_random);
            if (spawnList.Count == 0) return;
            foreach (var proto in spawnList)
            {
                var coordinates = GetSpawnCoordinates(uid, component.Offset);
                EntityManager.SpawnEntity(proto, coordinates);
            }
            if (component.DeleteSpawnerAfterSpawn && Exists(uid))
            {
                QueueDel(uid);
            }
        }

        private EntityCoordinates GetSpawnCoordinates(EntityUid uid, float offset)
        {
            if (offset <= 0)
            {
                return Transform(uid).Coordinates;
            }
            float randomOffsetX = _random.NextFloat(-offset, offset);
            float randomOffsetY = _random.NextFloat(-offset, offset);
            var displacement = new Vector2(randomOffsetX, randomOffsetY);
            return Transform(uid).Coordinates.Offset(displacement);
        }
    }
}
