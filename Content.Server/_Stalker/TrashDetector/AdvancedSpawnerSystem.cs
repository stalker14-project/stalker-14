using System.Numerics;
using Content.Server.Spawners.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems
{
    /// <summary>
    /// Отдельная система для спавна предметов через AdvancedRandomSpawnerComponent.
    /// Полностью адаптирована под старый движок, без использования новых методов Robust Engine.
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
            // ✅ Вызываем метод для выбора предметов или "плохих событий"
            var randomPrototypes = component.GetRandomPrototypes(_random);

            // Если предметов нет, проверяем плохие события
            if (randomPrototypes.Length == 0)
            {
                var negativePrototypes = component.GetNegativePrototypes(_random);

                if (negativePrototypes.Length == 0)
                    return; // Ничего не спавним

                foreach (var proto in negativePrototypes)
                {
                    var offset = component.Offset;
                    var xOffset = _random.NextFloat(-offset, offset);
                    var yOffset = _random.NextFloat(-offset, offset);
                    var coordinates = Transform(uid).Coordinates.Offset(new Vector2(xOffset, yOffset));

                    EntityManager.SpawnEntity(proto, coordinates);
                }
            }
            else
            {
                foreach (var proto in randomPrototypes)
                {
                    var offset = component.Offset;
                    var xOffset = _random.NextFloat(-offset, offset);
                    var yOffset = _random.NextFloat(-offset, offset);
                    var coordinates = Transform(uid).Coordinates.Offset(new Vector2(xOffset, yOffset));

                    EntityManager.SpawnEntity(proto, coordinates);
                }
            }

            // Удаляем спавнер, если это требуется
            if (component.DeleteSpawnerAfterSpawn && Exists(uid))
            {
                QueueDel(uid);
            }
        }
    }
}

