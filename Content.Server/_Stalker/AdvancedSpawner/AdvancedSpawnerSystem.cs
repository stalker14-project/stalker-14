using System.Numerics;
using Content.Server.Spawners.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Maths;
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
            var spawnList = new List<string>();

            // Спавним обычные, редкие и легендарные предметы
            spawnList.AddRange(component.GetRandomPrototypes(_random));

            // Если не выпало ничего, пробуем спавнить "плохие события"
            if (spawnList.Count == 0)
            {
                spawnList.AddRange(component.GetNegativePrototypes(_random));

                // Если даже негативные события не выпали - ничего не делаем
                if (spawnList.Count == 0)
                    return;
            }

            // Спавним все выбранные объекты
            foreach (var proto in spawnList)
            {
                var coordinates = GetSpawnCoordinates(uid, component.Offset);
                EntityManager.SpawnEntity(proto, coordinates);
            }

            // Удаляем спавнер после использования, если это указано в настройках
            if (component.DeleteSpawnerAfterSpawn && Exists(uid))
            {
                QueueDel(uid);
            }
        }

        /// <summary>
        /// Получает случайные координаты в радиусе Offset от спавнера.
        /// </summary>
        private EntityCoordinates GetSpawnCoordinates(EntityUid uid, float offset)
        {
            var displacement = _random.NextVector2(-offset, offset);
            return Transform(uid).Coordinates.Offset(displacement);
        }
    }
}
