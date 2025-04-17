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
using System.Linq;

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
            // Initialize Band Points
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

            // Initialize Faction Points
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
            component.PresentFactionProtoIds = new(); // Keep this for collision logic, but derive factions for capture logic
            component.PresentEntities = new();

            _ = LoadInitialZoneStateAsync(uid, component);

            // Initialize Last Reward Time based on DB ownership
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

        // Distribute rewards separately
        foreach (var (zone, lastRewardTime) in _lastRewardTimes)
        {
            DistributeRewards(zone, lastRewardTime, now);
        }
    }

    private void ResetCaptureProgress(WarZoneComponent comp)
    {
        comp.CaptureProgressTime = 0f;
        comp.CaptureProgress = 0f;
        comp.LastAnnouncedProgressStep = 0;
    }

    private async Task UpdateCaptureAsync(EntityUid zone, WarZoneComponent comp, TimeSpan now, float effectiveFrameTime)
    {
        if (!comp.InitialLoadComplete || !_prototypeManager.TryIndex<STWarZonePrototype>(comp.ZoneProto, out var wzProto))
            return;

        // --- New Capture Logic ---

        // 0. Determine Present Bands and Factions
        var presentBands = comp.PresentBandProtoIds; // Use the set populated by collision events
        var presentFactions = new HashSet<string>();
        if (presentBands != null)
        {
            foreach (var bandId in presentBands)
            {
                if (_prototypeManager.TryIndex<STBandPrototype>(bandId, out var bandProto) && bandProto.FactionId != default)
                {
                    presentFactions.Add(bandProto.FactionId);
                }
            }
        }

        // Get an entity for popups (doesn't matter which one in cooperative scenarios)
        EntityUid? feedbackEntity = GetFirstEntity(comp.PresentEntities);

        // --- Scenario Handling ---

        string? currentAttackerBand = null;
        string? currentAttackerFaction = null;
        bool proceedWithCapture = false;
        bool awardToFaction = false; // Flag to indicate if ownership should go to faction

        // Scenario 1: No Bands Present
        if (presentBands == null || presentBands.Count == 0)
        {
            if (comp.CurrentAttackerBandProtoId != null || comp.CurrentAttackerFactionProtoId != null)
            {
                AnnounceCaptureAbandonedLocal(zone, comp, "No attackers present");
                ResetCaptureProgress(comp);
                comp.CurrentAttackerBandProtoId = null;
                comp.CurrentAttackerFactionProtoId = null;
            }
            return;
        }
        // Scenario 2: Single Band Present
        else if (presentBands.Count == 1)
        {
            currentAttackerBand = GetFirst(presentBands);
            if (currentAttackerBand != null && _prototypeManager.TryIndex<STBandPrototype>(currentAttackerBand, out var bandProto))
            {
                currentAttackerFaction = bandProto.FactionId; // Faction associated with the single band
                proceedWithCapture = true;
                awardToFaction = false; // Award to the band
            }
        }
        // Scenario 3: Multiple Bands Present
        else // presentBands.Count > 1
        {
            // Subcase 3a: Single Faction (Cooperation)
            if (presentFactions.Count == 1)
            {
                currentAttackerFaction = GetFirst(presentFactions);
                currentAttackerBand = null; // Explicitly no single band attacker
                proceedWithCapture = true;
                awardToFaction = true; // Award to the faction
            }
            // Subcase 3b: Multiple Factions (Conflict)
            else
            {
                if (comp.CurrentAttackerBandProtoId != null || comp.CurrentAttackerFactionProtoId != null)
                {
                    AnnounceCaptureAbandonedLocal(zone, comp, "Conflicting forces present");
                    ResetCaptureProgress(comp);
                    comp.CurrentAttackerBandProtoId = null;
                    comp.CurrentAttackerFactionProtoId = null;
                }
                return; // Conflict stops capture
            }
        }

        // --- Capture Progression ---

        if (!proceedWithCapture || (currentAttackerBand == null && currentAttackerFaction == null))
        {
            // Should not happen if logic above is correct, but safety check
            Logger.WarningS("warzone", $"Zone '{comp.PortalName}': Capture logic reached invalid state. Bands: {presentBands.Count}, Factions: {presentFactions.Count}. AttackerBand: {currentAttackerBand}, AttackerFaction: {currentAttackerFaction}");
            return;
        }

        // Check if the attacker (band or faction) is the current defender
        if ((!awardToFaction && currentAttackerBand != null && currentAttackerBand == comp.DefendingBandProtoId) ||
            (awardToFaction && currentAttackerFaction != null && currentAttackerFaction == comp.DefendingFactionProtoId))
        {
            // Defender is present alone or with allies, no capture progress change needed.
            // Reset requirements state if necessary (though capture isn't progressing anyway)
            ResetAllRequirements(zone);
            return;
        }

        // Check Cooldown
        bool zoneCooldownActive = comp.CooldownEndTime.HasValue && now < comp.CooldownEndTime.Value;
        if (zoneCooldownActive)
        {
            if (feedbackEntity.HasValue && comp.CooldownEndTime.HasValue)
            {
                var remainingTime = comp.CooldownEndTime.Value - now;
                var portalName = comp.PortalName ?? "Unknown Zone";
                var message = Loc.GetString("st-warzone-cooldown", ("zone", portalName), ("minutes", $"{remainingTime.TotalMinutes:F1}"));
                _popup.PopupEntity(message, feedbackEntity.Value);
            }
            return; // Block capture attempt due to cooldown
        }

        // Check for New Attacker (Band or Faction)
        bool isNewAttacker = (currentAttackerBand != comp.CurrentAttackerBandProtoId || currentAttackerFaction != comp.CurrentAttackerFactionProtoId);
        if (isNewAttacker)
        {
            ResetCaptureProgress(comp);
             comp.CurrentAttackerBandProtoId = currentAttackerBand;
             comp.CurrentAttackerFactionProtoId = currentAttackerFaction;
             // Announce based on who is attacking (band or faction)
             string attackerName = GetAttackerName(currentAttackerBand, currentAttackerFaction);
             AnnounceCaptureStartedLocal(zone, comp, attackerName);
        }

        // Check Requirements
        // Prepare data for requirement checks
        var ownerships = new Dictionary<ProtoId<STWarZonePrototype>, (string? BandProtoId, string? FactionProtoId)>();
        var lastCaptureTimes = new Dictionary<ProtoId<STWarZonePrototype>, DateTime?>();
        var zonePrototypes = new Dictionary<ProtoId<STWarZonePrototype>, STWarZonePrototype>();
        var requiredZoneIds = wzProto.Requirements?.OfType<ZoneOwnershipRequirenment>().SelectMany(r => r.RequiredZones).ToHashSet() ?? new HashSet<ProtoId<STWarZonePrototype>>();

        foreach (var rid in requiredZoneIds)
        {
            var ownership = await _dbManager.GetStalkerWarOwnershipAsync(rid);
            if (ownership != null)
            {
                ownerships[rid] = (ownership.Band?.BandProtoId, ownership.Faction?.FactionProtoId);
                lastCaptureTimes[rid] = ownership.LastCapturedByCurrentOwnerAt;
            }
            if (_prototypeManager.TryIndex<STWarZonePrototype>(rid, out var reqProto))
            {
                zonePrototypes[rid] = reqProto;
            }
        }

        Action<EntityUid, string, (string, object)[]?> feedbackCallback =
            (entity, locId, args) => _popup.PopupEntity(Loc.GetString(locId, args ?? Array.Empty<(string, object)>()), entity);

        var allMet = true;
        if (wzProto.Requirements != null)
        {
            foreach (var req in wzProto.Requirements)
            {
                // Pass the *effective* attacker (band OR faction) to the check
                var blockReason = req.Check(
                    currentAttackerBand,
                    currentAttackerFaction,
                    ownerships,
                    lastCaptureTimes,
                    zonePrototypes,
                    comp.ZoneProto,
                    effectiveFrameTime,
                    feedbackEntity,
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
            // If requirements fail, reset progress *if* it was a new attacker this tick.
            // This prevents progress sticking if requirements become unmet mid-capture.
            if (isNewAttacker) ResetCaptureProgress(comp);
            return;
        }

        // Update Capture Progress
        comp.CaptureProgressTime += effectiveFrameTime;
        comp.CaptureProgress = Math.Clamp(comp.CaptureProgressTime / wzProto.CaptureTime, 0f, 1f);

        // Announce each 10% increment locally
        var step = (int)(comp.CaptureProgress * 10);
        if (step > comp.LastAnnouncedProgressStep)
        {
            comp.LastAnnouncedProgressStep = step;
            if (feedbackEntity.HasValue)
                AnnounceCaptureProgressLocal(zone, comp, step * 10);
        }

        // Check for Capture Completion
        if (comp.CaptureProgressTime < wzProto.CaptureTime)
            return; // Not captured yet

        // --- Capture Complete ---

        ProtoId<STBandPrototype>? finalBandOwnerId = null;
        ProtoId<NpcFactionPrototype>? finalFactionOwnerId = null;
        string finalOwnerName;

        if (awardToFaction)
        {
            // Faction Capture
            comp.DefendingFactionProtoId = currentAttackerFaction;
            comp.DefendingBandProtoId = null; // Clear band defender
            if (currentAttackerFaction != null)
                finalFactionOwnerId = new ProtoId<NpcFactionPrototype>(currentAttackerFaction);
            finalOwnerName = GetAttackerName(null, currentAttackerFaction);
            Logger.InfoS("warzone", $"Zone '{comp.PortalName}' captured by Faction: {currentAttackerFaction}");
        }
        else
        {
            // Band Capture
            comp.DefendingBandProtoId = currentAttackerBand;
            comp.DefendingFactionProtoId = currentAttackerFaction; // Store associated faction as defender info
            if (currentAttackerBand != null)
                finalBandOwnerId = new ProtoId<STBandPrototype>(currentAttackerBand);
            finalOwnerName = GetAttackerName(currentAttackerBand, null); // Prioritize band name
             Logger.InfoS("warzone", $"Zone '{comp.PortalName}' captured by Band: {currentAttackerBand}");
        }

        // Update Database Ownership
        await _dbManager.SetStalkerZoneOwnershipAsync(
            comp.ZoneProto,
            finalBandOwnerId,
            finalFactionOwnerId);

        // Set Cooldown
        if (wzProto.CaptureCooldownHours > 0)
        {
            comp.CooldownEndTime = _gameTiming.CurTime + TimeSpan.FromHours(wzProto.CaptureCooldownHours);
        }

        // Announce Server-Wide
        _chatManager.DispatchServerAnnouncement(Loc.GetString(
            "st-warzone-captured",
            ("zone", comp.PortalName ?? "Unknown"),
            ("attacker", finalOwnerName)));

        // Reset Reward Timer
        _lastRewardTimes[zone] = _gameTiming.CurTime;

        // Finalize progress state
        comp.CaptureProgress = 1f;
        // Reset progress time and clear attacker for next capture
        comp.CaptureProgressTime = 0f;
        comp.CurrentAttackerBandProtoId = null;
        comp.CurrentAttackerFactionProtoId = null;
    }

    private void ResetAllRequirements(EntityUid zone)
    {
        // This function might need more granular logic if requirements have persistent state
        // For now, just ensures capture progress is reset if defender is present
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
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

        // Check if anyone is defending
        if (wzComp.DefendingBandProtoId == null && wzComp.DefendingFactionProtoId == null)
        {
            // Only award if ShouldAwardWhenDefenderPresent is true and zone is uncaptured
            if (!wzProto.ShouldAwardWhenDefenderPresent)
                return;
            // If ShouldAwardWhenDefenderPresent is true, proceed with awarding logic below even when uncaptured
        }

        var points = wzProto.RewardPointsPerPeriod;
        bool rewarded = false;

        // Prioritize Band Reward if a band is defending
        if (wzComp.DefendingBandProtoId != null)
        {
            var bandProtoId = wzComp.DefendingBandProtoId;
            var currentPoints = _bandPoints.TryGetValue(bandProtoId, out var val) ? val : 0;
            var newPoints = currentPoints + points;
            _bandPoints[bandProtoId] = newPoints;
            _dbManager.SetStalkerBandAsync(new ProtoId<STBandPrototype>(bandProtoId), newPoints);
            Logger.InfoS("warzone", $"Awarded {points} points to band {bandProtoId} (total: {newPoints}) for controlling {wzComp.PortalName}");
            rewarded = true;
        }
        // Otherwise, reward Faction if a faction is defending (and no band)
        else if (wzComp.DefendingFactionProtoId != null)
        {
            var factionProtoId = wzComp.DefendingFactionProtoId;
            var currentPoints = _factionPoints.TryGetValue(factionProtoId, out var val) ? val : 0;
            var newPoints = currentPoints + points;
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

    // Helper to get the first element from a HashSet (order isn't guaranteed but consistent enough here)
    private static string? GetFirst(HashSet<string>? set)
    {
        if (set == null) return null;
        foreach (var g in set)
            return g;
        return null;
    }

    private static EntityUid? GetFirstEntity(HashSet<EntityUid>? set)
    {
         if (set == null) return null;
        foreach (var entity in set)
            return entity;
        return null;
    }

    // Collision Handling - Populates PresentEntities, PresentBandProtoIds, PresentFactionProtoIds
    private void OnStartCollide(EntityUid uid, WarZoneComponent component, ref readonly StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands) || bands.BandProto == default)
            return; // Ignore entities without a valid band

        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;
            factionId = bandProto.FactionId;
        }

        component.PresentEntities ??= new();
        component.PresentBandProtoIds ??= new();
        component.PresentFactionProtoIds ??= new();

        if (bandId != null)
            component.PresentBandProtoIds.Add(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Add(factionId);
        component.PresentEntities.Add(other);
    }

    private void OnEndCollide(EntityUid uid, WarZoneComponent component, ref readonly EndCollideEvent args)
    {
        var other = args.OtherEntity;

        // Check if the component or sets are null before proceeding
        if (component.PresentEntities == null || component.PresentBandProtoIds == null || component.PresentFactionProtoIds == null)
            return;

        // Only remove if the entity is actually present
        if (!component.PresentEntities.Contains(other))
             return;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands) || bands.BandProto == default)
        {
             // If entity has no band component but was somehow in PresentEntities, just remove it
             component.PresentEntities.Remove(other);
             return;
        }

        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;
            factionId = bandProto.FactionId;
        }

        // Remove from sets
        if (bandId != null)
            component.PresentBandProtoIds.Remove(bandId);
        if (factionId != null)
            component.PresentFactionProtoIds.Remove(factionId);
        component.PresentEntities.Remove(other);

         // Logger.DebugS("warzone", $"Entity {other} left zone {uid}. Band: {bandId}, Faction: {factionId}. Present Bands: {component.PresentBandProtoIds.Count}, Present Factions: {component.PresentFactionProtoIds.Count}");

        // Trigger an immediate check if the zone might become empty or uncontested
        // This helps reset state faster than waiting for the next 1-second tick.
        // Use TryComp to avoid issues if the zone entity is deleting.
        if (TryComp<WarZoneComponent>(uid, out var wzComp))
        {
             // Reset NextCheckTime to force an update on the next frame
             wzComp.NextCheckTime = TimeSpan.Zero;
        }
    }

    // Entity Termination Handling
    private void OnEntityTerminating(EntityUid uid, MetaDataComponent component, ref EntityTerminatingEvent args)
    {
        // Use a separate method to handle removal logic
        RemoveEntityFromAllCaptures(uid);
    }

    private void RemoveEntityFromAllCaptures(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent(uid, out BandsComponent? bands) || bands.BandProto == default)
            return;

        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;
            factionId = bandProto.FactionId;
        }

        // Iterate through all war zones
        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var zoneUid, out var wzComp))
        {
            bool changed = false;

            // Remove from sets if present
            if (wzComp.PresentEntities != null && wzComp.PresentEntities.Remove(uid))
                changed = true;
            if (wzComp.PresentBandProtoIds != null && bandId != null && wzComp.PresentBandProtoIds.Remove(bandId))
                changed = true;
            if (wzComp.PresentFactionProtoIds != null && factionId != null && wzComp.PresentFactionProtoIds.Remove(factionId))
                changed = true;

            // If the entity's removal potentially changed the zone state, force an update check
            if (changed)
            {
                 // Reset NextCheckTime to force an update on the next frame
                 wzComp.NextCheckTime = TimeSpan.Zero;
            }
        }
    }

    // Helper to get a display name for the attacker/defender (band or faction)
    private string GetAttackerName(string? bandProtoId, string? factionProtoId)
    {
        // Prioritize Band Name if provided
        if (!string.IsNullOrEmpty(bandProtoId))
        {
            if (_prototypeManager.TryIndex<STBandPrototype>(bandProtoId, out var bandProto))
                return bandProto.Name; // Use LocId if available and localized? For now, just Name.
        }        
        else if (!string.IsNullOrEmpty(factionProtoId))
        {
            if (_prototypeManager.TryIndex<NpcFactionPrototype>(factionProtoId, out var factionProto))
                return factionProto.ID;
        }
        return Loc.GetString("st-warzone-unknown-attacker");
    }

    // Announce capture abandonment locally
    private void AnnounceCaptureAbandonedLocal(EntityUid zoneUid, WarZoneComponent? wzComp, string reason = "")
    {
        if (wzComp == null || (wzComp.CurrentAttackerBandProtoId == null && wzComp.CurrentAttackerFactionProtoId == null))
            return;

        // Only announce if the attacker was not the defender (prevents spam if defender leaves/re-enters)
        // Note: This check might need refinement depending on desired announcement behavior
        bool wasAttacking = wzComp.CurrentAttackerBandProtoId != wzComp.DefendingBandProtoId || wzComp.CurrentAttackerFactionProtoId != wzComp.DefendingFactionProtoId;

        if (wasAttacking)
        {
            string attackerName = GetAttackerName(wzComp.CurrentAttackerBandProtoId, wzComp.CurrentAttackerFactionProtoId);
            var message = Loc.GetString("st-warzone-capture-abandoned", ("zone", wzComp.PortalName ?? "Unknown"), ("attacker", attackerName));
            // Optionally add reason: if (!string.IsNullOrEmpty(reason)) message += $" ({reason})";

            var mapCoords = _transformSystem.GetMapCoordinates(zoneUid);
            var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange); // Use appropriate range
            _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, message, zoneUid, false, true, colorOverride: null); // Consider ChatChannel choice
        }

        // Attacker info is reset in the main UpdateCaptureAsync logic where abandonment is detected
    }

    // Announce capture start locally
    private void AnnounceCaptureStartedLocal(EntityUid zoneUid, WarZoneComponent wzComp, string attackerName)
    {
        var message = Loc.GetString("st-warzone-capture-started", ("attacker", attackerName), ("zone", wzComp.PortalName ?? "Unknown"));
        var mapCoords = _transformSystem.GetMapCoordinates(zoneUid);
        var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, message, zoneUid, false, true, colorOverride: null);
    }


    private void AnnounceCaptureProgressLocal(EntityUid zoneUid, WarZoneComponent wzComp, int percent)
    {
        var portalName = wzComp.PortalName ?? "Unknown";
        var message = Loc.GetString("st-warzone-progress", ("zone", portalName), ("percent", percent));
        var mapCoords = _transformSystem.GetMapCoordinates(zoneUid);
        var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, message, zoneUid, false, true, colorOverride: null);
    }
    // Load initial zone ownership and cooldown state from DB
    private async Task LoadInitialZoneStateAsync(EntityUid zoneUid, WarZoneComponent component)
    {
        try
        {
            if (!_prototypeManager.TryIndex<STWarZonePrototype>(component.ZoneProto, out var wzProto))
            {
                Logger.ErrorS("warzone", $"Could not find STWarZonePrototype with ID '{component.ZoneProto}' during async state load for zone {zoneUid}.");
                component.InitialLoadComplete = true; // Mark as complete even on error to prevent blocking
                return;
            }

            var ownership = await _dbManager.GetStalkerWarOwnershipAsync(component.ZoneProto);

            if (ownership != null)
            {
                // Load defender info
                component.DefendingBandProtoId = ownership.Band?.BandProtoId;
                component.DefendingFactionProtoId = ownership.Faction?.FactionProtoId;

                // Load cooldown state
                if (ownership.LastCapturedByCurrentOwnerAt.HasValue && wzProto.CaptureCooldownHours > 0)
                {
                    DateTime captureTime = ownership.LastCapturedByCurrentOwnerAt.Value;
                    DateTime cooldownEndDateTime = captureTime.AddHours(wzProto.CaptureCooldownHours);
                    DateTime currentDateTime = DateTime.UtcNow;

                    if (cooldownEndDateTime > currentDateTime)
                    {
                        TimeSpan remainingCooldown = cooldownEndDateTime - currentDateTime;
                        component.CooldownEndTime = _gameTiming.CurTime + remainingCooldown;
                         Logger.InfoS("warzone", $"Zone '{component.PortalName}' loaded with active cooldown: {remainingCooldown.TotalMinutes:F1} minutes remaining.");
                    }
                }
                 Logger.InfoS("warzone", $"Zone '{component.PortalName}' loaded ownership. Band: {component.DefendingBandProtoId ?? "None"}, Faction: {component.DefendingFactionProtoId ?? "None"}");
            }
             else
             {
                 Logger.InfoS("warzone", $"Zone '{component.PortalName}' loaded with no current ownership.");
             }

            component.InitialLoadComplete = true;
        }
        catch (Exception ex)
        {
            component.InitialLoadComplete = true; // Ensure load completes even on error
            Logger.ErrorS("warzone", $"Exception during async zone state load for {zoneUid} ({component.ZoneProto}): {ex}");
        }
    }
}
