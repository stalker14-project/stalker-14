using Content.Server.Chat.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Robust.Shared.Log;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using System.Threading.Tasks;
using System.Threading;
using static Content.Server._Stalker.AI.AIManager;
using Content.Shared.Hands.Components;
using Robust.Shared.Player;
using Content.Server._Stalker.AI;
using Content.Shared._Stalker.AI;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Content.Shared.NPC.Systems;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Content.Shared.NPC.Prototypes;

namespace Content.Server._Stalker.AI
{
    public sealed partial class AINPCSystem : SharedAiNpcSystem
    {
        
        private string GetGiveItemToolDescription(AiNpcComponent component)
        {
            // Build the list of allowed items for the description from GivableItems
            var allowedItemsList = component.GivableItems
                .Select(item => $"- {item.ProtoId} (Max: {item.MaxQuantity}, Rarity: {item.Rarity})")
                .ToList();
            var allowedItemsString = allowedItemsList.Count > 0
                ? string.Join("\n", allowedItemsList)
                : "None";

            // Simplified description, emphasizing it's for rewards/gifts
            var description = $@"Give a specified item (as a reward or gift) to a player. You can only give items from this list:
                {allowedItemsString}";

            return $@"{{
                ""name"": ""TryGiveItem"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""ckey"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (not name) of the player to give the item to.""
                        }},
                        ""itemPrototypeId"": {{
                            ""type"": ""string"",
                            ""description"": ""The exact prototype ID of the item from the allowed list.""
                        }},
                        ""quantity"": {{
                            ""type"": ""integer"",
                            ""description"": ""Number of items to give (defaults to 1, respects max quantity)."",
                            ""default"": 1
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""Optional message the NPC says while giving the item (e.g., 'Here's your reward.').""
                        }}
                    }},
                    ""required"": [""ckey"", ""itemPrototypeId""]
                }}
            }}";
        }

        public bool TryGiveItem(EntityUid npc, string targetPlayerIdentifier, string itemPrototypeId, int quantity, string? npcResponse = null)
        {
            // npcResponse is handled by ExecuteToolCall before this method is run.
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting give item: Proto='{itemPrototypeId}', Qty={quantity}, Target='{targetPlayerIdentifier}'");

            // --- 0. Get NPC Component ---
            if (!TryComp<AiNpcComponent>(npc, out var aiComp))
            {
                _sawmill.Error($"NPC {ToPrettyString(npc)} is missing AiNpcComponent in TryGiveItem.");
                return false;
            }

            // --- 1. Find Target Player ---
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryGiveItem.");
                // Maybe chat failure? TryChat(npc, $"Who is {targetPlayerIdentifier}?");
                return false;
            }

            // --- 2. Validate Item Against Givable List & Quantity ---
            var givableItemInfo = aiComp.GivableItems.FirstOrDefault(item => item.ProtoId.Id.Equals(itemPrototypeId, StringComparison.OrdinalIgnoreCase));

            if (givableItemInfo == null)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give non-givable item '{itemPrototypeId}'. Denying.");
                // TryChat(npc, $"I can't give you one of those."); // Feedback
                return false;
            }

            if (quantity <= 0)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give zero or negative quantity ({quantity}) of '{itemPrototypeId}'. Setting to 1.");
                quantity = 1;
            }

            if (quantity > givableItemInfo.MaxQuantity)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give {quantity} of '{itemPrototypeId}', but max allowed is {givableItemInfo.MaxQuantity}. Clamping quantity.");
                quantity = givableItemInfo.MaxQuantity;
                // TryChat(npc, $"Whoa there, I can only give you {quantity} of those."); // Feedback
            }

            // --- 3. Validate Prototype ID ---
            if (!_proto.HasIndex<EntityPrototype>(itemPrototypeId))
            {
                _sawmill.Warning($"Invalid prototype ID '{itemPrototypeId}' requested by NPC {ToPrettyString(npc)} for TryGiveItem.");
                return false;
            }

            // --- 4. Check Range & Interaction ---
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > 2.0f)
            {
                _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far for NPC {ToPrettyString(npc)} to give item.");
                // TryChat(npc, $"Get closer if you want this."); // Feedback
                return false;
            }

            // --- 5. Spawn and Give Items ---
            var npcCoords = Transform(npc).Coordinates;
            if (!npcCoords.IsValid(EntityManager))
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} has invalid coordinates, cannot spawn items.");
                return false;
            }

            int givenCount = 0;
            for (int i = 0; i < quantity; i++)
            {
                var spawnedItem = Spawn(itemPrototypeId, npcCoords);
                if (!_hands.TryPickupAnyHand(targetPlayer.Value, spawnedItem, checkActionBlocker: false))
                {
                    _sawmill.Warning($"Failed direct pickup for item {i + 1}/{quantity} ({ToPrettyString(spawnedItem)}) by {ToPrettyString(targetPlayer.Value)}. Dropping near NPC.");
                    Transform(spawnedItem).Coordinates = npcCoords;
                    // If dropping near, maybe don't count as "given"? Or maybe do? Let's count it for now.
                    givenCount++; // Count even if dropped nearby
                }
                else
                {
                    givenCount++;
                }
            }

            if (givenCount > 0)
                _sawmill.Info($"NPC {ToPrettyString(npc)} successfully gave {givenCount}/{quantity} of '{itemPrototypeId}' to {ToPrettyString(targetPlayer.Value)}.");
            else
                _sawmill.Warning($"NPC {ToPrettyString(npc)} failed to give any '{itemPrototypeId}' to {ToPrettyString(targetPlayer.Value)} (pickup/drop failed).");

            return givenCount > 0;
        }


        private string GetTakeItemToolDescription()
        {
            // Simplified description, emphasizing it's for quest items/specific requests
            var description = "Request a specific item (e.g., a quest item) from a player and attempt to take it if they hold it out.";
            return $@"{{
                ""name"": ""TryTakeItem"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""ckey"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (not name) of the player to request the item from.""
                        }},
                        ""requestedItemName"": {{
                            ""type"": ""string"",
                            ""description"": ""The exact prototype ID of the item the NPC wants to receive (e.g., 'MutantPartBoarHoof').""
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""REQUIRED message the NPC says while requesting/taking the item (e.g., 'Alright, let's see that BoarHoof. Hold it out.').""
                        }}
                    }},
                    ""required"": [""ckey"", ""requestedItemName"", ""npcResponse""]
                }}
            }}";
        }

        /// <summary>
        /// Attempts to take a specified item from a player's hands.
        /// </summary>
        public bool TryTakeItem(EntityUid npc, string targetPlayerIdentifier, string requestedItemName, string? npcResponse = null)
        {
            // npcResponse is handled by ExecuteToolCall.
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting take item: ItemName='{requestedItemName}', Target='{targetPlayerIdentifier}'");

            // --- 1. Find Target Player ---
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryTakeItem.");
                // TryChat(npc, $"Who's {targetPlayerIdentifier}?");
                return false;
            }

            // --- 2. Find Item in Player's Active Hand ---
            // We only check the *active* hand for simplicity now.
            EntityUid? itemToTake = null;
            if (_hands.TryGetActiveItem(targetPlayer.Value, out var activeItem))
            {
                var proto = Prototype(activeItem.Value);
                if (proto != null && string.Equals(proto.ID, requestedItemName, StringComparison.OrdinalIgnoreCase))
                {
                    itemToTake = activeItem;
                }
            }

            if (itemToTake == null || !itemToTake.Value.Valid)
            {
                _sawmill.Warning($"Player {ToPrettyString(targetPlayer.Value)} does not have '{requestedItemName}' in active hand for TryTakeItem.");
                // TryChat(npc, $"You don't seem to be holding a {requestedItemName}. Hold it out."); // Feedback
                return false;
            }

            // --- 3. Check Range & Interaction ---
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > 2.0f)
            {
                _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far for NPC {ToPrettyString(npc)} to take item.");
                // TryChat(npc, $"Bring that {requestedItemName} closer."); // Feedback
                return false;
            }

            // --- 4. Perform Transfer (Original drop + move logic) ---
            // Player drops the item at their feet first.
            // A real implementation might need a more robust interaction system (e.g., trade window).
            if (_hands.TryDrop(targetPlayer.Value, itemToTake.Value, checkActionBlocker: false)) // Player drops the item
            {
                // Now move the dropped item to the NPC's location
                var npcTransform = Transform(npc);
                var itemTransform = Transform(itemToTake.Value);
                itemTransform.Coordinates = npcTransform.Coordinates; // Move item to NPC's feet

                _sawmill.Info($"NPC {ToPrettyString(npc)} successfully received item {ToPrettyString(itemToTake.Value)} from {ToPrettyString(targetPlayer.Value)} (dropped at NPC location).");
                return true; // Success is player dropping the item near the NPC
            }
            else
            {
                _sawmill.Warning($"Player {ToPrettyString(targetPlayer.Value)} failed to drop item {ToPrettyString(itemToTake.Value)} for NPC {ToPrettyString(npc)}.");
                // TryChat(npc, $"Looks like you couldn't drop that {requestedItemName}. Try again?"); // Feedback
                return false;
            }
        }
        
        // TODO: Replace with robust inventory search
        private EntityUid? FindItemInHands(EntityUid holder, string itemName)
        {
            // Use TryComp to get the HandsComponent
            if (!TryComp<HandsComponent>(holder, out var handsComp))
                return null;

            // Now enumerate hands using the component
            foreach (var hand in _hands.EnumerateHands(holder, handsComp))
            {
                // Check if the hand is holding an entity
                if (hand.HeldEntity is { } heldEntityValue)
                {
                    var proto = Prototype(heldEntityValue);

                    if (proto != null && string.Equals(proto.ID, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        return heldEntityValue;
                    }
                }
            }
            return null;
        }
    }
}