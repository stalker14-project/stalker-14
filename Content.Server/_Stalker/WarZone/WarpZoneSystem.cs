using Content.Server.Database;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker.WarZone;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.WarZone;
public sealed class WarpZoneSystem : SharedWarZoneSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _lastUpdateTime;

    public void Initialise()
    {
        SubscribeLocalEvent<WarZoneComponent, ComponentInit>(OnInit);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _lastUpdateTime.Add(TimeSpan.FromSeconds(2f))) // Decrease it to increase check frequency
            return;
        _lastUpdateTime = _timing.CurTime;

        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var uid, out var warZone))
        {
            if (_protoMan.TryIndex(warZone.ZoneProto.Id, out STWarZonePrototype? zoneProto) && zoneProto?.Requirements != null)
            {
                foreach(var requirenment in zoneProto.Requirements)
                {
                    requirenment.Check(_entityManager, _protoMan, out var reason);
                }
            }
        }

    }

    private void OnInit(Entity<WarZoneComponent> entity, ref ComponentInit args)
    {
        var ownership = _dbManager.GetStalkerWarOwnershipAsync(entity.Comp.ZoneProto).GetAwaiter().GetResult();

        if (ownership?.Band != null)
        {
            StalkerBand band = ownership.Band;
            ProtoId<STBandPrototype> bandProtoId = new(ownership.Band.BandProtoId);
            if (bandProtoId != default)
            {
                _dbManager.GetStalkerBandAsync(bandProtoId).GetAwaiter().GetResult();
            }
        }

    }
}
