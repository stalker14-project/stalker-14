using Content.Server.TrashDetector.Components;
using Content.Server.Popups;
using Robust.Shared.Random;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Server.Audio;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.TrashDetector;

// Используем правильное имя компонента TrashSearchableComponent
using Content.Server.TrashSearchable;

// Используем AdvancedRandomSpawnerComponent
using Content.Server.Spawners.Components;
using System.Numerics;

namespace Content.Server.TrashDetector;

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
        if (target == null)
            return;

        if (!TryComp<TrashSearchableComponent>(target, out var trash))
            return;

        if (trash.TimeBeforeNextSearch < 0f)
        {
            if (!_random.Prob(comp.CommonProbability + comp.RareProbability + comp.LegendaryProbability + comp.NegativeProbability))
            {
                _popupSystem.PopupEntity("Прибор не реагирует.", user, PopupType.LargeCaution);
                return;
            }

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

        var spawnerCoords = Transform(uid).Coordinates;
        var spawnerUid = EntityManager.SpawnEntity(comp.LootSpawner, spawnerCoords);

        if (!TryComp<AdvancedRandomSpawnerComponent>(spawnerUid, out var advSpawner))
        {
            _popupSystem.PopupEntity("Ошибка: спавнер не найден!", uid, PopupType.Medium);
            return;
        }

        var spawnList = advSpawner.GetRandomPrototypes(_random);

        if (spawnList.Count == 0)
        {
            spawnList.AddRange(advSpawner.GetNegativePrototypes(_random));
            if (spawnList.Count == 0)
            {
                _popupSystem.PopupEntity("Ничего не найдено", uid, PopupType.LargeCaution);
                return;
            }
        }

        const float offsetRadius = 1.0f;
        var baseCoords = Transform(uid).Coordinates;

        foreach (var proto in spawnList)
        {
            var displacement = new Vector2(
                _random.NextFloat(-offsetRadius, offsetRadius),
                _random.NextFloat(-offsetRadius, offsetRadius)
            );
            var finalCoords = baseCoords.Offset(displacement);

            EntityManager.SpawnEntity(proto, finalCoords);
        }

        if (advSpawner.DeleteSpawnerAfterSpawn && EntityManager.EntityExists(spawnerUid))
        {
            QueueDel(spawnerUid);
        }

        args.Handled = true;
    }
}
