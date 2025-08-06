using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Mind;
using Content.Shared._Stalker.Bands;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon.Components;
using Content.Server.Players.JobWhitelist;
using Content.Shared._Stalker.Bands.Components;
using Content.Server._Stalker.WarZone;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._Stalker.Bands
{
    public sealed class BandsSystem : SharedBandsSystem
    {
        [Dependency] private readonly IServerDbManager _dbManager = default!;
        [Dependency] private readonly MindSystem _mindSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly JobWhitelistManager _jobWhitelistManager = default!;
        [Dependency] private readonly WarZoneSystem _warZoneSystem = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private sealed record ServerBandInfo(STBandPrototype Prototype, StalkerBand? DbBand = null);

        private Dictionary<EntityUid, List<BandShopItem>> _loadedShopItems = new();


        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BandsManagingComponent, BoundUIOpenedEvent>(HandleBoundUIOpen);
            SubscribeLocalEvent<BandsManagingComponent, ComponentStartup>(SubscribeUpdateUiState);
            SubscribeLocalEvent<BandsManagingComponent, BandsManagingAddMemberMessage>(OnAddMember);
            SubscribeLocalEvent<BandsManagingComponent, BandsManagingRemoveMemberMessage>(OnRemoveMember);

            SubscribeLocalEvent<BandsComponent, ComponentInit>(OnInit);

            // Subscribe to the new buy message
            SubscribeLocalEvent<BandsManagingComponent, BandsManagingBuyItemMessage>(OnBuyItem);

            SubscribeLocalEvent<BandsComponent, ChangeBandEvent>(OnChange);
        }

        private void SubscribeUpdateUiState<T>(Entity<BandsManagingComponent> ent, ref T ev) where T : notnull
        {
            // Call UpdateUiState, passing null for actor as ComponentStartup doesn't have one readily available.
            // UpdateUiState will attempt to find a session from the UI itself.
            UpdateUiState(ent);
        }

        private void HandleBoundUIOpen(Entity<BandsManagingComponent> ent, ref BoundUIOpenedEvent args)
        {
            UpdateUiState(ent, args.Actor);
        }


        private async Task LoadShopItems(Entity<BandsManagingComponent> ent)
        {
            var (uid, component) = ent;
            if (_prototypeManager.TryIndex(component.ShopListingsProto, out var shopProto))
            {
                _loadedShopItems[uid] = shopProto.Items;
            }
            else
            {
                Logger.ErrorS("bands", $"Failed to load BandShopListingsPrototype with ID {component.ShopListingsProto} for entity {ToPrettyString(uid)}");
                _loadedShopItems[uid] = new List<BandShopItem>(); // Ensure the key exists even if loading fails
            }
            // No need for async void here, but keep Task for potential future async ops
            await Task.CompletedTask;
        }


        private async void UpdateUiState(Entity<BandsManagingComponent> ent, EntityUid? actor = null)
        {
            var (uid, component) = ent;

            // Ensure shop items are loaded for this component instance
            if (!_loadedShopItems.ContainsKey(uid))
            {
                await LoadShopItems(ent); // Load items if not already loaded
            }

            if (actor == null)
                return;

            if (!_mindSystem.TryGetMind(actor.Value, out _, out var mindComp) || !_mindSystem.TryGetSession(mindComp, out var session))
            {
                // Send empty state if session cannot be found
                _uiSystem.SetUiState(uid, BandsUiKey.Key, new BandsManagingBoundUserInterfaceState(null, 0, new(), false, new(), new(), new())); // Added shop items list
                return;
            }

            // If userId is still null after checks, we cannot proceed.
            if (session.UserId == null)
            {
                Logger.ErrorS("bands", $"Failed to obtain NetUserId for BandsManaging UI update on {ToPrettyString(uid)}.");
                // Send empty state if UserId is null
                _uiSystem.SetUiState(uid, BandsUiKey.Key, new BandsManagingBoundUserInterfaceState(null, 0, new(), false, new(), new(), new())); // Added shop items list
                return;
            }


            var bandInfo = await GetPlayerBandInfoAsync(session.UserId);
            // Don't return early if bandInfo is null, send state indicating no band or not manageable.
            List<BandMemberInfo> members = new();
            string? bandName = null;
            int maxMembers = 0;
            bool canManage = false;

            if (bandInfo != null)
            {
                members = await GetBandMembersAsync(bandInfo.Prototype);
                bandName = bandInfo.Prototype.Name;
                maxMembers = bandInfo.Prototype.MaxMembers;
                canManage = await CanPlayerManageBandAsync(session.UserId);
            }

            // --- Gather War Zone Data ---
            var warZoneInfos = new List<WarZoneInfo>();
            foreach (var (wzUid, wzComp) in _warZoneSystem.GetAllWarZones())
            {
                var zoneId = wzComp.ZoneProto;
                var owner = "None";
                if (!string.IsNullOrEmpty(wzComp.DefendingBandProtoId))
                    owner = $"Band {wzComp.DefendingBandProtoId}";
                else if (!string.IsNullOrEmpty(wzComp.DefendingFactionProtoId))
                    owner = $"Faction {wzComp.DefendingFactionProtoId}";

                var cooldown = wzComp.CooldownEndTime.HasValue
                    ? (float)Math.Max(0, (wzComp.CooldownEndTime.Value - _warZoneSystem.CurrentTime).TotalSeconds)
                    : 0f;

                string attacker = "None";
                if (!string.IsNullOrEmpty(wzComp.CurrentAttackerBandProtoId))
                    attacker = $"Band {wzComp.CurrentAttackerBandProtoId}";
                else if (!string.IsNullOrEmpty(wzComp.CurrentAttackerFactionProtoId))
                    attacker = $"Faction {wzComp.CurrentAttackerFactionProtoId}";

                string defender = "None";
                if (!string.IsNullOrEmpty(wzComp.DefendingBandProtoId))
                    defender = $"Band {wzComp.DefendingBandProtoId}";
                else if (!string.IsNullOrEmpty(wzComp.DefendingFactionProtoId))
                    defender = $"Faction {wzComp.DefendingFactionProtoId}";

                float progress = 0f;

                warZoneInfos.Add(new WarZoneInfo(zoneId, owner, cooldown, attacker, defender, progress));
            }

            // --- Gather Band Points Data ---
            var bandPointsInfos = new List<BandPointsInfo>();
            foreach (var kvp in _warZoneSystem.BandPoints)
            {
                string name = kvp.Key;
                if (_prototypeManager.TryIndex<STBandPrototype>(kvp.Key, out var bandProto))
                    name = bandProto.Name;
                bandPointsInfos.Add(new BandPointsInfo(kvp.Key, name, kvp.Value));
            }

            // --- Create and Send State ---
            // Get the loaded shop items for this specific component instance
            var shopItems = _loadedShopItems.GetValueOrDefault(uid, new List<BandShopItem>());

            // --- Create and Send State ---
            var state = new BandsManagingBoundUserInterfaceState(bandName, maxMembers, members, canManage, warZoneInfos, bandPointsInfos, shopItems);

            // Use the correct SetUiState overload - no session needed here.
            _uiSystem.SetUiState(uid, BandsUiKey.Key, state);
        }


        // --- Action Handlers ---
        // OnAddMember and OnRemoveMember remain largely the same, but call the new UpdateUiState
        private async void OnAddMember(EntityUid uid, BandsManagingComponent component, BandsManagingAddMemberMessage msg)
        {
            if (!_mindSystem.TryGetMind(msg.Actor, out _, out var mindComp) || !_mindSystem.TryGetSession(mindComp, out var session))
                return;

            var leaderUserId = session.UserId;

            if (!await CanPlayerManageBandAsync(leaderUserId)) // Use helper
                return;

            var targetPlayerRecord = await _dbManager.GetPlayerRecordByUserName(msg.PlayerName);
            if (targetPlayerRecord == null)
            {
                // TODO: Send feedback to the user that the player was not found
                return;
            }

            var targetUserId = targetPlayerRecord.UserId;

            // Prevent adding self
            if (leaderUserId == targetUserId)
                return;

            var leaderBandInfo = await GetPlayerBandInfoAsync(leaderUserId);
            if (leaderBandInfo == null)
                return; // Leader isn't in a band? Should not happen if they can manage.

            var currentMembers = await GetBandMembersAsync(leaderBandInfo.Prototype);
            if (currentMembers.Count >= leaderBandInfo.Prototype.MaxMembers)
            {
                // TODO: Send feedback to the user that the band is full
                return;
            }

            if (currentMembers.Any(m => m.UserId == targetUserId))
            {
                // TODO: Send feedback to the user that the player is already in the band
                return;
            }

            var targetBandInfo = await GetPlayerBandInfoAsync(targetUserId);
            if (targetBandInfo != null)
            {
                // TODO: Send feedback to the user that the target player is already in another band
                return;
            }

            // Find the lowest rank role ID from the band's hierarchy prototype
            if (leaderBandInfo.Prototype.Hierarchy.Count == 0)
                return;
            var minKey = leaderBandInfo.Prototype.Hierarchy.Keys.Min();
            var lowestRankRoleId = leaderBandInfo.Prototype.Hierarchy[minKey].ToString();

            if (string.IsNullOrEmpty(lowestRankRoleId))
            {
                Logger.ErrorS("bands", $"Band {leaderBandInfo.Prototype.Name} (ProtoID: {leaderBandInfo.Prototype.ID}) has no roles defined in hierarchy for adding members.");
                return;
            }

            // Add the lowest rank role to the target player using JobWhitelistManager or DBManager
            // Assuming AddRoleWhitelistAsync is the correct method
            // Add the lowest rank role to the target player using AddJobWhitelist
            await _dbManager.AddJobWhitelist(targetUserId.UserId, new ProtoId<JobPrototype>(lowestRankRoleId));

            // Update UI state after modification
            UpdateUiState((uid, component), msg.Actor);
        }

        private async void OnRemoveMember(EntityUid uid, BandsManagingComponent component, BandsManagingRemoveMemberMessage msg)
        {
            // Use the requested pattern for getting session from the message actor
            if (!_mindSystem.TryGetMind(msg.Actor, out _, out var mindComp) || !_mindSystem.TryGetSession(mindComp, out var session))
                return;

            var leaderUserId = session.UserId;

            if (!await CanPlayerManageBandAsync(leaderUserId))
                return;

            // Prevent leader from removing themselves via this UI (they should leave the band differently)
            if (leaderUserId.UserId == msg.PlayerUserId)
                return;

            var leaderBandInfo = await GetPlayerBandInfoAsync(leaderUserId);
            if (leaderBandInfo == null)
                return;

            // Construct a set of all role IDs associated with this band prototype's hierarchy
            var bandRoleIds = leaderBandInfo.Prototype.Hierarchy.Values
                                    .Select(p => p.ToString()) // Get Role IDs from prototype hierarchy
                                    .ToHashSet();
            // Also include the main band prototype ID if it represents the leader/base role
            bandRoleIds.Add(leaderBandInfo.Prototype.ID.ToString());


            // Get the roles the target member has that are part of this band
            // Get all whitelisted jobs for the player
            var whitelistedJobs = await _dbManager.GetJobWhitelists(msg.PlayerUserId);
            var memberRolesInBand = whitelistedJobs
                .Where(roleId => bandRoleIds.Contains(roleId))
                .ToList();

            if (!memberRolesInBand.Any())
                return; // Target player is not in this band (or has no roles from it)

            // Remove all band-related roles from the target member
            foreach (var roleWhitelist in memberRolesInBand)
            {
                await _dbManager.RemoveJobWhitelist(msg.PlayerUserId, new ProtoId<JobPrototype>(roleWhitelist));
            }

            // Update UI state after modification
            UpdateUiState((uid, component), msg.Actor);
        }

        // --- Methods from SharedBandsSystem ---

        private void OnInit(EntityUid uid, BandsComponent component, ComponentInit args)
        {
            EnsureComp<StatusIconComponent>(uid);

            if (component is { AltBand: not null, CanChange: true })
                _actions.AddAction(uid, ref component.ActionChangeEntity, component.ActionChange, uid);
        }

        private void OnChange(Entity<BandsComponent> entity, ref ChangeBandEvent args)
        {
            if (args.Handled)
                return;

            var comp = entity.Comp;
            if (comp.AltBand == null || !comp.CanChange)
                return;

            // Swap the band status icon and alt band name
            (comp.BandStatusIcon, comp.AltBand) = (comp.AltBand, comp.BandStatusIcon);
            Dirty(entity);
            args.Handled = true;
        }

        // --- Helper Methods

        /// <summary>
        /// Gets the band prototype and potentially related DB info for a player based on their primary role.
        /// </summary>
        private async Task<ServerBandInfo?> GetPlayerBandInfoAsync(NetUserId userId)
        {
            // 1. Get the player's whitelisted jobs and use the first as their "primary" for band purposes
            var whitelistedJobs = await _dbManager.GetJobWhitelists(userId);
            var primaryRole = whitelistedJobs.FirstOrDefault();
            if (primaryRole == null)
                return null; // Player has no whitelisted jobs

            // 2. Find the STBandPrototype whose ID matches the primary role ID
            // This assumes the STBandPrototype ID *is* the RoleId. Adjust if mapping is different.
            if (!_prototypeManager.TryIndex<STBandPrototype>(primaryRole, out var bandProto))
            {
                // It's possible not every role corresponds to a band leader role
                // Check if the player belongs to *any* band role defined in *any* band prototype hierarchy
                foreach (var proto in _prototypeManager.EnumeratePrototypes<STBandPrototype>())
                {
                    var candidateJobs = await _dbManager.GetJobWhitelists(userId);
                    var bandRoleIds = proto.Hierarchy.Values.Select(p => p.ToString()).ToHashSet();
                    bandRoleIds.Add(proto.ID.ToString()); // Include base role

                    if (candidateJobs.Any(r => bandRoleIds.Contains(r)))
                    {
                        // Player is part of this band, find the corresponding DB entry if needed
                        var dbBand = await _dbManager.GetStalkerBandAsync(proto.ID);
                        return new ServerBandInfo(proto, dbBand);
                    }
                }
                return null; // Player is not part of any known band
            }


            // 3. Player's primary role IS a band leader role. Find the corresponding DB entry if needed.
            var bandDbInfo = await _dbManager.GetStalkerBandAsync(bandProto.ID);

            return new ServerBandInfo(bandProto, bandDbInfo);
        }


        /// <summary>
        /// Gets members of a band defined by the given prototype, returning BandMemberInfo objects.
        /// </summary>
        private async Task<List<BandMemberInfo>> GetBandMembersAsync(STBandPrototype bandProto)
        {
            var members = new List<BandMemberInfo>();

            // Collect all role IDs associated with this band
            var bandRoleIds = bandProto.Hierarchy.Values.Select(p => p.ToString()).ToHashSet();
            bandRoleIds.Add(bandProto.ID.ToString()); // Include the base/leader role

            // Get all players who have any of these roles whitelisted
            var playersWithRoles = await _dbManager.GetPlayersWithRoleWhitelistAsync(bandRoleIds);

            // Convert database players to PlayerRecords and create BandMemberInfo for each
            foreach (var player in playersWithRoles)
            {
                var userId = new NetUserId(player.UserId);

                // --- Get Character Name ---
                string characterName = player.LastSeenUserName; // Default to ckey if not found
                var prefs = await _dbManager.GetPlayerPreferencesAsync(userId, default);
                if (prefs != null)
                {
                    var profile = prefs.SelectedCharacter;
                    // Assuming HumanoidCharacterProfile for now, adjust if other types are possible
                    if (profile is Content.Shared.Preferences.HumanoidCharacterProfile humanoidProfile)
                    {
                        characterName = humanoidProfile.Name;
                    }
                }
                // --- End Get Character Name ---

                // Get all whitelisted jobs for this player
                var whitelistedJobs = await _dbManager.GetJobWhitelists(player.UserId);

                // Filter to only jobs relevant to this band
                var bandJobs = whitelistedJobs.Where(job => bandRoleIds.Contains(job)).ToList();

                if (!bandJobs.Any())
                    continue; // Skip if no relevant jobs (shouldn't happen due to our query, but just in case)

                // Find the highest rank role this player has in the band's hierarchy
                string displayRole = "Unknown";
                int highestRank = -1;

                foreach (var job in bandJobs)
                {
                    // If job is the band leader role
                    if (job == bandProto.ID)
                    {
                        displayRole = job;
                        break; // Leader role takes precedence
                    }

                    // Find the rank in the hierarchy
                    var rankEntry = bandProto.Hierarchy.FirstOrDefault(h => h.Value == job);
                    if (rankEntry.Value != default && rankEntry.Key > highestRank)
                    {
                        highestRank = rankEntry.Key;
                        displayRole = job;
                    }
                }

                members.Add(new BandMemberInfo(userId, player.LastSeenUserName, characterName, displayRole));
            }

            return members;
        }

        /// <summary>
        /// Checks if a player can manage their band (delegated to DB Manager).
        /// </summary>
        private async Task<bool> CanPlayerManageBandAsync(NetUserId userId)
        {
            // 1. Get the player's band component (STBandPrototype)
            if (!_playerManager.TryGetSessionById(userId, out var session) || session.AttachedEntity is not { } player)
                return false;
            if (!EntityManager.TryGetComponent(player, out BandsComponent? bandsComp) || bandsComp == null)
                return false;

            // 2. Get the band prototype
            if (!_prototypeManager.TryIndex<STBandPrototype>(bandsComp.BandProto, out var bandProto))
                return false;

            // 3. Get the player's job (use first whitelisted job)
            var whitelistedJobs = await _dbManager.GetJobWhitelists(userId);
            var playerJob = whitelistedJobs.FirstOrDefault();
            if (playerJob == null)
                return false;

            // 4. Find the rank ID for that job in the prototype's Hierarchy
            var found = bandProto.Hierarchy.FirstOrDefault(kvp => kvp.Value == playerJob);
            if (found.Value.Equals(default(ProtoId<JobPrototype>)))
                return false;

            var rankId = found.Key;

            // 5. Compare to ManagingRankId
            return rankId >= bandProto.ManagingRankId;
        }

        private async void OnBuyItem(EntityUid uid, BandsManagingComponent component, BandsManagingBuyItemMessage msg)
        {
            // Use the requested pattern for getting session from the message actor
            if (!_mindSystem.TryGetMind(msg.Actor, out _, out var mindComp) || !_mindSystem.TryGetSession(mindComp, out var session))
                return;

            var buyer = msg.Actor;
            var buyerUserId = session.UserId;

            // 1. Verify the player can manage the band (required to buy for the band)
            if (!await CanPlayerManageBandAsync(buyerUserId))
            {
                Logger.WarningS("bands", $"Player {buyerUserId} attempted to buy band item {msg.ItemId} without managing rights.");
                // TODO: Send feedback to user?
                return;
            }

            // 2. Get the player's band info
            var bandInfo = await GetPlayerBandInfoAsync(buyerUserId);
            if (bandInfo == null)
            {
                Logger.WarningS("bands", $"Player {buyerUserId} attempted to buy band item {msg.ItemId} but is not in a band.");
                return; // Should not happen if CanPlayerManageBandAsync passed, but check anyway
            }
            var bandProtoId = bandInfo.Prototype.ID;

            // 3. Find the item in the loaded shop items for this component
            if (!_loadedShopItems.TryGetValue(uid, out var shopItems))
            {
                Logger.ErrorS("bands", $"Shop items not loaded for BandsManagingComponent {ToPrettyString(uid)} when player {buyerUserId} tried to buy {msg.ItemId}.");
                return;
            }

            var itemToBuy = shopItems.FirstOrDefault(item => item.ProductEntity == msg.ItemId);
            if (itemToBuy == null)
            {
                Logger.WarningS("bands", $"Player {buyerUserId} attempted to buy unknown item {msg.ItemId}.");
                // TODO: Send feedback to user?
                return;
            }

            // 4. Check band points
            var currentPoints = _warZoneSystem.GetBandPoints(bandProtoId); // Assuming WarZoneSystem manages points
            if (currentPoints < itemToBuy.Price)
            {
                Logger.InfoS("bands", $"Band {bandProtoId} has insufficient points ({currentPoints}) to buy {msg.ItemId} (cost: {itemToBuy.Price}).");
                // TODO: Send feedback to user
                return;
            }

            // 5. Deduct points
            if (!_warZoneSystem.TryModifyBandPoints(bandProtoId, -itemToBuy.Price))
            {
                Logger.ErrorS("bands", $"Failed to deduct points from band {bandProtoId} for item {msg.ItemId}.");
                // This likely indicates an issue with WarZoneSystem point management
                return;
            }

            // 6. Spawn item
            var coordinates = Transform(buyer).Coordinates;
            if (!coordinates.IsValid(_entityManager)) // Ensure coordinates are valid before spawning
            {
                Logger.WarningS("bands", $"Cannot spawn item {msg.ItemId} for player {buyerUserId} at invalid coordinates.");
                // Refund points? Or handle differently? For now, log and stop.
                _warZoneSystem.TryModifyBandPoints(bandProtoId, itemToBuy.Price); // Attempt refund
                return;
            }

            var product = Spawn(itemToBuy.ProductEntity, coordinates);

            // 7. Give item to player
            _hands.PickupOrDrop(buyer, product); // Use PickupOrDrop as requested

            Logger.InfoS("bands", $"Player {buyerUserId} (Band: {bandProtoId}) bought item {msg.ItemId} for {itemToBuy.Price} points.");

            // 8. Update UI state for all clients viewing this UI
            UpdateUiState((uid, component), buyer);
        }
    }
}
