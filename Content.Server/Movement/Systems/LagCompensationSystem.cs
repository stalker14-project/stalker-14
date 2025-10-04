using Content.Server.Movement.Components;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed class LagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    public TimeSpan BufferTime = TimeSpan.FromMilliseconds(1250);

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
        Subs.CVar(_config, CCVars.LagCompBufferTimeMs, OnBufferTimeChanged, true);
    }

    private void OnBufferTimeChanged(int bufferTimeMs)
    {
        BufferTime = TimeSpan.FromMilliseconds(bufferTimeMs);
        Log.Info($"Lag compensation buffer time changed to: {bufferTimeMs} ms");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;
        var maxIdle = BufferTime * 2;

        // Cull any old ones from active updates, but only for recently active entities
        var query = AllEntityQuery<LagCompensationComponent>();
        while (query.MoveNext(out var comp))
        {
            if (curTime - comp.LastMoveTime > maxIdle)
                continue; // Skip idle entities
            while (comp.Positions.TryPeek(out var pos))
            {
                if (pos.Item1 < earliestTime)
                {
                    comp.Positions.Dequeue();
                    continue;
                }
                break;
            }
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
        component.LastMoveTime = _timing.CurTime;
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (pSession == null || !TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        var angle = Angle.Zero;
        var coordinates = EntityCoordinates.Invalid;
        var ping = pSession.Channel.Ping;
        // Use 1.5 due to the trip buffer.
        var sentTime = _timing.CurTime - TimeSpan.FromMilliseconds(ping * 1.5);

        foreach (var pos in lag.Positions)
        {
            coordinates = pos.Item2;
            angle = pos.Item3;
            if (pos.Item1 >= sentTime)
                break;
        }

        if (coordinates == default)
        {
            coordinates = xform.Coordinates;
            angle = xform.LocalRotation;
        }

        return (coordinates, angle);
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}
