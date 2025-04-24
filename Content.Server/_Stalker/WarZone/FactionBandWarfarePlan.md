# Faction & Band Warfare System - Design and Implementation

## Overview
This document outlines the design and current implementation status of the Faction and Band Warfare system for the Stalker 14 project.

---

## Design Concepts

### Core Concepts
- **War Zones:** Defined by `STWarZonePrototype`, these are areas that can be captured by Bands or Factions. They have configurable properties like reward rates, capture requirements, and cooldown periods. Represented in-game by entities with a `WarZoneComponent`.
- **Factions:** NPC groups (defined by `NpcFactionPrototype`) that can own zones and accumulate reward points, tracked persistently in the database.
- **Bands:** Player groups (defined by `STBandPrototype`), potentially aligned with factions, that can own zones and accumulate reward points, tracked persistently in the database.
- **Ownership:** A zone is owned by either a single Band or a single Faction. Ownership is tracked persistently in the `stalker_zone_ownerships` database table via `ServerDbManager`.
- **Capture Mechanics:**
    - Players (with a `BandsComponent`) entering a zone's trigger area are registered.
    - The system tracks entities entering/leaving the zone via collision events. It maintains counts of players per Band and Faction (`PresentBandCounts`, `PresentFactionCounts`) and uses these counts to manage sets of currently present groups (`PresentBandProtoIds`, `PresentFactionProtoIds` in `WarZoneComponent`). A group is considered present only if its count is greater than zero.
    - If only one non-owning Band/Faction group (as determined by the presence sets) is present and the zone is not on cooldown, capture begins.
    - Capture progress (`CaptureProgress` in `WarZoneComponent`) advances based on requirements, primarily `CaptureTimeRequirenment`.
    - If multiple potential attackers enter, or the defender re-enters, capture progress is halted/reset.
    - If the attacker leaves, the capture attempt is abandoned.
    - Successful capture updates ownership in the database and component, and potentially triggers a cooldown.
- **Requirements:** Zones can have capture requirements defined in `STWarZonePrototype.Requirements` (e.g., `ZoneOwnershipRequirenment`, `CaptureTimeRequirenment`). All requirements must be met for capture progress to occur and complete.
- **Rewards:** Owners gain points (`RewardPointsPerPeriod`) periodically (`RewardPeriod`) for held zones, unless `ShouldAwardWhenDefenderPresent` is true and no defender is present. Points are added to the Band/Faction totals stored in the database (`stalker_bands`, `stalker_factions`).
- **Cooldown:** After capture, a zone enters a cooldown period (`CaptureCooldownHours` in prototype, `CooldownEndTime` in component) during which it cannot be captured. Players entering during cooldown receive a popup notification.
- **Persistence:** Zone ownership, Band points, and Faction points are stored in the database using `IServerDbManager` and loaded on system initialization.

### Database Schema (Implicit via `ServerDbManager`)
- `stalker_bands`: Stores Band prototype IDs and their accumulated reward points.
- `stalker_factions`: Stores Faction prototype IDs and their accumulated reward points.
- `stalker_zone_ownerships`: Tracks which Band or Faction owns which zone prototype, and when it was last captured.

---

## Implementation Summary

### Prototypes and Components
- **`STWarZonePrototype` (`Resources/Prototypes/_Stalker/ScriptedEntities/WarZones/WarZones.yml`):** Defines zone properties:
    - `ID`: Unique identifier.
    - `RewardPointsPerPeriod`: Points awarded per reward cycle.
    - `RewardPeriod`: Duration of the reward cycle in seconds.
    - `Requirements`: A set of conditions (e.g., `CaptureTimeRequirenment`, `ZoneOwnershipRequirenment`) needed to capture the zone.
    - `CaptureCooldownHours`: Duration of the post-capture cooldown.
    - `ShouldAwardWhenDefenderPresent`: If false (default), rewards are only given if a defender owns the zone. If true, rewards are given even if the zone is uncaptured/neutral.
- **`STBandPrototype`:** Defines player bands and their potential faction alignment (`FactionId`). Used to identify attackers/defenders.
- **`NpcFactionPrototype`:** Defines NPC factions. Used to identify attackers/defenders.
- **`WarZoneComponent` (`Content.Server/_Stalker/WarZone/WarZoneComponent.cs`):** Attached to zone entities:
    - `PortalName`: Display name for the zone.
    - `ZoneProto`: Links to the `STWarZonePrototype`.
    - `DefendingBandProtoId`/`DefendingFactionProtoId`: Current owner.
    - `CurrentAttackerBandProtoId`/`CurrentAttackerFactionProtoId`: Current group attempting capture.
    - `CooldownEndTime`: Timestamp when the zone cooldown expires.
    - `InitialLoadComplete`: Flag indicating if initial state loaded from DB.
    - `PresentEntities`: Set tracking the specific `EntityUid`s currently inside the zone trigger.
    - `PresentBandCounts`/`PresentFactionCounts`: Dictionaries tracking the *number* of entities per Band/Faction currently inside the zone.
    - `PresentBandProtoIds`/`PresentFactionProtoIds`: Sets indicating which Band/Faction *groups* have at least one member present (derived from the counts). These are used for capture logic checks.
    - `CaptureProgress`: Tracks the current capture progress (0.0 to 1.0).
- **`BandsComponent`:** Attached to player entities, tracks their band membership (`BandProto`).

### War Zone System (`Content.Server/_Stalker/WarZone/WarZoneSystem.cs`)
- **Initialization:** Loads Band/Faction points and zone ownership from the database on startup (`InitializeWarZoneAsync`, `LoadInitialZoneStateAsync`). Initializes points to 0 if not found.
- **Presence Tracking:** Uses physics collision events (`StartCollideEvent`, `EndCollideEvent`) with the zone's trigger fixture. It updates `PresentEntities`, increments/decrements counts in `PresentBandCounts` and `PresentFactionCounts`, and updates the `PresentBandProtoIds`/`PresentFactionProtoIds` sets only when a group's count transitions between zero and non-zero. Handles entity termination (`EntityTerminatingEvent`) via `RemoveEntityFromCaptureZone` to correctly decrement counts and update sets.
- **Capture Logic (`UpdateCaptureAsync`):**
    - Runs periodically.
    - Checks for contestation (multiple attackers present) or defender presence; resets requirements (like capture time) if contested.
    - Checks if the zone is on cooldown.
    - Identifies a single attacker if uncontested and not the defender.
    - Evaluates `Requirements`. If met, capture proceeds/completes. `CaptureTimeRequirenment` updates its internal progress based on frame time.
    - Updates `CaptureProgress` based on requirement state (specifically `CaptureTimeRequirenment` progress).
    - On successful capture: Updates `DefendingBandProtoId`/`DefendingFactionProtoId`, saves ownership to DB via `SetStalkerZoneOwnershipAsync`, sets `CooldownEndTime`, resets requirements, and announces the capture.
    - Announces capture start, abandonment, and completion using `IChatManager`.
    - Shows cooldown popups using `PopupSystem`.
- **Reward Distribution (`DistributeRewards`):**
    - Runs periodically (`Update`).
    - Checks if `RewardPeriod` has elapsed since the last reward for each zone.
    - Awards `RewardPointsPerPeriod` to the owning Band or Faction based on `DefendingBandProtoId`/`DefendingFactionProtoId`.
    - Considers the `ShouldAwardWhenDefenderPresent` flag.
    - Updates points in the database via `SetStalkerBandAsync()` or `SetStalkerFactionAsync()`.
    - Updates the last reward time (`_lastRewardTimes`).
- **Persistence:** Integrates with `IServerDbManager` to load/save Band points, Faction points, and Zone ownership.

### Commands
- **`WarZoneAdminCommand` (`Content.Server/_Stalker/WarZone/Commands/WarZoneAdminCommand.cs`):** (Admin only)
    - `warzoneadmin setpoints band <bandProtoId> <points>`: Sets reward points for a specific band.
    - `warzoneadmin setpoints faction <factionProtoId> <points>`: Sets reward points for a specific faction.
    - `warzoneadmin setowner <zoneProtoId> band <bandProtoId>`: Forces ownership of a zone to a specific band.
    - `warzoneadmin setowner <zoneProtoId> faction <factionProtoId>`: Forces ownership of a zone to a specific faction.
    - `warzoneadmin clearowner <zoneProtoId>`: Clears ownership and cooldown for a specific zone.
- **`WarZoneInfoCommand` (`Content.Server/_Stalker/WarZone/Commands/WarZoneInfoCommand.cs`):** (Any player)
    - `warzoneinfo`: Displays a list of all war zones with their current owner, cooldown status, attacker, defender, capture progress, and entity UID. Also lists current points for all known Bands and Factions.

### Notes
- UI integration for capture progress and ownership status is not yet implemented.
- The system is modular and allows for adding new requirement types.

---

## Next Steps
- Implement UI feedback for players showing zone ownership, capture progress, and cooldown status.
- Expand the use of reward points within the game (e.g., vendor unlocks, faction reputation).
- Refine and potentially expand the available capture requirements and consequences logic.
- Consider adding more complex interactions between factions/bands based on zone control.