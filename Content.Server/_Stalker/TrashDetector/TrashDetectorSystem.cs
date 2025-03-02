using Content.Server._Stalker.AdvancedSpawner;
using Content.Server.TrashDetector.Components;
using Content.Server.Popups;
using Robust.Shared.Random;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Server.TrashSearchable;
using Content.Shared.TrashDetector;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Server.Player;
using Robust.Shared.Timing;
using Content.Shared.Physics;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Server.TrashDetector
{
    public sealed partial class TrashDetectorSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly AdvancedRandomSpawnerSystem _spawnerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        private readonly ISawmill _sawmill = Logger.GetSawmill("TrashDetector");

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

        private void OnUse(EntityUid uid, TrashDetectorComponent comp, EntityUid? target, EntityUid user)
        {
            if (target == null || !TryComp<TrashSearchableComponent>(target.Value, out var trash))
                return;

            if (trash.TimeBeforeNextSearch <= 0f)
            {
                var doAfterArgs = new DoAfterArgs(_entityManager, user, comp.SearchTime, new GetTrashDoAfterEvent(), uid, target: target.Value, used: uid)
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

            if (!TryComp<TransformComponent>(args.Args.Target.Value, out var targetTransform))
                return;

            var spawnCoords = FindFreePosition(targetTransform.Coordinates);
            var spawnerUid = _entityManager.SpawnEntity(TrashDetectorComponent.LootSpawner, spawnCoords);

            if (!TryComp<AdvancedRandomSpawnerComponent>(spawnerUid, out var spawner))
            {
                _sawmill.Warning("Ошибка: спавнер не найден! Удаляем объект.");
                _entityManager.DeleteEntity(spawnerUid);
                return;
            }

            var config = new AdvancedRandomSpawnerConfig(spawner);
            config.ApplyModifiers(comp);

            var spawnedCategories = _spawnerSystem.SpawnEntitiesUsingSpawner(spawnerUid, config);
            string message = "Прибор не издает звука";
            PopupType popupType = PopupType.LargeCaution;

            if (spawnedCategories.Contains("Legendary"))
            {
                message = "Прибор пищит очень громко! Что-то ценное!";
                popupType = PopupType.LargeCaution;
                trash.TimeBeforeNextSearch = 1200f; // 20 минут
            }
            else if (spawnedCategories.Contains("Rare"))
            {
                message = "Прибор подает заметный сигнал. Неплохо!";
                popupType = PopupType.MediumCaution;
                trash.TimeBeforeNextSearch = 900f; // 15 минут
            }
            else if (spawnedCategories.Contains("Common"))
            {
                message = "Прибор слабо пищит. Ничего особенного.";
                popupType = PopupType.SmallCaution;
                trash.TimeBeforeNextSearch = 600f; // 10 минут
            }
            else if (spawnedCategories.Contains("Negative"))
            {
                message = "Прибор издает странный звук… Ты привлек внимание мутанта!";
                popupType = PopupType.LargeCaution;
                trash.TimeBeforeNextSearch = 300f; // 5 минут
            }

            _popupSystem.PopupEntity(message, uid, popupType);
            args.Handled = true;
        }

        private EntityCoordinates FindFreePosition(EntityCoordinates origin)
        {
            var radius = 1.0f;
            for (int i = 0; i < 360; i += 30)
            {
                var angle = i * (float)(System.Math.PI / 180.0);
                var offset = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                var testCoords = new EntityCoordinates(origin.EntityId, origin.Position + offset);

                if (!_entityManager.TryGetComponent(testCoords.EntityId, out PhysicsComponent? physics) || !physics.CanCollide)
                {
                    return testCoords;
                }
            }
            return origin;
        }

        private void SnapToGrid(EntityUid entity)
        {
            if (!TryComp<TransformComponent>(entity, out var transform))
                return;

            var newCoords = new EntityCoordinates(transform.GridUid ?? EntityUid.Invalid, MathF.Round(transform.Coordinates.X), MathF.Round(transform.Coordinates.Y));
            _transformSystem.SetCoordinates(entity, newCoords);
        }
    }
}

