using System.Threading.Tasks;
using Content.Shared._Stalker.WarZone.Requirenments;
using Robust.Shared.Player;
using Content.Server.Database;
using Content.Shared._Stalker.WarZone;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared._Stalker.Bands;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;

namespace Content.Server._Stalker.WarZone;

public sealed partial class WarZoneSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
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
            // Check if enough time has passed since the last check for this zone
            if (now < comp.NextCheckTime)
                continue;

            // Schedule the next check time
            comp.NextCheckTime = now + TimeSpan.FromSeconds(1);

            // Run the capture update logic, passing 1.0f as the effective frame time
            _ = UpdateCaptureAsync(uid, comp, now, 1.0f);
        }

        foreach (var (zone, lastRewardTime) in _lastRewardTimes)
        {
            DistributeRewards(zone, lastRewardTime, now);
        }
    }

    private async Task UpdateCaptureAsync(EntityUid zone, WarZoneComponent comp, TimeSpan now, float effectiveFrameTime)
    {
        if (!_prototypeManager.TryIndex<STWarZonePrototype>(comp.ZoneProto, out var wzProto))
            return;

        // If multiple entities/factions are present, reset capture progress and attacker info.
        if (comp.PresentEntities.Count > 1)
        {
            // Announce abandonment if there was a single attacker before
            AnnounceCaptureAbandonedLocal(zone, comp);
            ResetCaptureProgress(comp);
            // Keep track of who is present, but don't allow capture progress.
        }

        if (comp.PresentBandProtoIds.Count == 0 && comp.PresentFactionProtoIds.Count == 0)
        {
            ResetCaptureProgress(comp);

            if (comp.CurrentAttackerBandProtoId != null || comp.CurrentAttackerFactionProtoId != null)
                AnnounceCaptureAbandonedLocal(zone, comp);
            return;
        }

        string? attackerBand = null;
        string? attackerFaction = null;

        // Determine the single attacker's band/faction if only one entity is present
        EntityUid? attackerEntity = null;
        if (comp.PresentEntities.Count == 1)
        {
            attackerEntity = GetFirstEntity(comp.PresentEntities);
        }

        if (comp.PresentBandProtoIds.Count == 1)
            attackerBand = GetFirst(comp.PresentBandProtoIds);
        if (comp.PresentFactionProtoIds.Count == 1)
            attackerFaction = GetFirst(comp.PresentFactionProtoIds);

        // If the only present entity is the defender, do nothing.

        if ((attackerBand != null && attackerBand == comp.DefendingBandProtoId) ||
            (attackerFaction != null && attackerFaction == comp.DefendingFactionProtoId))
        {
            ResetAllRequirements(zone);
            return;
        }

        // Check for cooldown *before* checking requirements
        bool zoneCooldownActive = comp.CooldownEndTime.HasValue && now < comp.CooldownEndTime.Value;
        if (zoneCooldownActive)
        {
            if (attackerEntity.HasValue && comp.CooldownEndTime != null) // Show popup only if a single potential attacker is present
            {
                // Use .Value directly as HasValue guarantees non-null here.
                var remainingTime = comp.CooldownEndTime.Value - now;
                var portalName = comp.PortalName ?? "Unknown Zone";
                var message = Loc.GetString(
                    "st-warzone-cooldown",
                    ("zone", portalName),
                    ("minutes", $"{remainingTime.TotalMinutes:F1}"));
                _popup.PopupEntity(message, attackerEntity.Value);
            }
            return; // Block capture attempt due to cooldown
        }

        // Check if it's a new, valid, non-defending attacker starting the capture.
        bool isNewAttacker = (attackerBand != comp.CurrentAttackerBandProtoId || attackerFaction != comp.CurrentAttackerFactionProtoId);
        bool isValidAttacker = (attackerBand != null || attackerFaction != null);
        bool isNotDefender = !((attackerBand != null && attackerBand == comp.DefendingBandProtoId) || (attackerFaction != null && attackerFaction == comp.DefendingFactionProtoId));

        if (comp.InitialLoadComplete && isValidAttacker && isNotDefender && isNewAttacker)
        {
            comp.CurrentAttackerBandProtoId = attackerBand;
            comp.CurrentAttackerFactionProtoId = attackerFaction;
            AnnounceCaptureStartedLocal(zone, comp, attackerBand, attackerFaction);
        }

        // Prepare data for requirement checks
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

        // Define the feedback callback using PopupSystem
        Action<EntityUid, string, (string, object)[]?> feedbackCallback =
            (entity, locId, args) => _popup.PopupEntity(Loc.GetString(locId, args ?? Array.Empty<(string, object)>()), entity);

        // Check all requirements
        var allMet = true;

        if (wzProto.Requirements != null)
        {
            foreach (var req in wzProto.Requirements)
            {
                var blockReason = req.Check(
                    attackerBand,
                    attackerFaction,
                    ownerships,
                    lastCaptureTimes,
                    zonePrototypes,
                    comp.ZoneProto,
                    effectiveFrameTime,
                    attackerEntity,
                    feedbackCallback);
                if (blockReason != CaptureBlockReason.None)
                {
                    allMet = false;
                    break;
                }
            }
        }

        if (!allMet)
        {
            return;
        }

        // Update capture progress using the prototype's CaptureTime
        comp.CaptureProgressTime += effectiveFrameTime;
        comp.CaptureProgress = Math.Clamp(comp.CaptureProgressTime / wzProto.CaptureTime, 0f, 1f);

        // If we haven't reached the required capture time yet, return
        if (comp.CaptureProgressTime < wzProto.CaptureTime)
            return;

        // Requirements and capture time met! Set the new defender.
        // Local announcement moved earlier and into its own method.

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

        // Announce successful capture server-wide
        string defenderName = GetAttackerName(comp.DefendingBandProtoId, comp.DefendingFactionProtoId);
        _chatManager.DispatchServerAnnouncement(Loc.GetString(
            "st-warzone-captured",
            ("zone", comp.PortalName ?? "Unknown"),
            ("attacker", defenderName)));

        _lastRewardTimes[zone] = _gameTiming.CurTime;

        comp.CaptureProgress = 1f;
    }

    private void ResetCaptureProgress(WarZoneComponent comp)
    {
        comp.CaptureProgressTime = 0f;
        comp.CaptureProgress = 0f;
    }

    private void ResetAllRequirements(EntityUid zone)
    {
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
            return;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        if (wzProto.Requirements == null)
            return;

        ResetCaptureProgress(wzComp);
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

    private static EntityUid? GetFirstEntity(HashSet<EntityUid> set)
    {
        foreach (var entity in set)
            return entity;
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
        if (component.PresentEntities == null)
            component.PresentEntities = new();

        if (bandId != null)
            component.PresentBandProtoIds.Add(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Add(factionId);
        component.PresentEntities.Add(other);
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

        if (component.PresentBandProtoIds == null || component.PresentFactionProtoIds == null || component.PresentEntities == null)
            return;

        if (bandId != null)
            component.PresentBandProtoIds.Remove(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Remove(factionId);
        component.PresentEntities.Remove(other);
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
            if (wzComp.PresentEntities != null && wzComp.PresentEntities.Remove(uid))
                changed = true;

            if (changed &&
                wzComp.PresentBandProtoIds != null &&
                wzComp.PresentFactionProtoIds != null &&
                wzComp.PresentEntities != null &&
                wzComp.PresentBandProtoIds.Count == 0 &&
                wzComp.PresentFactionProtoIds.Count == 0 &&
                wzComp.PresentEntities.Count == 0) // Only reset/announce if zone is truly empty
            {
                ResetAllRequirements(zoneUid);
                AnnounceCaptureAbandonedLocal(zoneUid, wzComp);
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

    // Helper to announce capture abandonment locally using ChatSystem
    private void AnnounceCaptureAbandonedLocal(EntityUid zoneUid, WarZoneComponent? wzComp)
    {
        if (wzComp == null || (wzComp.CurrentAttackerBandProtoId == null && wzComp.CurrentAttackerFactionProtoId == null))
            return;

        if (wzComp.CurrentAttackerBandProtoId != wzComp.DefendingBandProtoId || wzComp.CurrentAttackerFactionProtoId != wzComp.DefendingFactionProtoId)
        {
            string attackerName = GetAttackerName(wzComp.CurrentAttackerBandProtoId, wzComp.CurrentAttackerFactionProtoId);
            var message = Loc.GetString("st-warzone-capture-abandoned", ("zone", wzComp.PortalName ?? "Unknown"), ("attacker", attackerName));
            var mapCoords = _transformSystem.GetMapCoordinates(zoneUid);
            var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
            _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, message, zoneUid, false, true, colorOverride: null);
        }

        // Reset attacker info
        wzComp.CurrentAttackerBandProtoId = null;
        wzComp.CurrentAttackerFactionProtoId = null;
    }

    private void AnnounceCaptureStartedLocal(EntityUid zoneUid, WarZoneComponent wzComp, string? attackerBand, string? attackerFaction)
    {
        // We assume the checks for validity (new attacker, not defender, etc.) are done before calling this.
        string attackerName = GetAttackerName(attackerBand, attackerFaction);
        var message = Loc.GetString("st-warzone-capture-started", ("attacker", attackerName), ("zone", wzComp.PortalName ?? "Unknown"));
        var mapCoords = _transformSystem.GetMapCoordinates(zoneUid); // Convert to MapCoordinates
        var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, message, zoneUid, false, true, colorOverride: null);
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
