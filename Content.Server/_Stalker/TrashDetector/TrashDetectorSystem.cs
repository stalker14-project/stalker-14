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

namespace Content.Server.TrashDetector
{
    public sealed partial class TrashDetectorSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly AudioSystem Audio = default!;

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

            var spawnerUid = EntityManager.SpawnEntity(comp.LootSpawner, spawnCoords);
            if (!TryComp<AdvancedRandomSpawnerComponent>(spawnerUid, out var spawner))
            {
                _popupSystem.PopupEntity("Ошибка: спавнер не найден!", spawnerUid, PopupType.Medium);
                return;
            }

            var lootList = spawner.GetFinalizedPrototypes(_random);

            if (lootList.Count > 0)
            {
                _popupSystem.PopupEntity("Прибор пищит! Вы нашли что-то!", uid, PopupType.LargeCaution);
                foreach (var loot in lootList)
                {
                    EntityManager.SpawnEntity(loot, spawnCoords);
                }
            }
            else
            {
                _popupSystem.PopupEntity("Прибор не издает звука", uid, PopupType.Medium);
            }

            args.Handled = true;
        }
    }
}
