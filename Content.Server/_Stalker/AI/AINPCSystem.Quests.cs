using Content.Server.Chat.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System;
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
        private string GetOfferQuestToolDescription(AiNpcComponent component)
        {
            var questItemsList = component.QuestItems
                .Select(item => $"- {item.ProtoId} (Rarity: {item.Rarity})")
                .ToList();
            var questItemsString = questItemsList.Count > 0
                ? string.Join("\n", questItemsList)
                : "None";

            var description = $@"Offer a quest to a player to retrieve ONE specific item. Checks if the player already has an active quest from you. Possible items to request:
                {questItemsString}";

            return $@"{{
                ""name"": ""TryOfferQuest"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""ckey"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (not name) of the player to offer the quest to.""
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""REQUIRED message the NPC says when offering the quest (e.g., 'Hey stalker, bring me a BoarHoof and I'll pay you.').""
                        }}
                    }},
                    ""required"": [""ckey"", ""npcResponse""]
                }}
            }}";
        }

        
        /// <summary>
        /// Offers a quest to the player if they don't already have one.
        /// TODO: Implement actual quest tracking.
        /// </summary>
        public bool TryOfferQuest(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string? npcResponse = null)
        {
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting to offer quest to: Target='{targetPlayerIdentifier}'");

            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryOfferQuest.");
                return false;
            }

            // TODO: Implement a real quest tracking system. This is just a placeholder.
            bool playerHasActiveQuest = false;
            if (playerHasActiveQuest)
            {
                _sawmill.Info($"Player {ToPrettyString(targetPlayer.Value)} already has an active quest from {ToPrettyString(npc)}. Cannot offer another.");
                return false;
            }

            if (aiComp.QuestItems == null || aiComp.QuestItems.Count == 0)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to offer quest, but has no QuestItems defined.");
                return false;
            }

            // TODO: Implement better quest selection logic (e.g., based on rarity, player level, etc.)
            var random = new Random();
            var questItemInfo = aiComp.QuestItems[random.Next(aiComp.QuestItems.Count)];
            var questItemId = questItemInfo.ProtoId;

            _sawmill.Info($"NPC {ToPrettyString(npc)} offered quest for '{questItemId}' to {ToPrettyString(targetPlayer.Value)}. (Quest tracking not implemented)");

            // The actual quest offer text (e.g., "Bring me a DogTail") should be in the npcResponse parameter,
            // which was already spoken by ExecuteToolCall. We don't need to chat again here unless
            // the npcResponse was missing (which the tool description requires).
            if (string.IsNullOrWhiteSpace(npcResponse))
            {
                _sawmill.Warning($"TryOfferQuest called without npcResponse for {ToPrettyString(npc)}. Tool description requires it.");
                TryChat(npc, $"Hey {Name(targetPlayer.Value)}, I need someone to fetch me a {questItemId}. Interested?");
            }


            return true;
        }

        private string GetCompleteQuestToolDescription(AiNpcComponent component)
        {
            var rewardItemsList = component.GivableItems
                .Select(item => $"- {item.ProtoId} (Max: {item.MaxQuantity})")
                .ToList();
            var rewardItemsString = rewardItemsList.Count > 0
                ? string.Join("\n", rewardItemsList)
                : "None";

            var description = $@"Attempt to complete a quest for a player. First, takes the required quest item from their active hand. If successful, gives them the specified reward item. Possible reward items:
                {rewardItemsString}";

            return $@"{{
                ""name"": ""TryCompleteQuest"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""ckey"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (not name) of the player completing the quest.""
                        }},
                        ""questItemId"": {{
                            ""type"": ""string"",
                            ""description"": ""The exact prototype ID of the item the player should be handing over (e.g., 'MutantPartBoarHoof').""
                        }},
                        ""rewardItemId"": {{
                            ""type"": ""string"",
                            ""description"": ""The exact prototype ID of the item to give as a reward from the allowed list.""
                        }},
                        ""rewardQuantity"": {{
                            ""type"": ""integer"",
                            ""description"": ""Number of reward items to give (defaults to 1, respects max quantity)."",
                            ""default"": 1
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""REQUIRED message the NPC says during the exchange (e.g., 'Good work, here's your payment.').""
                        }}
                    }},
                    ""required"": [""ckey"", ""questItemId"", ""rewardItemId"", ""npcResponse""]
                }}
            }}";
        }

        /// <summary>
        /// Attempts to take a quest item from a player and then give them a reward.
        /// </summary>
        public bool TryCompleteQuest(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string questItemId, string rewardItemId, int rewardQuantity, string? npcResponse = null)
        {
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting complete quest: Take='{questItemId}', Give='{rewardItemId}' x{rewardQuantity}, Target='{targetPlayerIdentifier}'");

            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryCompleteQuest.");
                return false;
            }

            // We pass null for npcResponse here because the main response is handled by ExecuteToolCall
            // and we don't want duplicate messages if TryTakeItem has its own feedback.
            // A more refined approach might pass specific feedback messages for each step.
            if (!TryTakeItem(npc, targetPlayerIdentifier, questItemId, null))
            {
                _sawmill.Warning($"Failed to take quest item '{questItemId}' from {ToPrettyString(targetPlayer.Value)} during TryCompleteQuest for NPC {ToPrettyString(npc)}.");
                return false;
            }

            _sawmill.Info($"NPC {ToPrettyString(npc)} successfully took quest item '{questItemId}' from {ToPrettyString(targetPlayer.Value)}.");

            rewardQuantity = Math.Max(1, rewardQuantity);

            if (!TryGiveItem(npc, targetPlayerIdentifier, rewardItemId, rewardQuantity, null))
            {
                _sawmill.Warning($"Failed to give reward item '{rewardItemId}' x{rewardQuantity} to {ToPrettyString(targetPlayer.Value)} after taking quest item for NPC {ToPrettyString(npc)}.");
                return false;
            }

            _sawmill.Info($"NPC {ToPrettyString(npc)} successfully gave reward '{rewardItemId}' x{rewardQuantity} to {ToPrettyString(targetPlayer.Value)}.");

            _sawmill.Info($"Quest for '{questItemId}' considered complete for player {ToPrettyString(targetPlayer.Value)} by NPC {ToPrettyString(npc)}. (Quest tracking not implemented)");

            return true;
        }

    }
}
