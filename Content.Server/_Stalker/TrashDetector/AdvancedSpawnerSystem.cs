using System.Numerics;
using Content.Server.Spawners.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.GameObjects;

namespace Content.Server.Spawners.EntitySystems
{
    /// <summary>
    /// Отдельная система, которая отвечает за спавн предметов через AdvancedRandomSpawnerComponent.
    /// Не изменяет другие спавнеры и работает отдельно.
    /// </summary>
    [UsedImplicitly]
    public sealed class AdvancedSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnAdvancedSpawnerMapInit);
        }

        /// <summary>
        /// Обработчик события MapInitEvent – активирует спавн при загрузке карты.
        /// </summary>
        private void OnAdvancedSpawnerMapInit(EntityUid uid, AdvancedRandomSpawnerComponent component, MapInitEvent args)
        {
            SpawnItems(uid, component);
            if (component.DeleteSpawnerAfterSpawn)
                QueueDel(uid);
        }

        /// <summary>
        /// Логика спавна предметов, учитывая редкость и количество.
        /// </summary>
        private void SpawnItems(EntityUid uid, AdvancedRandomSpawnerComponent component)
        {
            if (Deleted(uid) || component.CommonPrototypes.Count == 0 && component.RarePrototypes.Count == 0 && component.LegendaryPrototypes.Count == 0)
            {
                Log.Warning($"Prototype list in AdvancedRandomSpawnerComponent is empty! Entity: {ToPrettyString(uid)}");
                return;
            }

            var randomPrototypes = component.GetRandomPrototypes(_random);
            if (randomPrototypes.Count == 0)
                return;

            foreach (var proto in randomPrototypes)
            {
                var offset = component.Offset;
                var xOffset = _random.NextFloat(-offset, offset);
                var yOffset = _random.NextFloat(-offset, offset);
                var coordinates = Transform(uid).Coordinates.Offset(new Vector2(xOffset, yOffset));

                EntityManager.SpawnEntity(proto, coordinates);
            }
        }
    }
}
