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
using Content.Server._Stalker.Sponsors;
using Content.Server._Stalker.Sponsors.SponsorManager;

namespace Content.Server._Stalker.AI
{
    public sealed partial class AINPCSystem : SharedAiNpcSystem
    {
        [Dependency] private readonly AIManager _aiManager = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly EntityManager _entity = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly AudioSystem _audio = default!;
        [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
        [Dependency] private readonly SponsorsManager _sponsorsManager = default!;

        private ISawmill _sawmill = default!;
        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("ai.npc.system");

            SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
            SubscribeLocalEvent<ProcessAIResponseEvent>(HandleAIResponse);
            SubscribeLocalEvent<AiNpcComponent, ComponentShutdown>(OnComponentRemoved);

            _sawmill.Info("AI NPC System Initialized");
        }
        private readonly Dictionary<EntityUid, CancellationTokenSource> _ongoingRequests = new();
        private readonly Dictionary<EntityUid, Dictionary<string, List<OpenRouterMessage>>> _conversationHistories = new();

        private void OnEntitySpoke(EntitySpokeEvent args)
        {
            if (string.IsNullOrWhiteSpace(args.Message) ||
                HasComp<AiNpcComponent>(args.Source) ||
                !HasComp<ActorComponent>(args.Source))
                return;

            var speakerName = Name(args.Source);
            string? speakerCKey = null;
            if (TryComp<ActorComponent>(args.Source, out var actor))
            {
                speakerCKey = actor.PlayerSession.Name;
            }

            if (speakerCKey == null)
            {
                _sawmill.Warning($"Could not get CKey for speaker {ToPrettyString(args.Source)}. Cannot process AI interaction.");
                return;
            }
            var query = EntityQueryEnumerator<AiNpcComponent, TransformComponent>();
            while (query.MoveNext(out var npcUid, out var aiComp, out var npcTransform))
            {
                if (npcUid == args.Source)
                    continue;

                if (!aiComp.Enabled)
                    continue;

                if (!EntityManager.TryGetComponent<TransformComponent>(args.Source, out var sourceTransform))
                    continue;

                float interactionRange = aiComp.InteractionRange;
                if (!npcTransform.Coordinates.TryDistance(EntityManager, sourceTransform.Coordinates, out var distance) || distance > interactionRange)
                    continue;

                if (aiComp.SponsorOnly)
                {
                    if (actor != null && !_sponsorsManager.HavePriorityJoin(actor.PlayerSession.UserId))
                    {
                        _sawmill.Debug($"NPC {ToPrettyString(npcUid)} (SponsorOnly) ignored non-sponsor {ToPrettyString(args.Source)} ({speakerCKey}).");

                        var replyMessage = Loc.GetString("st-ai-npc-sponsor-only-reply");

                        TryChat(npcUid, replyMessage);
                        continue;
                    }
                    _sawmill.Debug($"NPC {ToPrettyString(npcUid)} (SponsorOnly) processing sponsor {ToPrettyString(args.Source)} ({speakerCKey}).");
                }

                if (_ongoingRequests.ContainsKey(npcUid))
                {
                    _sawmill.Debug($"AI request already in progress for NPC {ToPrettyString(npcUid)}. Ignoring speech from {ToPrettyString(args.Source)}.");
                    continue;
                }
                _sawmill.Debug($"NPC {ToPrettyString(npcUid)} heard speech from {ToPrettyString(args.Source)}: \"{args.Message}\"");

                TrimHistory(npcUid, speakerCKey, aiComp, 1);
                AddMessageToHistory(npcUid, speakerCKey, aiComp, "user", args.Message, speakerName, speakerCKey);

                var tools = GetAvailableToolDescriptions(npcUid, aiComp);
                var history = GetHistoryForNpcAndPlayer(npcUid, speakerCKey);
                var prompt = aiComp.BasePrompt;

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                _ongoingRequests[npcUid] = cts;

                Task.Run(async () =>
                {
                    try
                    {
                        var response = await _aiManager.GetActionAsync(npcUid, prompt, history, string.Empty, tools, cts.Token);

                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, response));
                    }
                    catch (OperationCanceledException)
                    {
                        _sawmill.Debug($"AI request for NPC {ToPrettyString(npcUid)} timed out or was cancelled.");
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, AIResponse.Failure("Request timed out or cancelled.")));
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Unhandled exception during async AI request for {ToPrettyString(npcUid)}: {e}");
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, AIResponse.Failure($"Internal error: {e.Message}")));
                    }
                }, cts.Token);
            }
        }
        private sealed class ProcessAIResponseEvent : EntityEventArgs
        {
            public EntityUid TargetNpc { get; }
            public string PlayerCKey { get; }
            public AIResponse Response { get; }
            public ProcessAIResponseEvent(EntityUid targetNpc, string playerCKey, AIResponse response)
            {
                TargetNpc = targetNpc;
                PlayerCKey = playerCKey;
                Response = response;
            }
        }
        private void HandleAIResponse(ProcessAIResponseEvent args)
        {
            var npcUid = args.TargetNpc;
            var playerCKey = args.PlayerCKey; if (!TryComp<AiNpcComponent>(npcUid, out var component))
                return;

            if (_ongoingRequests.Remove(npcUid, out var cts))
            {
                cts.Dispose();
            }

            var response = args.Response; if (!response.Success)
            {
                _sawmill.Warning($"AI request failed for NPC {ToPrettyString(npcUid)} (Player: {playerCKey}): {response.ErrorMessage}");
                return;
            }

            if (response.TextResponse != null)
            {
                _sawmill.Debug($"NPC {ToPrettyString(npcUid)} received text response for Player {playerCKey}: {response.TextResponse}");
                TryChat(npcUid, response.TextResponse);

                TrimHistory(npcUid, playerCKey, component, 1);

                AddMessageToHistory(npcUid, playerCKey, component, "assistant", response.TextResponse, null, null);
            }
            else if (response.ToolCallRequests != null && response.ToolCallRequests.Count > 0)
            {
                _sawmill.Debug($"NPC {ToPrettyString(npcUid)} received {response.ToolCallRequests.Count} tool call requests for Player {playerCKey}."); var assistantToolCalls = response.ToolCallRequests.Select(tc => new OpenRouterToolCall
                {
                    Id = tc.ToolCallId,
                    Type = "function",
                    Function = new OpenRouterToolFunction { Name = tc.ToolName, Arguments = tc.Arguments.ToJsonString() }
                }).ToList();
                AddMessageToHistory(npcUid, playerCKey, component, "assistant", null, null, null, assistantToolCalls); foreach (var toolCall in response.ToolCallRequests)
                {
                    _sawmill.Debug($"Executing tool call: {toolCall.ToolName} (ID: {toolCall.ToolCallId}) for Player {playerCKey}");
                    var (success, resultMessage) = ExecuteToolCall(npcUid, component, toolCall);

                    TrimHistory(npcUid, playerCKey, component, 1);
                    AddMessageToHistory(npcUid, playerCKey, component, "tool", resultMessage, null, null, null, toolCall.ToolCallId);

                    _sawmill.Info($"Tool '{toolCall.ToolName}' (ID: {toolCall.ToolCallId}) executed for NPC {ToPrettyString(npcUid)} (Player: {playerCKey}). Success: {success}. Result: {resultMessage}");

                }
            }
            else
            {
                _sawmill.Warning($"AI response for NPC {ToPrettyString(npcUid)} (Player: {playerCKey}) was successful but contained neither text nor tool call.");
            }
        }

        /// <summary>
        /// Executes the requested tool call and returns success status and a result message.
        /// Handles the optional npcResponse parameter for simultaneous chat.
        /// </summary>
        private (bool Success, string ResultMessage) ExecuteToolCall(EntityUid uid, AiNpcComponent component, AIToolCall toolCall)
        {
            bool success = false;
            string resultMessage = $"Unknown tool name: {toolCall.ToolName}"; // Default failure message
            string? npcResponse = null;
            if (TryGetStringArgument(toolCall.Arguments, "npcResponse", out var responseMsg) && !string.IsNullOrWhiteSpace(responseMsg))
            {
                npcResponse = responseMsg;
                TryChat(uid, npcResponse);
                _sawmill.Debug($"NPC {ToPrettyString(uid)} saying via npcResponse: '{npcResponse}' while executing {toolCall.ToolName}");
            }

            try
            {
                switch (toolCall.ToolName)
                {
                    case nameof(TryChat):
                        if (!component.CanChat)
                        {
                            resultMessage = "TryChat tool is disabled for this NPC.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "message", out var message))
                        {
                            success = TryChat(uid, message, npcResponse);
                            resultMessage = success ? "Chat action performed." : "Chat action failed.";
                        }
                        else resultMessage = "Missing or invalid 'message' argument for TryChat.";
                        break;

                    case nameof(TryGiveItem):
                        if (!component.CanGiveItems)
                        {
                            resultMessage = "TryGiveItem tool is disabled for this NPC.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "ckey", out var targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "itemPrototypeId", out var itemPrototypeId))
                        {
                            TryGetIntArgument(toolCall.Arguments, "quantity", out var quantity);
                            quantity = Math.Max(1, quantity);
                            success = TryGiveItem(uid, targetPlayer, itemPrototypeId, quantity, npcResponse);
                            resultMessage = success ? "Give item action performed." : "Give item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryGiveItem (expected targetPlayer, itemPrototypeId).";
                        break;

                    case nameof(TryTakeItem):
                        if (!component.CanTakeItems)
                        {
                            resultMessage = "TryTakeItem tool is disabled for this NPC.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "ckey", out targetPlayer) &&
                           TryGetStringArgument(toolCall.Arguments, "requestedItemName", out var requestedItemName))
                        {
                            success = TryTakeItem(uid, targetPlayer, requestedItemName, npcResponse);
                            resultMessage = success ? "Take item action performed." : "Take item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryTakeItem (expected targetPlayer, requestedItemName).";
                        break;

                    case nameof(TryPunishPlayer):
                        if (!component.CanPunish)
                        {
                            resultMessage = "TryPunishPlayer tool is disabled for this NPC.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "ckey", out targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "reason", out var reason))
                        {
                            success = TryPunishPlayer(uid, component, targetPlayer, reason, npcResponse);
                            resultMessage = success ? "Punish player action performed." : "Punish player action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryPunishPlayer (expected targetPlayer, reason).";
                        break;

                    case nameof(TryOfferQuest):
                        if (!component.CanOfferQuests)
                        {
                            resultMessage = "TryOfferQuest tool is disabled for this NPC.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "ckey", out targetPlayer))
                        {
                            success = TryOfferQuest(uid, component, targetPlayer, npcResponse);
                            resultMessage = success ? "Offer quest action performed." : "Offer quest action failed.";
                        }
                        else resultMessage = "Missing or invalid 'targetPlayer' argument for TryOfferQuest.";
                        break;

                    case nameof(TryCompleteQuest):
                        if (!component.CanOfferQuests)
                        {
                            resultMessage = "TryCompleteQuest tool requires canOfferQuests to be enabled.";
                            break;
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "ckey", out targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "questItemId", out var questItemId) &&
                            TryGetStringArgument(toolCall.Arguments, "rewardItemId", out var rewardItemId))
                        {
                            TryGetIntArgument(toolCall.Arguments, "rewardQuantity", out var rewardQuantity);
                            rewardQuantity = Math.Max(1, rewardQuantity);
                            success = TryCompleteQuest(uid, component, targetPlayer, questItemId, rewardItemId, rewardQuantity, npcResponse);
                            resultMessage = success ? "Complete quest action performed." : "Complete quest action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryCompleteQuest (expected targetPlayer, questItemId, rewardItemId).";
                        break;

                    default:
                        _sawmill.Warning($"NPC {ToPrettyString(uid)} received request for unknown tool: {toolCall.ToolName}");
                        break;
                }
            }
            catch (Exception e)
            {
                _sawmill.Error($"Exception while executing tool '{toolCall.ToolName}' for NPC {ToPrettyString(uid)}: {e}");
                resultMessage = $"Internal error executing tool: {e.Message}";
                success = false;
            }

            return (success, resultMessage);
        }

        /// <summary>
        /// Helper to safely extract string arguments from JsonObject
        /// </summary>
        private bool TryGetStringArgument(JsonObject args, string key, out string value)
        {
            value = string.Empty;
            if (args.TryGetPropertyValue(key, out var node) && node is JsonValue val && val.TryGetValue(out string? strValue))
            {
                value = strValue ?? string.Empty;
                return true;
            }
            _sawmill.Warning($"Failed to get string argument '{key}' from tool call arguments: {args.ToJsonString()}");
            return false;
        }


        /// <summary>
        /// Gets the conversation history list for a specific NPC and Player CKey,
        /// creating the necessary dictionaries and list if they don't exist.
        /// </summary>
        private List<OpenRouterMessage> GetHistoryForNpcAndPlayer(EntityUid npcUid, string playerCKey)
        {
            if (!_conversationHistories.TryGetValue(npcUid, out var npcHistories))
            {
                npcHistories = new Dictionary<string, List<OpenRouterMessage>>();
                _conversationHistories[npcUid] = npcHistories;
            }

            if (!npcHistories.TryGetValue(playerCKey, out var playerHistory))
            {
                playerHistory = new List<OpenRouterMessage>();
                npcHistories[playerCKey] = playerHistory;
            }

            return playerHistory;
        }

        /// <summary>
        /// Helper to safely extract int arguments from JsonObject
        /// </summary>
        private bool TryGetIntArgument(JsonObject args, string key, out int value)
        {
            value = 0;
            if (args.TryGetPropertyValue(key, out var node) && node is JsonValue val && val.TryGetValue(out int intValue))
            {
                value = intValue;
                return true;
            }
            // Try parsing from string as fallback
            if (args.TryGetPropertyValue(key, out var strNode) && strNode is JsonValue strVal && strVal.TryGetValue(out string? strString) && int.TryParse(strString, out int parsedInt))
            {
                value = parsedInt;
                return true;
            }
            _sawmill.Warning($"Failed to get int argument '{key}' from tool call arguments: {args.ToJsonString()}");
            return false;
        }


        /// <summary>
        /// Adds a message to the specific player's conversation history for the given NPC.
        /// Does NOT handle trimming. Corrected signature.
        /// </summary>
        private void AddMessageToHistory(EntityUid npcUid, string playerCKey, AiNpcComponent component, string role, string? content, string? speakerName = null, string? speakerCKey = null, List<OpenRouterToolCall>? toolCalls = null, string? toolCallId = null)
        {
            var history = GetHistoryForNpcAndPlayer(npcUid, playerCKey);

            string? sanitizedName = null;
            if (!string.IsNullOrEmpty(speakerName))
            {
                sanitizedName = SanitizeNameForApi(speakerName);
                if (string.IsNullOrWhiteSpace(sanitizedName))
                {
                    sanitizedName = "UnknownSpeaker";
                }
            }

            // Prepend CKey to user message content if available
            string? finalContent = content;
            if (role == "user" && !string.IsNullOrWhiteSpace(speakerCKey) && !string.IsNullOrWhiteSpace(content))
            {
                finalContent = $"[CKEY: {speakerCKey}] {content}";
                _sawmill.Debug($"Prepended CKey to user message for {speakerCKey}. New content preview: {finalContent.Substring(0, Math.Min(finalContent.Length, 50))}...");
            }

            history.Add(new OpenRouterMessage { Role = role, Content = finalContent, Name = sanitizedName, ToolCalls = toolCalls, ToolCallId = toolCallId });

        }

        /// <summary>
        /// Removes characters from a name that are not letters (including Cyrillic) to comply with API requirements.
        /// </summary>
        private string SanitizeNameForApi(string name)
        {
            // Keep only Unicode letters (covers Latin, Cyrillic, etc.)
            // Remove spaces, brackets, symbols, numbers.
            return System.Text.RegularExpressions.Regex.Replace(name, @"[^\p{L}]", "");
        }


        /// <summary>
        /// Trims the history list for a specific NPC and Player if it exceeds the max limit,
        /// making space for a specified number of new messages.
        /// Removes messages from the beginning (oldest).
        /// Fixed CS1061: Uses MaxHistoryPerPlayer.
        /// </summary>
        private void TrimHistory(EntityUid npcUid, string playerCKey, AiNpcComponent component, int spaceNeeded)
        {
            // Get the specific history list for this NPC and Player
            var history = GetHistoryForNpcAndPlayer(npcUid, playerCKey);
            int maxAllowed = component.MaxHistoryPerPlayer;

            int removeCount = (history.Count + spaceNeeded) - maxAllowed;

            if (removeCount > 0)
            {
                int actualRemoveCount = Math.Min(removeCount, history.Count);
                if (actualRemoveCount > 0)
                {
                    history.RemoveRange(0, actualRemoveCount);
                    _sawmill.Debug($"Trimmed {actualRemoveCount} messages from history for NPC {ToPrettyString(npcUid)}, Player {playerCKey}. New count: {history.Count}");
                }
            }
        }

        /// <summary>
        /// Cleanup history when component is removed
        /// </summary>
        public override void Shutdown()
        {
            base.Shutdown();
            _conversationHistories.Clear();
            _ongoingRequests.Clear();
        }

        private void OnComponentRemoved(EntityUid uid, AiNpcComponent component, ComponentShutdown args)
        {
            // Remove the entire entry for the NPC, which includes all player histories within it.
            _conversationHistories.Remove(uid);

            // Cancel and remove any ongoing request for this NPC
            if (_ongoingRequests.Remove(uid, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }


        /// <summary>
        /// Tool description methods
        /// </summary>
        private List<string> GetAvailableToolDescriptions(EntityUid uid, AiNpcComponent component)
        {
            var descriptions = new List<string>();

            if (component.CanChat)
                descriptions.Add(GetChatToolDescription());

            if (component.CanGiveItems && component.GivableItems.Count > 0)
            {
                descriptions.Add(GetGiveItemToolDescription(component));
            }

            if (component.CanOfferQuests && component.QuestItems.Count > 0)
            {
                descriptions.Add(GetOfferQuestToolDescription(component));
            }

            if (component.CanTakeItems)
                descriptions.Add(GetTakeItemToolDescription());

            if (component.CanOfferQuests && component.QuestItems.Count > 0 && component.GivableItems.Count > 0)
            {
                descriptions.Add(GetCompleteQuestToolDescription(component));
            }

            if (component.CanPunish && (component.PunishmentDamage != null || component.PunishmentSound != null))
            {
                descriptions.Add(GetPunishPlayerToolDescription());
            }
            return descriptions;
        }

        /// <summary>
        /// Finds a player entity based *only* on their CKey (username).
        /// </summary>
        private EntityUid? FindPlayerByIdentifier(string ckeyIdentifier)
        {
            var query = EntityQueryEnumerator<ActorComponent>();
            while (query.MoveNext(out var uid, out var actor))
            {
                if (actor.PlayerSession.Name.Equals(ckeyIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    return uid;
                }
            }
            _sawmill.Warning($"Could not find player with CKey: {ckeyIdentifier}");
            return null;
        }
    }
}
