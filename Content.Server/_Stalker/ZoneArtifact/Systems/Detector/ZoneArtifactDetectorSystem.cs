using Content.Server._Stalker.ZoneArtifact.Components;
using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Content.Server._Stalker.ZoneArtifact.Components.Spawner;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneArtifact;
using Content.Shared._Stalker.ZoneArtifact.Components;
using Content.Shared._Stalker.ZoneArtifact.Events;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneArtifact.Systems.Detector;

public sealed class ZoneArtifactDetectorSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ZoneArtifactSpawnerSystem _artifactSpawner = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneArtifactDetectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ZoneArtifactDetectorComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneArtifactDetectorComponent>();
        while (query.MoveNext(out var uid, out var detector))
        {
            if (!Enabled((uid, detector)))
                continue;

            if (_timing.CurTime < detector.UpdateTime)
                continue;

            Update((uid, detector));
            detector.UpdateTime = _timing.CurTime + detector.UpdateInterval;
        }
    }

    public bool Enabled(Entity<ZoneArtifactDetectorComponent> detector)
    {
        return detector.Comp is { Available: true, Enabled: true };
    }

    private void Update(Entity<ZoneArtifactDetectorComponent> detector)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var xform = xformQuery.GetComponent(detector);

        var entities = _entityLookup.GetEntitiesInRange<ZoneArtifactDetectorTargetComponent>(
            _transform.GetMapCoordinates(xform), detector.Comp.DetectionDistance);

        EntityUid? closestEntity = null;
        float? closestDist = null;
        foreach (var entity in entities)
        {
            if (!InMap(entity))
                continue;

            if (!entity.Comp.Detectable)
                continue;

            if (entity.Comp.DetectedLevel > detector.Comp.Level)
                continue;

            var dist = (_transform.GetWorldPosition(xform, xformQuery) - _transform.GetWorldPosition(entity, xformQuery)).Length();

            if (TryComp<ZoneArtifactSpawnerComponent>(entity, out var spawner))
            {
                if (!_artifactSpawner.Ready((entity, spawner)))
                    continue;

                if (detector.Comp.ActivationDistance > dist)
                {
                    if (!_artifactSpawner.TrySpawn((entity, spawner)))
                        continue;
                }
            }

            if (dist > (closestDist ?? float.MaxValue))
                continue;

            closestEntity = entity;
            closestDist = dist;
        }

        SetClosest(detector, closestEntity, closestDist);

        var ev = new ZoneArtifactDetectorUpdateEvent(detector, detector.Comp.ClosestEntity, detector.Comp.ClosestDistance);
        RaiseLocalEvent(detector, ev);
    }

    private void OnUseInHand(Entity<ZoneArtifactDetectorComponent> detector, ref UseInHandEvent args)
    {
        if (!detector.Comp.Available)
            return;

        Toggle(detector, args.User);
    }

    private void OnGotUnequippedHand(Entity<ZoneArtifactDetectorComponent> detector, ref GotUnequippedHandEvent args)
    {
        if (!detector.Comp.Enabled || TryComp<ZoneAnomalyDetectorComponent>(detector, out var anomaly))
            return;

        Toggle(detector);
    }

    private void Toggle(Entity<ZoneArtifactDetectorComponent> detector, EntityUid? user = null)
    {
        detector.Comp.Enabled = !detector.Comp.Enabled;
        detector.Comp.UpdateTime = _timing.CurTime;
        Dirty(detector);

        _appearance.SetData(detector, ZoneArtifactDetectorVisuals.Layer, Enabled(detector) ? ZoneArtifactDetectorVisuals.Enabled : ZoneArtifactDetectorVisuals.Disabled);
    }

    private void SetClosest(Entity<ZoneArtifactDetectorComponent> detector, EntityUid? uid, float? distance)
    {
        var previousClosestEntity = detector.Comp.ClosestEntity;

        detector.Comp.ClosestEntity = uid;
        detector.Comp.ClosestDistance = distance;

        if (uid is { } entityUid && !InMap(entityUid))
        {
            detector.Comp.ClosestEntity = null;
            detector.Comp.ClosestDistance = null;
        }

        Dirty(detector);

        var visuals = ZoneArtifactDetectorVisuals.Detected;
        if (detector.Comp.ClosestEntity is null)
        {
            visuals = Enabled(detector) ? ZoneArtifactDetectorVisuals.Enabled : ZoneArtifactDetectorVisuals.Disabled;
        }

        _appearance.SetData(detector, ZoneArtifactDetectorVisuals.Layer, visuals);

        if (previousClosestEntity == detector.Comp.ClosestEntity)
            return;

        var ev = new ZoneArtifactDetectorSetTargetEvent(detector, detector.Comp.ClosestEntity);
        RaiseLocalEvent(detector, ev);
    }

    private bool InMap(EntityUid entity)
    {
        var parent = Transform(entity).ParentUid;
        return HasComp<MapComponent>(parent) || HasComp<MapGridComponent>(parent);
    }
}
