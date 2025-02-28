using Content.Server._Stalker.AdvancedSpawner;
using Content.Server.TrashDetector.Components;
using Content.Server.Popups;
using Robust.Shared.Random;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Server.Audio;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Server.TrashSearchable;
using Content.Shared.TrashDetector;
using Content.Server.Spawners.Components;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server.TrashDetector
{
    public sealed partial class TrashDetectorSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly AdvancedRandomSpawnerSystem _spawnerSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TrashDetectorComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<TrashDetectorComponent, GetTrashDoAfterEvent>(OnDoAfter);
        }

        private void OnUseInHand(EntityUid uid, TrashDetectorComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;

            OnUse(uid, comp, args.Target, args.User);
        }

        private void OnUse(EntityUid? uid, TrashDetectorComponent comp, EntityUid? target, EntityUid user)
        {
            if (target == null || !TryComp<TrashSearchableComponent>(target, out var trash))
                return;

            if (trash.TimeBeforeNextSearch < 0f)
            {
                var doAfterArgs = new DoAfterArgs(_entityManager, user, comp.SearchTime, new GetTrashDoAfterEvent(), uid, target: target, used: uid)
                {
                    BreakOnDamage = true,
                    NeedHand = true,
                    DistanceThreshold = 2f,
                };

                _doAfterSystem.TryStartDoAfter(doAfterArgs);
            }
            else
            {
                _popupSystem.PopupEntity("Эту кучу уже недавно проверяли", user, PopupType.LargeCaution);
            }
        }

        private void OnDoAfter(EntityUid uid, TrashDetectorComponent comp, GetTrashDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target == null)
                return;

            if (!TryComp<TrashSearchableComponent>(args.Args.Target.Value, out var trash))
                return;

            trash.TimeBeforeNextSearch = 900f;
            var spawnCoords = Transform(args.Args.Target.Value).Coordinates;

            // Создаем спавнер
            var spawnerUid = _entityManager.SpawnEntity(TrashDetectorComponent.LootSpawner, spawnCoords);
            if (!TryComp<AdvancedRandomSpawnerComponent>(spawnerUid, out var spawner))
            {
                _popupSystem.PopupEntity("Ошибка: спавнер не найден!", spawnerUid, PopupType.Medium);
                return;
            }

            // Проверяем, существует ли категория, к которой относится дополнительный предмет
            if (!spawner.CategoryWeights.ContainsKey(comp.ExtraPrototypeCategory))
            {
                _popupSystem.PopupEntity($"Ошибка: категория {comp.ExtraPrototypeCategory} не найдена!", spawnerUid, PopupType.Medium);
                return;
            }

            // Увеличиваем вес категории, связанной с данным детектором
            spawner.CategoryWeights[comp.ExtraPrototypeCategory] += comp.AdditionalSpawnWeight;

            // Если у детектора есть доп. предмет, добавляем его в нужную категорию
            if (!string.IsNullOrEmpty(comp.ExtraPrototype))
            {
                var categoryList = GetPrototypeListByCategory(comp.ExtraPrototypeCategory, spawner);

                if (!categoryList.Any(x => x.PrototypeId == comp.ExtraPrototype))
                {
                    categoryList.Add(new SpawnEntry { PrototypeId = comp.ExtraPrototype, Weight = 1 });
                }
            }

            // Вызываем спавн через AdvancedRandomSpawnerSystem
            _spawnerSystem.TrySpawnEntities(spawnerUid, spawner);

            args.Handled = true;
        }

        /// <summary>
        /// Получает список прототипов для указанной категории.
        /// </summary>
        private List<SpawnEntry> GetPrototypeListByCategory(string category, AdvancedRandomSpawnerComponent spawner)
        {
            return category switch
            {
                "Common" => spawner.CommonPrototypes,
                "Rare" => spawner.RarePrototypes,
                "Legendary" => spawner.LegendaryPrototypes,
                "Negative" => spawner.NegativePrototypes,
                _ => new List<SpawnEntry>(), // Если категория не найдена, возвращаем пустой список
            };
        }
    }
}
