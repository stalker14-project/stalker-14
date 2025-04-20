using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Robust.Server.Audio;
using Robust.Shared.Timing;
using ZoneArtifactDetectorComponent = Content.Shared._Stalker.ZoneArtifact.Components.ZoneArtifactDetectorComponent;

namespace Content.Server._Stalker.ZoneArtifact.Systems.Detector;
public sealed class ZoneArtifactDetectorSoundIndicatorSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ZoneArtifactDetectorSystem _artifactDetector = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ZoneArtifactDetectorSoundIndicatorComponent, ZoneArtifactDetectorComponent>();

        while (query.MoveNext(out var uid, out var indicator, out var detector))
        {
            if (!_artifactDetector.Enabled((uid, detector)) || curTime < indicator.NextTime)
                continue;

            if (detector.ClosestEntity == null)
                continue;

            if (!detector.ClosestDistance.HasValue)
                continue;
            var distance = detector.ClosestDistance.Value;

            _audio.PlayPvs(indicator.Sound, uid);

            var detectionDistance = detector.DetectionDistance;
            if (detectionDistance <= 0)
                continue;

            var scalingFactor = distance / detectionDistance;
            if (scalingFactor < 0f)
                scalingFactor = 0f;
            if (scalingFactor > 1f)
                scalingFactor = 1f;

            var intervalRange = indicator.MaxInterval - indicator.MinInterval;
            var interval = intervalRange * scalingFactor + indicator.MinInterval;

            var nextTime = indicator.NextTime + interval;
            if (nextTime < curTime + interval)
                nextTime = curTime + interval;
            indicator.NextTime = nextTime;
        }
    }
}
