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

    /// <summary>
    /// Gets the current points for a specific band.
    /// </summary>
    /// <param name="bandProtoId">The prototype ID of the band.</param>
    /// <returns>The points for the band, or 0 if the band is not tracked.</returns>
    public float GetBandPoints(string bandProtoId)
    {
        return _bandPoints.TryGetValue(bandProtoId, out var points) ? points : 0f;
    }

    /// <summary>
    /// Attempts to modify the points for a specific band by a given delta.
    /// Updates the internal cache and persists the change to the database.
    /// Use this instead of SetBandPoints for incremental changes.
    /// </summary>
    /// <param name="bandProtoId">The prototype ID of the band.</param>
    /// <param name="delta">The amount to change the points by (can be negative).</param>
    /// <returns>True if the points were successfully modified (even if clamped), false if the band prototype ID is invalid.</returns>
    public bool TryModifyBandPoints(string bandProtoId, float delta)
    {
        // Check if the band exists in prototypes to avoid adding points for invalid bands
        if (!_prototypeManager.HasIndex<STBandPrototype>(bandProtoId))
        {
            Logger.WarningS("warzone", $"Attempted to modify points for non-existent band prototype ID: {bandProtoId}");
            return false;
        }

        var currentPoints = GetBandPoints(bandProtoId);
        var newPoints = currentPoints + delta;

        // Prevent points from going below zero? Or allow debt? Assuming non-negative for now.
        if (newPoints < 0)
        {
             Logger.InfoS("warzone", $"Attempted to set points below zero for band {bandProtoId}. Clamping to 0.");
             newPoints = 0; // Clamp to zero if modification results in negative points
        }


        _bandPoints[bandProtoId] = newPoints;

        // Persist the change to the database asynchronously.
        // We don't necessarily need to wait for this to complete before returning true,
        // as the in-memory value is updated. Error handling for DB failure might be needed.
        _dbManager.SetStalkerBandAsync(new ProtoId<STBandPrototype>(bandProtoId), newPoints);

        Logger.DebugS("warzone", $"Modified points for band {bandProtoId} by {delta}. New total: {newPoints}");
        return true;
    }

    // Note: SetBandPoints and SetFactionPoints are still useful for direct setting, like in admin commands or initialization.

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
            component.PresentBandProtoIds = new(); // Represents bands with count > 0
            component.PresentFactionProtoIds = new(); // Represents factions with count > 0
            component.PresentEntities = new();
            component.PresentBandCounts = new(); // Initialize new dictionary
            component.PresentFactionCounts = new(); // Initialize new dictionary

            var initialOwnership = await _dbManager.GetStalkerWarOwnershipAsync(component.ZoneProto);

            _ = LoadInitialZoneStateAsync(uid, component, initialOwnership);

            if (initialOwnership != null && (initialOwnership.BandId != null || initialOwnership.FactionId != null))
            {
                var lastRewardTime = initialOwnership.LastCapturedByCurrentOwnerAt.HasValue
                    ? _gameTiming.CurTime - (DateTime.UtcNow - initialOwnership.LastCapturedByCurrentOwnerAt.Value)
                    : _gameTiming.CurTime;

                _lastRewardTimes[uid] = lastRewardTime;

                var ownerDesc = initialOwnership.BandId != null ? $"band:{initialOwnership.BandId}" : (initialOwnership.FactionId != null ? $"faction:{initialOwnership.FactionId}" : "unknown");
                Logger.InfoS("warzone", $"Initialized reward timing for zone '{component.PortalName}', owned by {ownerDesc}");
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
        // This logic remains the same, but the underlying PresentBandProtoIds/PresentFactionProtoIds
        // are now more stable due to the count-based updates in collision handlers.
        bool isNewAttacker = (currentAttackerBand != comp.CurrentAttackerBandProtoId || currentAttackerFaction != comp.CurrentAttackerFactionProtoId);
        if (isNewAttacker)
        {
            // Logger.InfoS("warzone", $"Zone '{comp.PortalName}': Attacker changed. Calculated: B={currentAttackerBand ?? "N"}, F={currentAttackerFaction ?? "N"}. Stored: B={comp.CurrentAttackerBandProtoId ?? "N"}, F={comp.CurrentAttackerFactionProtoId ?? "N"}. Resetting progress."); // Optional debug log
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

    // Collision Handling - Manages counts and presence sets
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

        // Ensure collections are initialized (should be by OnWarZoneInit, but safety first)
        component.PresentEntities ??= new();
        component.PresentBandProtoIds ??= new();
        component.PresentFactionProtoIds ??= new();
        component.PresentBandCounts ??= new();
        component.PresentFactionCounts ??= new();

        // Add entity first. If already present, do nothing (collision might fire multiple times).
        if (!component.PresentEntities.Add(other))
            return;

        // Update counts and presence sets
        bool changedState = false;
        if (bandId != null)
        {
            component.PresentBandCounts.TryGetValue(bandId, out var currentBandCount);
            component.PresentBandCounts[bandId] = currentBandCount + 1;
            if (currentBandCount == 0) // Only add to set if count was zero before increment
            {
                component.PresentBandProtoIds.Add(bandId);
                changedState = true; // Band presence changed from none to some
            }
        }
        if (factionId != null)
        {
            component.PresentFactionCounts.TryGetValue(factionId, out var currentFactionCount);
            component.PresentFactionCounts[factionId] = currentFactionCount + 1;
            if (currentFactionCount == 0) // Only add to set if count was zero before increment
            {
                 component.PresentFactionProtoIds.Add(factionId);
                 // Note: Faction presence change doesn't trigger immediate update on its own,
                 // as band presence change is usually the more critical factor for capture state.
            }
        }

        // Force an update check if a new band appeared
        if (changedState)
        {
            component.NextCheckTime = TimeSpan.Zero;
        }
    }

    private void OnEndCollide(EntityUid uid, WarZoneComponent component, ref readonly EndCollideEvent args)
    {
        var other = args.OtherEntity;

        // Check if the component or necessary collections are null before proceeding
        if (component.PresentEntities == null || component.PresentBandProtoIds == null || component.PresentFactionProtoIds == null || component.PresentBandCounts == null || component.PresentFactionCounts == null)
            return;

        // Only process if the entity was actually present and is now leaving
        if (!component.PresentEntities.Remove(other))
             return; // Entity wasn't in the set, maybe EndCollide fired before StartCollide processed?

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands) || bands.BandProto == default)
        {
             // Entity had no band component, but was somehow in PresentEntities.
             // Just ensure NextCheckTime is reset in case its removal matters.
             component.NextCheckTime = TimeSpan.Zero;
             return;
        }

        string? bandId = null;
        string? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;
            factionId = bandProto.FactionId;
        }

        // Update counts and presence sets
        bool changedState = false;
        if (bandId != null && component.PresentBandCounts.TryGetValue(bandId, out var currentBandCount))
        {
            if (currentBandCount > 1)
            {
                component.PresentBandCounts[bandId] = currentBandCount - 1;
            }
            else // Count is 1, will become 0
            {
                component.PresentBandCounts.Remove(bandId);
                if (component.PresentBandProtoIds.Remove(bandId)) // Remove from set only when count reaches zero
                    changedState = true; // Band presence changed from some to none
            }
        }
        else if (bandId != null)
        {
             Logger.WarningS("warzone", $"Entity {other} ending collision in zone {uid} had band {bandId}, but band count was not found or already zero.");
        }

        if (factionId != null && component.PresentFactionCounts.TryGetValue(factionId, out var currentFactionCount))
        {
             if (currentFactionCount > 1)
            {
                component.PresentFactionCounts[factionId] = currentFactionCount - 1;
            }
            else // Count is 1, will become 0
            {
                component.PresentFactionCounts.Remove(factionId);
                component.PresentFactionProtoIds.Remove(factionId); // Remove from set only when count reaches zero
                // Note: Faction presence change doesn't trigger immediate update on its own.
            }
        }
         else if (factionId != null)
        {
             Logger.WarningS("warzone", $"Entity {other} ending collision in zone {uid} had faction {factionId}, but faction count was not found or already zero.");
        }

        // Trigger an immediate check if a band disappeared entirely
        if (changedState)
        {
             // Use TryComp as the zone entity might be deleting simultaneously
             if (TryComp<WarZoneComponent>(uid, out var wzComp))
                 wzComp.NextCheckTime = TimeSpan.Zero;
        }
    }

    // Entity Termination Handling - Refactored for clarity and safety
    private void OnEntityTerminating(EntityUid uid, MetaDataComponent meta, ref EntityTerminatingEvent args)
    {
        // Check if the terminating entity has a BandsComponent
        if (!_entityManager.TryGetComponent(uid, out BandsComponent? bands) || bands.BandProto == default)
            return; // Not a player/band member we track for captures

        string? bandId = null;
        string? factionId = null;
        if (_prototypeManager.TryIndex<STBandPrototype>(bands.BandProto, out var bandProto))
        {
            bandId = bandProto.ID;
            factionId = bandProto.FactionId;
        }

        // Find all zones this entity might be in and remove it
        var query = EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var zoneUid, out var wzComp))
        {
            RemoveEntityFromCaptureZone(zoneUid, wzComp, uid, bandId, factionId);
        }
    }

    /// <summary>
    /// Helper method to remove a specific entity from a single capture zone's tracking state.
    /// Called by OnEndCollide (indirectly via OnEntityTerminating).
    /// </summary>
    private void RemoveEntityFromCaptureZone(EntityUid zoneUid, WarZoneComponent wzComp, EntityUid entityUid, string? bandId, string? factionId)
    {
        // Check if the component or necessary collections are null before proceeding
        if (wzComp.PresentEntities == null || wzComp.PresentBandProtoIds == null || wzComp.PresentFactionProtoIds == null || wzComp.PresentBandCounts == null || wzComp.PresentFactionCounts == null)
            return;

        // Only process if the entity was actually present
        if (!wzComp.PresentEntities.Remove(entityUid))
            return; // Entity wasn't in this zone's set

        // Update counts and presence sets
        bool changedState = false;
        if (bandId != null && wzComp.PresentBandCounts.TryGetValue(bandId, out var currentBandCount))
        {
            if (currentBandCount > 1)
            {
                wzComp.PresentBandCounts[bandId] = currentBandCount - 1;
            }
            else // Count is 1, will become 0
            {
                wzComp.PresentBandCounts.Remove(bandId);
                if (wzComp.PresentBandProtoIds.Remove(bandId)) // Remove from set only when count reaches zero
                    changedState = true; // Band presence changed from some to none
            }
        }
         else if (bandId != null)
        {
             Logger.WarningS("warzone", $"Terminating entity {entityUid} in zone {zoneUid} had band {bandId}, but band count was not found or already zero.");
        }


        if (factionId != null && wzComp.PresentFactionCounts.TryGetValue(factionId, out var currentFactionCount))
        {
             if (currentFactionCount > 1)
            {
                wzComp.PresentFactionCounts[factionId] = currentFactionCount - 1;
            }
            else // Count is 1, will become 0
            {
                wzComp.PresentFactionCounts.Remove(factionId);
                wzComp.PresentFactionProtoIds.Remove(factionId); // Remove from set only when count reaches zero
                // Note: Faction presence change doesn't trigger immediate update on its own.
            }
        }
        else if (factionId != null)
        {
             Logger.WarningS("warzone", $"Terminating entity {entityUid} in zone {zoneUid} had faction {factionId}, but faction count was not found or already zero.");
        }

        // If the entity's removal caused a band to disappear entirely, force an update check
        if (changedState)
        {
             // Reset NextCheckTime to force an update on the next frame
             wzComp.NextCheckTime = TimeSpan.Zero;
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
    // Load initial zone ownership and cooldown state from pre-fetched DB data
    private async Task LoadInitialZoneStateAsync(EntityUid zoneUid, WarZoneComponent component, StalkerZoneOwnership? ownership) // Added ownership parameter
    {
        try
        {
            if (!_prototypeManager.TryIndex<STWarZonePrototype>(component.ZoneProto, out var wzProto))
            {
                Logger.ErrorS("warzone", $"Could not find STWarZonePrototype with ID '{component.ZoneProto}' during async state load for zone {zoneUid}.");
                component.InitialLoadComplete = true;
                return;
            }

            if (ownership != null)
            {
                Logger.InfoS("warzone", $"Zone '{component.PortalName}' ({component.ZoneProto}): Loading ownership. BandProtoId from DB record: '{ownership.Band?.BandProtoId ?? "NULL"}', FactionProtoId from DB record: '{ownership.Faction?.FactionProtoId ?? "NULL"}'");
                component.DefendingBandProtoId = ownership.Band?.BandProtoId;
                component.DefendingFactionProtoId = ownership.Faction?.FactionProtoId;
                Logger.InfoS("warzone", $"Zone '{component.PortalName}' ({component.ZoneProto}): Loaded ownership from init. Assigned DefendingBandProtoId: '{component.DefendingBandProtoId ?? "NULL"}', DefendingFactionProtoId: '{component.DefendingFactionProtoId ?? "NULL"}'");

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
                 Logger.InfoS("warzone", $"Zone '{component.PortalName}' ({component.ZoneProto}): No ownership record passed from init.");
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
