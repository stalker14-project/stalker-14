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

            // 🔹 Модифицируем веса категорий (вероятность выбора категории)
            spawner.CategoryWeights["Common"] += comp.CommonWeightMod;
            spawner.CategoryWeights["Rare"] += comp.RareWeightMod;
            spawner.CategoryWeights["Legendary"] += comp.LegendaryWeightMod;
            spawner.CategoryWeights["Negative"] += comp.NegativeWeightMod;

            // 🔹 Добавляем список предметов в категории, сохраняя их количество (Count)
            foreach (var entry in comp.ExtraCommonPrototypes)
                spawner.CommonPrototypes.Add(new SpawnEntry { PrototypeId = entry.PrototypeId, Weight = entry.Weight, Count = entry.Count });

            foreach (var entry in comp.ExtraRarePrototypes)
                spawner.RarePrototypes.Add(new SpawnEntry { PrototypeId = entry.PrototypeId, Weight = entry.Weight, Count = entry.Count });

            foreach (var entry in comp.ExtraLegendaryPrototypes)
                spawner.LegendaryPrototypes.Add(new SpawnEntry { PrototypeId = entry.PrototypeId, Weight = entry.Weight, Count = entry.Count });

            foreach (var entry in comp.ExtraNegativePrototypes)
                spawner.NegativePrototypes.Add(new SpawnEntry { PrototypeId = entry.PrototypeId, Weight = entry.Weight, Count = entry.Count });

            // 🔹 Запускаем спавн
            _spawnerSystem.TrySpawnEntities(spawnerUid, spawner);

            // 🔹 Определяем сообщение и звук прибора
            string message = "Прибор не издает звука";
            PopupType popupType = PopupType.LargeCaution;

            // Получаем категорию спавна (берём самую высокую вероятность)
            var highestCategory = spawner.CategoryWeights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "Common";

            switch (highestCategory)
            {
                case "Legendary":
                    message = "Прибор пищит очень громко! Что-то ценное!";
                    popupType = PopupType.LargeCaution;
                    break;
                case "Rare":
                    message = "Прибор подает заметный сигнал. Неплохо!";
                    popupType = PopupType.MediumCaution;
                    break;
                case "Common":
                    message = "Прибор слабо пищит. Ничего особенного.";
                    popupType = PopupType.SmallCaution;
                    break;
                case "Negative":
                    message = "Прибор издает странный звук… Ты привлек внимание мутанта!";
                    popupType = PopupType.LargeCaution;
                    break;
            }

            // 🔹 Отображаем сообщение
            _popupSystem.PopupEntity(message, uid, popupType);

            args.Handled = true;
        }
    }
}
