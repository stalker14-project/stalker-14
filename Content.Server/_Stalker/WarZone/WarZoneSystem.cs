using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._Stalker.WarZone;
using Content.Shared._Stalker.WarZone.Requirenments;
using Content.Server.Database;
using Content.Shared._Stalker.WarZone;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Mobs;
using Content.Shared._Stalker.Bands;
using Content.Shared.NPC.Prototypes;

namespace Content.Server._Stalker.WarZone;

public sealed partial class WarZoneSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly Dictionary<string, float> _bandPoints = new();
    private readonly Dictionary<string, float> _factionPoints = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastRewardTimes = new();

    public IReadOnlyDictionary<string, float> BandPoints => _bandPoints;
    public IReadOnlyDictionary<string, float> FactionPoints => _factionPoints;
    public TimeSpan CurrentTime => _gameTiming.CurTime;

    public void SetBandPoints(string protoId, float points)
    {
        _bandPoints[protoId] = points;
    }

    public void SetFactionPoints(string protoId, float points)
    {
        _factionPoints[protoId] = points;
    }

    public IEnumerable<(EntityUid Uid, WarZoneComponent Component)> GetAllWarZones()
    {
        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
            yield return (uid, comp);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarZoneComponent, ComponentInit>(OnWarZoneInit);
        SubscribeLocalEvent<WarZoneComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<WarZoneComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<MetaDataComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnWarZoneInit(EntityUid uid, WarZoneComponent component, ComponentInit args)
    {
        _ = InitializeWarZoneAsync(uid, component);
    }

    private async Task InitializeWarZoneAsync(EntityUid uid, WarZoneComponent component)
    {
        try
        {
            foreach (var bandProto in _prototypeManager.EnumeratePrototypes<STBandPrototype>())
            {
                var band = await _dbManager.GetStalkerBandAsync(bandProto.ID);
                if (band == null)
                {
                    await _dbManager.SetStalkerBandAsync(bandProto.ID, 0);
                    _bandPoints[bandProto.ID] = 0;
                }
                else
                {
                    _bandPoints[bandProto.ID] = band.RewardPoints;
                }
            }

            foreach (var factionProto in _prototypeManager.EnumeratePrototypes<NpcFactionPrototype>())
            {
                var faction = await _dbManager.GetStalkerFactionAsync(factionProto.ID);
                if (faction == null)
                {
                    await _dbManager.SetStalkerFactionAsync(factionProto.ID, 0);
                    _factionPoints[factionProto.ID] = 0;
                }
                else
                {
                    _factionPoints[factionProto.ID] = faction.RewardPoints;
                }
            }

            component.InitialLoadComplete = false;
            component.PresentBandProtoIds = new();
            component.PresentFactionProtoIds = new();

            _ = LoadInitialZoneStateAsync(uid, component);

            var ownership = await _dbManager.GetStalkerWarOwnershipAsync(component.ZoneProto);
            if (ownership != null && (ownership.BandId != null || ownership.FactionId != null))
            {
                var lastRewardTime = ownership.LastCapturedByCurrentOwnerAt.HasValue
                    ? _gameTiming.CurTime - (DateTime.UtcNow - ownership.LastCapturedByCurrentOwnerAt.Value)
                    : _gameTiming.CurTime;

                _lastRewardTimes[uid] = lastRewardTime;

                Logger.InfoS("warzone", $"Initialized reward timing for zone '{component.PortalName}', owned by {(ownership.BandId != null ? $"band:{ownership.BandId}" : $"faction:{ownership.FactionId}")}");
            }
        }
        catch (Exception ex)
        {
            Logger.ErrorS("warzone", $"Error initializing war zone {component.ZoneProto}: {ex}");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;

        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            _ = UpdateCaptureAsync(uid, comp, now);
        }

        foreach (var (zone, lastRewardTime) in _lastRewardTimes)
        {
            DistributeRewards(zone, lastRewardTime, now);
        }
    }

    private async Task UpdateCaptureAsync(EntityUid zone, WarZoneComponent comp, TimeSpan now)
    {
        var frameTimeSec = (float)_gameTiming.FrameTime.TotalSeconds;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(comp.ZoneProto, out var wzProto))
            return;

        if (comp.PresentBandProtoIds.Count > 1 || comp.PresentFactionProtoIds.Count > 1)
        {
            ResetAllRequirements(zone);
            return;
        }

        if (comp.PresentBandProtoIds.Count == 0 && comp.PresentFactionProtoIds.Count == 0)
        {
            ResetAllRequirements(zone);

            if (comp.CurrentAttackerBandProtoId != null || comp.CurrentAttackerFactionProtoId != null)
            {
                if ((comp.CurrentAttackerBandProtoId != comp.DefendingBandProtoId || comp.CurrentAttackerFactionProtoId != comp.DefendingFactionProtoId) &&
                    (comp.CurrentAttackerBandProtoId != null || comp.CurrentAttackerFactionProtoId != null))
                {
                    string attackerName = GetAttackerName(comp.CurrentAttackerBandProtoId, comp.CurrentAttackerFactionProtoId);
                    _chatManager.DispatchServerAnnouncement(Loc.GetString(
                        "st-warzone-capture-abandoned",
                        ("zone", comp.PortalName ?? "Unknown"),
                        ("attacker", attackerName)));
                }
                comp.CurrentAttackerBandProtoId = null;
                comp.CurrentAttackerFactionProtoId = null;
                comp.PresentBandProtoIds.Clear();
                comp.PresentFactionProtoIds.Clear();
            }

            return;
        }

        string? attackerBand = null;
        string? attackerFaction = null;

        if (comp.PresentBandProtoIds.Count == 1)
            attackerBand = GetFirst(comp.PresentBandProtoIds);
        if (comp.PresentFactionProtoIds.Count == 1)
            attackerFaction = GetFirst(comp.PresentFactionProtoIds);

        if ((attackerBand != null && attackerBand == comp.DefendingBandProtoId) ||
            (attackerFaction != null && attackerFaction == comp.DefendingFactionProtoId))
        {
            ResetAllRequirements(zone);
            return;
        }

        bool isDefender = (attackerBand != null && attackerBand == comp.DefendingBandProtoId) ||
                          (attackerFaction != null && attackerFaction == comp.DefendingFactionProtoId);

        bool zoneCooldownActive = comp.CooldownEndTime.HasValue && now < comp.CooldownEndTime.Value;

        if (!isDefender && !zoneCooldownActive && comp.InitialLoadComplete &&
            (attackerBand != comp.CurrentAttackerBandProtoId || attackerFaction != comp.CurrentAttackerFactionProtoId) &&
            (attackerBand != null || attackerFaction != null))
        {
            comp.CurrentAttackerBandProtoId = attackerBand;
            comp.CurrentAttackerFactionProtoId = attackerFaction;

            string attackerName = GetAttackerName(attackerBand, attackerFaction);
            _chatManager.DispatchServerAnnouncement(Loc.GetString(
                "st-warzone-capture-started",
                ("attacker", attackerName),
                ("zone", comp.PortalName ?? "Unknown")));
        }

        var ownerships = new Dictionary<ProtoId<STWarZonePrototype>, (string? BandProtoId, string? FactionProtoId)>();
        var lastCaptureTimes = new Dictionary<ProtoId<STWarZonePrototype>, DateTime?>();
        var zonePrototypes = new Dictionary<ProtoId<STWarZonePrototype>, STWarZonePrototype>();

        var requiredZoneIds = new HashSet<ProtoId<STWarZonePrototype>>();

        if (wzProto.Requirements != null)
        {
            foreach (var req in wzProto.Requirements)
            {
                if (req is ZoneOwnershipRequirenment zoneReq)
                {
                    foreach (var rid in zoneReq.RequiredZones)
                        requiredZoneIds.Add(rid);
                }
            }
        }

        foreach (var rid in requiredZoneIds)
        {
            var ownership = await _dbManager.GetStalkerWarOwnershipAsync(rid);
            if (ownership != null)
            {
                string? zoneBandProtoId = ownership.Band?.BandProtoId;
                string? zoneFactionProtoId = ownership.Faction?.FactionProtoId;
                ownerships[rid] = (zoneBandProtoId, zoneFactionProtoId);
                lastCaptureTimes[rid] = ownership.LastCapturedByCurrentOwnerAt;
            }

            if (_prototypeManager.TryIndex<STWarZonePrototype>(rid, out var reqProto))
            {
                zonePrototypes[rid] = reqProto;
            }
        }

        var allMet = true;

        if (wzProto.Requirements != null)
        {
            foreach (var req in wzProto.Requirements)
            {
                var blockReason = req.Check(attackerBand, attackerFaction, ownerships, lastCaptureTimes, zonePrototypes, comp.ZoneProto, frameTimeSec);
                if (blockReason != CaptureBlockReason.None)
                {
                    allMet = false;
                    break;
                }
            }
        }

        if (!allMet)
        {
            // Update capture progress based on CaptureTimeRequirenment(s)
            float maxProgress = 0f;
            if (wzProto.Requirements != null)
            {
                foreach (var req in wzProto.Requirements)
                {
                    if (req is CaptureTimeRequirenment timeReq && timeReq.CaptureTime > 0f)
                    {
                        var progress = timeReq.ProgressSeconds / timeReq.CaptureTime;
                        if (progress > maxProgress)
                            maxProgress = progress;
                    }
                }
            }

            // Clamp between 0 and 1
            maxProgress = Math.Clamp(maxProgress, 0f, 1f);
            comp.CaptureProgress = maxProgress;

            return;
        }

        comp.DefendingBandProtoId = attackerBand;
        comp.DefendingFactionProtoId = attackerFaction;

        ProtoId<STBandPrototype>? bandProtoId = null;
        ProtoId<NpcFactionPrototype>? factionProtoId = null;

        if (attackerBand != null)
        {
            bandProtoId = attackerBand;
        }

        if (attackerFaction != null)
        {
            factionProtoId = attackerFaction;
        }

        if (bandProtoId != null && factionProtoId != null)
            bandProtoId = null;

        await _dbManager.SetStalkerZoneOwnershipAsync(
            comp.ZoneProto,
            bandProtoId,
            factionProtoId);

        if (wzProto.CaptureCooldownHours > 0)
        {
            comp.CooldownEndTime = _gameTiming.CurTime + TimeSpan.FromHours(wzProto.CaptureCooldownHours);
        }

        string defenderName = GetAttackerName(comp.DefendingBandProtoId, comp.DefendingFactionProtoId);
        _chatManager.DispatchServerAnnouncement(Loc.GetString(
            "st-warzone-captured",
            ("zone", comp.PortalName ?? "Unknown"),
            ("attacker", defenderName)));

        _lastRewardTimes[zone] = _gameTiming.CurTime;

        // Set capture progress to 100% on successful capture
        comp.CaptureProgress = 1f;
    }

    private void ResetAllRequirements(EntityUid zone)
    {
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
            return;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        if (wzProto.Requirements == null)
            return;

        foreach (var req in wzProto.Requirements)
        {
            if (req is CaptureTimeRequirenment captureReq)
                captureReq.Reset();
        }

        // Reset capture progress
        wzComp.CaptureProgress = 0f;
    }

    private void DistributeRewards(EntityUid zone, TimeSpan lastRewardTime, TimeSpan now)
    {
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
            return;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        var period = TimeSpan.FromSeconds(wzProto.RewardPeriod);

        if (now - lastRewardTime < period)
            return;

        if (wzComp.DefendingBandProtoId == null && wzComp.DefendingFactionProtoId == null && wzProto.ShouldAwardWhenDefenderPresent)
            return;

        var points = wzProto.RewardPointsPerPeriod;
        bool rewarded = false;

        if (wzComp.DefendingBandProtoId != null)
        {
            var bandProtoId = wzComp.DefendingBandProtoId;
            var currentPoints = _bandPoints.TryGetValue(bandProtoId, out var val) ? val : 0;
            var newPoints = currentPoints + (int)points;
            _bandPoints[bandProtoId] = newPoints;
            _dbManager.SetStalkerBandAsync(new ProtoId<STBandPrototype>(bandProtoId), newPoints);
            Logger.InfoS("warzone", $"Awarded {points} points to band {bandProtoId} (total: {newPoints}) for controlling {wzComp.PortalName}");
            rewarded = true;
        }
        else if (wzComp.DefendingFactionProtoId != null)
        {
            var factionProtoId = wzComp.DefendingFactionProtoId;
            var currentPoints = _factionPoints.TryGetValue(factionProtoId, out var val) ? val : 0;
            var newPoints = currentPoints + (int)points;
            _factionPoints[factionProtoId] = newPoints;
            _dbManager.SetStalkerFactionAsync(new ProtoId<NpcFactionPrototype>(factionProtoId), newPoints);
            Logger.InfoS("warzone", $"Awarded {points} points to faction {factionProtoId} (total: {newPoints}) for controlling {wzComp.PortalName}");
            rewarded = true;
        }

        if (rewarded)
        {
            _lastRewardTimes[zone] = now;
        }
    }

    private static string? GetFirst(HashSet<string> set)
    {
        foreach (var g in set)
            return g;
        return null;
    }

    private void OnStartCollide(EntityUid uid, WarZoneComponent component, ref readonly StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return;

        var bandProtoId = bands.BandProto;
        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bandProtoId, out var bandProto))
        {
            bandId = bandProto.ID;

            if (_prototypeManager.TryIndex<NpcFactionPrototype>(bandProto.FactionId, out var factionProto))
            {
                factionId = factionProto.ID;
            }
        }

        if (component.PresentBandProtoIds == null)
            component.PresentBandProtoIds = new();
        if (component.PresentFactionProtoIds == null)
            component.PresentFactionProtoIds = new();

        var now = _gameTiming.CurTime;
        if (component.CooldownEndTime.HasValue && now < component.CooldownEndTime.Value)
        {
            var remainingTime = component.CooldownEndTime.Value - now;
            var portalName = component.PortalName ?? "Unknown Zone";
            var message = Loc.GetString(
                "st-warzone-cooldown",
                ("zone", portalName),
                ("minutes", $"{remainingTime.TotalMinutes:F1}"));
            _popup.PopupEntity(message, other);
        }

        if (bandId != null)
            component.PresentBandProtoIds.Add(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Add(factionId);
    }

    private void OnEndCollide(EntityUid uid, WarZoneComponent component, ref readonly EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return;

        var bandProtoId = bands.BandProto;
        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bandProtoId, out var bandProto))
        {
            bandId = bandProto.ID;

            if (_prototypeManager.TryIndex<NpcFactionPrototype>(bandProto.FactionId, out var factionProto))
            {
                factionId = factionProto.ID;
            }
        }

        if (component.PresentBandProtoIds == null || component.PresentFactionProtoIds == null)
            return;

        if (bandId != null)
            component.PresentBandProtoIds.Remove(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Remove(factionId);
    }

    private void OnEntityTerminating(EntityUid uid, MetaDataComponent component, ref EntityTerminatingEvent args)
    {
        RemoveEntityFromAllCaptures(uid);
    }

    private void RemoveEntityFromAllCaptures(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent(uid, out BandsComponent? bands))
            return;

        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;

            if (_prototypeManager.TryIndex<NpcFactionPrototype>(bandProto.FactionId, out var factionProto))
            {
                factionId = factionProto.ID;
            }
        }

        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var zoneUid, out var wzComp))
        {
            bool changed = false;

            if (wzComp.PresentBandProtoIds != null && bandId != null && wzComp.PresentBandProtoIds.Remove(bandId))
                changed = true;

            if (wzComp.PresentFactionProtoIds != null && factionId != null && wzComp.PresentFactionProtoIds.Remove(factionId))
                changed = true;

            if (changed &&
                wzComp.PresentBandProtoIds != null &&
                wzComp.PresentFactionProtoIds != null &&
                wzComp.PresentBandProtoIds.Count == 0 &&
                wzComp.PresentFactionProtoIds.Count == 0)
            {
                ResetAllRequirements(zoneUid);

                if (wzComp != null && (wzComp.CurrentAttackerBandProtoId != null || wzComp.CurrentAttackerFactionProtoId != null))
                {
                    string attackerName = GetAttackerName(wzComp.CurrentAttackerBandProtoId, wzComp.CurrentAttackerFactionProtoId);
                    _chatManager.DispatchServerAnnouncement(Loc.GetString(
                        "st-warzone-capture-abandoned",
                        ("zone", wzComp.PortalName ?? "Unknown"),
                        ("attacker", attackerName)));
                }

                if (wzComp != null)
                {
                    wzComp.CurrentAttackerBandProtoId = null;
                    wzComp.CurrentAttackerFactionProtoId = null;
                    if (wzComp.PresentBandProtoIds != null)
                        wzComp.PresentBandProtoIds.Clear();
                    if (wzComp.PresentFactionProtoIds != null)
                        wzComp.PresentFactionProtoIds.Clear();
                }
            }
        }
    }

    private string GetAttackerName(string? bandProtoId, string? factionProtoId)
    {
        if (!string.IsNullOrEmpty(bandProtoId))
        {
            if (_prototypeManager.TryIndex<STBandPrototype>(bandProtoId, out var bandProto))
                return bandProto.Name;
        }
        else if (!string.IsNullOrEmpty(factionProtoId))
        {
            if (_prototypeManager.TryIndex<NpcFactionPrototype>(factionProtoId, out var factionProto))
                return factionProto.ID;
        }
        return "Unknown";
    }

    private async Task LoadInitialZoneStateAsync(EntityUid zoneUid, WarZoneComponent component)
    {
        try
        {
            if (!_prototypeManager.TryIndex<STWarZonePrototype>(component.ZoneProto, out var wzProto))
            {
                Logger.ErrorS("warzone", $"Could not find STWarZonePrototype with ID '{component.ZoneProto}' during async state load for zone {zoneUid}.");
                return;
            }

            var ownership = await _dbManager.GetStalkerWarOwnershipAsync(component.ZoneProto);

            if (ownership != null)
            {
                component.DefendingBandProtoId = ownership.BandId != null && ownership.Band != null ? ownership.Band.BandProtoId : null;
                component.DefendingFactionProtoId = ownership.FactionId != null && ownership.Faction != null ? ownership.Faction.FactionProtoId : null;

                if (ownership.LastCapturedByCurrentOwnerAt.HasValue && wzProto.CaptureCooldownHours > 0)
                {
                    DateTime captureTime = ownership.LastCapturedByCurrentOwnerAt.Value;
                    DateTime cooldownEndDateTime = captureTime.AddHours(wzProto.CaptureCooldownHours);
                    DateTime currentDateTime = DateTime.UtcNow;

                    if (cooldownEndDateTime > currentDateTime)
                    {
                        TimeSpan remainingCooldown = cooldownEndDateTime - currentDateTime;
                        component.CooldownEndTime = _gameTiming.CurTime + remainingCooldown;
                    }
                }
            }

            component.InitialLoadComplete = true;
        }
        catch (Exception ex)
        {
            component.InitialLoadComplete = true;
            Logger.ErrorS("warzone", $"Exception during async zone state load for {zoneUid} ({component.ZoneProto}): {ex}");
        }
    }
}