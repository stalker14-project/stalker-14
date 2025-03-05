using System.Linq;
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
using Robust.Shared.Localization;
using System.Collections.Generic;

namespace Content.Server.TrashDetector;

public sealed partial class TrashDetectorSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] internal readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly AdvancedRandomSpawnerSystem _spawnerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("TrashDetector");

    private const float SearchRadius = 1.0f;
    private const int AngleStep = 30;
    private const int FullCircle = 360;

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


        var detectorPrototypeId = Comp<MetaDataComponent>(uid).EntityPrototype?.ID;
        if (detectorPrototypeId == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("trash-detector-invalid"), user, PopupType.LargeCaution);
            return;
        }


        if (comp.AllowedDetectors.Count > 0 && !comp.AllowedDetectors.Contains(detectorPrototypeId))
        {
            _popupSystem.PopupEntity(Loc.GetString("trash-detector-not-compatible"), user, PopupType.LargeCaution);
            return;
        }


        if (trash.TimeBeforeNextSearch > 0f)
        {
            _popupSystem.PopupEntity(Loc.GetString("trash-detector-already-checked"), user, PopupType.LargeCaution);
            return;
        }

        var doAfterArgs = new DoAfterArgs(_entityManager, user, comp.SearchTime, new GetTrashDoAfterEvent(), uid, target: target.Value, used: uid)
        {
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 2f,
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
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
        var spawnerUid = _entityManager.SpawnEntity(comp.LootSpawner, spawnCoords);

        if (!TryComp<AdvancedRandomSpawnerComponent>(spawnerUid, out var spawner))
        {
            _sawmill.Warning("Error: Spawner not found! Deleting object.");
            _entityManager.DeleteEntity(spawnerUid);
            return;
        }


        var config = new AdvancedRandomSpawnerConfig(spawner);
        config.ApplyModifiers(comp.WeightModifiers, comp.ExtraPrototypes);

        var spawnedCategories = _spawnerSystem.SpawnEntitiesUsingSpawner(spawnerUid, config);


        var categoryData = new Dictionary<string, (string locKey, float time)>
        {
            { "Legendary", ("trash-detector-legendary", 1200f) },
            { "Rare", ("trash-detector-rare", 900f) },
            { "Common", ("trash-detector-common", 600f) },
            { "Negative", ("trash-detector-negative", 300f) }
        };


        var entry = categoryData.FirstOrDefault(pair => spawnedCategories.Contains(pair.Key));

        string message;
        if (entry.Key == null)
        {
            message = Loc.GetString("trash-detector-no-signal");
        }
        else
        {
            message = Loc.GetString(entry.Value.locKey);
            trash.TimeBeforeNextSearch = entry.Value.time;
            _sawmill.Info($"[TrashDetector] Setting TimeBeforeNextSearch = {entry.Value.time} for entity {args.Args.Target.Value}");
        }

        _popupSystem.PopupEntity(message, uid, PopupType.LargeCaution);
        args.Handled = true;
    }

    private EntityCoordinates FindFreePosition(EntityCoordinates origin)
    {
        for (var i = 0; i < FullCircle; i += AngleStep)
        {
            var angle = i * (float)(System.Math.PI / 180.0);
            var offset = new Vector2(MathF.Cos(angle) * SearchRadius, MathF.Sin(angle) * SearchRadius);
            var testCoords = new EntityCoordinates(origin.EntityId, origin.Position + offset);

            if (!_entityManager.TryGetComponent(testCoords.EntityId, out PhysicsComponent? physics) || !physics.CanCollide)
            {
                return testCoords;
            }
        }
        return origin;
    }
}
