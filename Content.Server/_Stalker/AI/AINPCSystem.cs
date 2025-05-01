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
    public sealed class AINPCSystem : SharedAiNpcSystem
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

        private ISawmill _sawmill = default!;
        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("ai.npc.system");

            SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
            SubscribeLocalEvent<ProcessAIResponseEvent>(HandleAIResponse);

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

            // If we couldn't get a CKey for the speaker, we cannot track history per player.
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

                // Prevent spamming requests for the same NPC
                if (_ongoingRequests.ContainsKey(npcUid))
                {
                    _sawmill.Debug($"AI request already in progress for NPC {ToPrettyString(npcUid)}. Ignoring speech from {ToPrettyString(args.Source)}.");
                    continue;
                }
                _sawmill.Debug($"NPC {ToPrettyString(npcUid)} heard speech from {ToPrettyString(args.Source)}: \"{args.Message}\"");

                TrimHistory(npcUid, speakerCKey, aiComp, 1);
                AddMessageToHistory(npcUid, speakerCKey, aiComp, "user", args.Message, speakerName, speakerCKey);

                // Prepare data for AI Manager
                var tools = GetAvailableToolDescriptions(npcUid, aiComp);
                // Get the specific player's history from our internal dictionary
                var history = GetHistoryForNpcAndPlayer(npcUid, speakerCKey);
                var prompt = aiComp.BasePrompt;
                // Note: userMessage is already part of the history list passed to AIManager
                // var userMessage = args.Message; // We don't need to pass this separately anymore

                // Create a cancellation token for this request
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Added 30s timeout
                _ongoingRequests[npcUid] = cts;

                // Call AIManager asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        // Pass the specific player's history to the AI Manager
                        // Fixed CS1503: Re-adding placeholder for potentially expected userMessage argument
                        var response = await _aiManager.GetActionAsync(npcUid, prompt, history, string.Empty, tools, cts.Token); // Added string.Empty placeholder

                        // Queue the response processing back to the main game thread, including the player CKey
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, response));
                    }
                    catch (OperationCanceledException)
                    {
                        _sawmill.Debug($"AI request for NPC {ToPrettyString(npcUid)} timed out or was cancelled.");
                        // Queue a failure response to ensure state is cleaned up
                        // Fixed CS7036: Add missing 'response' argument
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, AIResponse.Failure("Request timed out or cancelled.")));
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Unhandled exception during async AI request for {ToPrettyString(npcUid)}: {e}");
                        // Fixed CS7036: Add missing 'response' argument
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, speakerCKey, AIResponse.Failure($"Internal error: {e.Message}")));
                    }
                    // No finally block needed here for removal, HandleAIResponse will do it.
                    // Dispose CTS on the main thread in HandleAIResponse.
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
                // Trim the specific player's history *before* adding the assistant message
                // Trim the specific player's history *before* adding the assistant message
                TrimHistory(npcUid, playerCKey, component, 1); // Error CS0103 fixed by re-adding TrimHistory below
                // Add assistant's response to the specific player's history
                // Fixed CS1503: Correct arguments for AddMessageToHistory
                AddMessageToHistory(npcUid, playerCKey, component, "assistant", response.TextResponse, null, null);
            }
            else if (response.ToolCallRequests != null && response.ToolCallRequests.Count > 0) // Check for multiple tool calls
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

                    // TODO: Consider if we need to immediately re-query the AI after *each* tool result,
                    // or only after all requested tools in a batch are executed.
                    // Current implementation executes all requested tools before potentially re-querying (if implemented).
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
                            break; // Skip execution
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "message", out var message))
                        {
                            success = TryChat(uid, message, npcResponse);
                            resultMessage = success ? "Chat action performed." : "Chat action failed.";
                        }
                        else resultMessage = "Missing or invalid 'message' argument for TryChat.";
                        break;

                    case nameof(TryGiveItem): // Use nameof
                        if (!component.CanGiveItems)
                        {
                            resultMessage = "TryGiveItem tool is disabled for this NPC.";
                            break; // Skip execution
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out var targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "itemPrototypeId", out var itemPrototypeId))
                        {
                            TryGetIntArgument(toolCall.Arguments, "quantity", out var quantity);
                            quantity = Math.Max(1, quantity);
                            success = TryGiveItem(uid, targetPlayer, itemPrototypeId, quantity, npcResponse); // Pass npcResponse
                            resultMessage = success ? "Give item action performed." : "Give item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryGiveItem (expected targetPlayer, itemPrototypeId).";
                        break;

                    case nameof(TryTakeItem): // Use nameof
                        if (!component.CanTakeItems)
                        {
                            resultMessage = "TryTakeItem tool is disabled for this NPC.";
                            break; // Skip execution
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out targetPlayer) &&
                           TryGetStringArgument(toolCall.Arguments, "requestedItemName", out var requestedItemName))
                        {
                            success = TryTakeItem(uid, targetPlayer, requestedItemName, npcResponse); // Pass npcResponse
                            resultMessage = success ? "Take item action performed." : "Take item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryTakeItem (expected targetPlayer, requestedItemName).";
                        break;

                    case nameof(TryPunishPlayer): // Added case for the new tool
                        if (!component.CanPunish)
                        {
                            resultMessage = "TryPunishPlayer tool is disabled for this NPC.";
                            break; // Skip execution
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "reason", out var reason)) // Added reason argument
                        {
                            success = TryPunishPlayer(uid, component, targetPlayer, reason, npcResponse); // Pass npcResponse
                            resultMessage = success ? "Punish player action performed." : "Punish player action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryPunishPlayer (expected targetPlayer, reason).";
                        break;

                    case nameof(TryOfferQuest): // Added case for the new quest tool
                        if (!component.CanOfferQuests)
                        {
                            resultMessage = "TryOfferQuest tool is disabled for this NPC.";
                            break; // Skip execution
                        }
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out targetPlayer))
                        {
                            success = TryOfferQuest(uid, component, targetPlayer, npcResponse); // Pass npcResponse
                            resultMessage = success ? "Offer quest action performed." : "Offer quest action failed.";
                        }
                        else resultMessage = "Missing or invalid 'targetPlayer' argument for TryOfferQuest.";
                        break;

                    default:
                        _sawmill.Warning($"NPC {ToPrettyString(uid)} received request for unknown tool: {toolCall.ToolName}");
                        // resultMessage remains "Unknown tool name."
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

        // Helper to safely extract string arguments from JsonObject
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
            // Get or create the outer dictionary for the NPC
            if (!_conversationHistories.TryGetValue(npcUid, out var npcHistories))
            {
                npcHistories = new Dictionary<string, List<OpenRouterMessage>>();
                _conversationHistories[npcUid] = npcHistories;
            }

            // Get or create the inner list for the specific player
            if (!npcHistories.TryGetValue(playerCKey, out var playerHistory))
            {
                playerHistory = new List<OpenRouterMessage>();
                npcHistories[playerCKey] = playerHistory;
            }

            return playerHistory;
        }

        // Helper to safely extract int arguments from JsonObject
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
            // Get the specific history list for this NPC and Player
            var history = GetHistoryForNpcAndPlayer(npcUid, playerCKey);

            // Sanitize speaker name for the API if it's provided
            string? sanitizedName = null;
            if (!string.IsNullOrEmpty(speakerName))
            {
                sanitizedName = SanitizeNameForApi(speakerName);
                // If sanitization results in an empty string, set it to null or a default placeholder
                if (string.IsNullOrWhiteSpace(sanitizedName))
                {
                    sanitizedName = "UnknownSpeaker"; // Or null, depending on API requirements
                }
            }

            // Basic history addition using the sanitized name
            history.Add(new OpenRouterMessage { Role = role, Content = content, Name = sanitizedName, CKey = speakerCKey, ToolCalls = toolCalls, ToolCallId = toolCallId });

            // Trimming is handled by TrimHistory calls before this method.
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
            int maxAllowed = component.MaxHistoryPerPlayer; // Use the new field

            // Calculate how many messages *currently* exceed the limit if we add the new ones
            int removeCount = (history.Count + spaceNeeded) - maxAllowed;

            if (removeCount > 0)
            {
                // Ensure we don't try to remove more messages than exist
                int actualRemoveCount = Math.Min(removeCount, history.Count);
                if (actualRemoveCount > 0)
                {
                    history.RemoveRange(0, actualRemoveCount);
                    _sawmill.Debug($"Trimmed {actualRemoveCount} messages from history for NPC {ToPrettyString(npcUid)}, Player {playerCKey}. New count: {history.Count}");
                }
            }
        }

        // Cleanup history when component is removed
        public override void Shutdown()
        {
            base.Shutdown();
            _conversationHistories.Clear();
            _ongoingRequests.Clear(); // Also clear ongoing requests
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


        // Tool description methods
        private List<string> GetAvailableToolDescriptions(EntityUid uid, AiNpcComponent component)
        {
            var descriptions = new List<string>();

            if (component.CanChat)
                descriptions.Add(GetChatToolDescription());

            // Only add GiveItem tool if the NPC actually has givable items defined
            // AND the tool is enabled
            if (component.CanGiveItems && component.GivableItems.Count > 0)
            {
                descriptions.Add(GetGiveItemToolDescription(component)); // Still needs component for item list
            }

            // Only add OfferQuest tool if the NPC actually has quest items defined
            // AND the tool is enabled
            if (component.CanOfferQuests && component.QuestItems.Count > 0)
            {
                descriptions.Add(GetOfferQuestToolDescription(component)); // Needs component for item list
            }

            if (component.CanTakeItems)
                descriptions.Add(GetTakeItemToolDescription());

            // Add the punish tool description if the component allows it
            // AND the tool is enabled
            if (component.CanPunish && (component.PunishmentDamage != null || component.PunishmentSound != null))
            {
                descriptions.Add(GetPunishPlayerToolDescription()); // No longer needs component
            }
            return descriptions;
        }

        private string GetChatToolDescription() // Removed component parameter
        {
            // Simplified description
            var description = "Respond verbally to a user or initiate conversation.";

            return $@"{{
                ""name"": ""TryChat"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                        ""message"": {{
                            ""type"": ""string"",
                            ""description"": ""The primary message the NPC should say.""
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""Optional alternative message the NPC says. If provided, this is spoken instead of 'message'.""
                        }}
                    }},
                    ""required"": [""message""]
                }}
            }}";
        }


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
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (username) of the player to give the item to.""
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
                    ""required"": [""targetPlayer"", ""itemPrototypeId""]
                }}
            }}";
        }

        private string GetOfferQuestToolDescription(AiNpcComponent component)
        {
            // Build the list of possible quest items
            var questItemsList = component.QuestItems
                .Select(item => $"- {item.ProtoId} (Rarity: {item.Rarity})")
                .ToList();
            var questItemsString = questItemsList.Count > 0
                ? string.Join("\n", questItemsList)
                : "None";

            // Description for the new quest tool
            var description = $@"Offer a quest to a player to retrieve ONE specific item. Checks if the player already has an active quest from you. Possible items to request:
                {questItemsString}";

            return $@"{{
                ""name"": ""TryOfferQuest"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (username) of the player to offer the quest to.""
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""REQUIRED message the NPC says when offering the quest (e.g., 'Hey stalker, bring me a BoarHoof and I'll pay you.').""
                        }}
                    }},
                    ""required"": [""targetPlayer"", ""npcResponse""]
                }}
            }}";
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
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (username) of the player to request the item from.""
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
                    ""required"": [""targetPlayer"", ""requestedItemName"", ""npcResponse""]
                }}
            }}";
        }

        private string GetPunishPlayerToolDescription() // Removed component parameter
        {
            // Simplified description
            var description = "Punish a player perceived as rude, lying, or hostile by attacking them. Use sparingly.";

            return $@"{{
                ""name"": ""TryPunishPlayer"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (username) of the player to punish.""
                        }},
                        ""reason"": {{
                            ""type"": ""string"",
                            ""description"": ""A brief reason for the punishment (e.g., 'Insults', 'Attempted scam').""
                        }},
                        ""npcResponse"": {{
                            ""type"": ""string"",
                            ""description"": ""Optional message the NPC shouts while punishing (e.g., 'You asked for it!').""
                        }}
                    }},
                    ""required"": [""targetPlayer"", ""reason""]
                }}
            }}";
        }


        // Actual tool methods, invoked locally after AI response parsing

        /// <summary>
        /// Makes the NPC speak. Handles the npcResponse parameter to avoid double-speaking if called from ExecuteToolCall.
        /// </summary>
        public bool TryChat(EntityUid npc, string message, string? npcResponse = null)
        {
            // Prioritize npcResponse if provided, otherwise use message.
            var messageToSpeak = !string.IsNullOrWhiteSpace(npcResponse) ? npcResponse : message;

            if (string.IsNullOrWhiteSpace(messageToSpeak))
                return false;

            // Avoid logging the same message twice if npcResponse was handled in ExecuteToolCall
            if (messageToSpeak != npcResponse)
                _sawmill.Debug($"NPC {ToPrettyString(npc)} executing chat: {messageToSpeak}");

            _chatSystem.TrySendInGameICMessage(npc, messageToSpeak, InGameICChatType.Speak, hideChat: false);
            return true;
        }


        // Updated TryGiveItem to include npcResponse
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

        /// <summary>
        /// Attempts to punish a player by applying damage.
        /// </summary>
        public bool TryPunishPlayer(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string reason, string? npcResponse = null)
        {
            // npcResponse is handled by ExecuteToolCall.
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting to punish player: Target='{targetPlayerIdentifier}', Reason='{reason}'");

            // --- 1. Find Target Player ---
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryPunishPlayer.");
                return false;
            }


            // Check if the target is whitelisted from punishment by this NPC
            if (aiComp.PunishmentWhitelist != null && _whitelistSystem.IsWhitelistPass(aiComp.PunishmentWhitelist, targetPlayer.Value))
            {
                _sawmill.Info($"Target player {ToPrettyString(targetPlayer.Value)} is whitelisted. Punishment aborted.");
                // TryChat(npc, $"I can't touch {Name(targetPlayer.Value)}."); // Feedback
                return false;
            }

            // --- 3. Check Range ---
            const float punishRange = 5.0f;
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > punishRange)
            {
                _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far ({distance}m) for NPC {ToPrettyString(npc)} to punish.");
                // TryChat(npc, $"Get back here, coward!"); // Feedback
                return false;
            }

            // --- 4. Apply Punishment ---
            bool damageApplied = false;
            if (aiComp.PunishmentDamage != null)
            {
                var damageResult = _damageable.TryChangeDamage(targetPlayer.Value, aiComp.PunishmentDamage, ignoreResistances: true);
                damageApplied = damageResult != null; // Don't fucking touch it. There is no Total damage!
            }
            else
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to punish, but no PunishmentDamage is defined in its AiNpcComponent.");
            }

            // Play sound effect if defined
            if (aiComp.PunishmentSound != null)
            {
                _audio.PlayPvs(aiComp.PunishmentSound, Transform(npc).Coordinates);
            }

            return damageApplied;
        }

        /// <summary>
        /// Offers a quest to the player if they don't already have one.
        /// TODO: Implement actual quest tracking.
        /// </summary>
        public bool TryOfferQuest(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string? npcResponse = null)
        {
            // npcResponse is handled by ExecuteToolCall. It should contain the quest offer text.
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting to offer quest to: Target='{targetPlayerIdentifier}'");

            // --- 1. Find Target Player ---
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryOfferQuest.");
                // TryChat(npc, $"Who are you talking about?");
                return false;
            }

            // --- 2. Check if Player Already Has Quest (Placeholder) ---
            // TODO: Implement a real quest tracking system. This is just a placeholder.
            // For now, we'll pretend they never have a quest and always offer one.
            bool playerHasActiveQuest = false; // Replace with actual check
            if (playerHasActiveQuest)
            {
                _sawmill.Info($"Player {ToPrettyString(targetPlayer.Value)} already has an active quest from {ToPrettyString(npc)}. Cannot offer another.");
                // TryChat(npc, $"Finish the job I already gave you first!"); // Feedback
                return false;
            }

            // --- 3. Check if NPC has Quest Items Defined ---
            if (aiComp.QuestItems == null || aiComp.QuestItems.Count == 0)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to offer quest, but has no QuestItems defined.");
                // TryChat(npc, $"I don't have any work for you right now."); // Feedback
                return false;
            }

            // --- 4. Select a Quest Item (Simple Random for now) ---
            // TODO: Implement better quest selection logic (e.g., based on rarity, player level, etc.)
            var random = new Random();
            var questItemInfo = aiComp.QuestItems[random.Next(aiComp.QuestItems.Count)];
            var questItemId = questItemInfo.ProtoId;

            // --- 5. Record the Quest (Placeholder) ---
            // TODO: Record that targetPlayer now has a quest for questItemId from npc.
            _sawmill.Info($"NPC {ToPrettyString(npc)} offered quest for '{questItemId}' to {ToPrettyString(targetPlayer.Value)}. (Quest tracking not implemented)");

            // --- 6. Communicate Offer ---
            // The actual quest offer text (e.g., "Bring me a DogTail") should be in the npcResponse parameter,
            // which was already spoken by ExecuteToolCall. We don't need to chat again here unless
            // the npcResponse was missing (which the tool description requires).
            if (string.IsNullOrWhiteSpace(npcResponse))
            {
                _sawmill.Warning($"TryOfferQuest called without npcResponse for {ToPrettyString(npc)}. Tool description requires it.");
                // Fallback chat if npcResponse was missing
                TryChat(npc, $"Hey {Name(targetPlayer.Value)}, I need someone to fetch me a {questItemId}. Interested?");
            }


            return true; // Success means the offer was made (even if tracking failed)
        }


        // --- Placeholder Helper Methods ---

        // Finds a player entity based *only* on their CKey (username).
        private EntityUid? FindPlayerByIdentifier(string ckeyIdentifier)
        {
            // Iterates all players and checks CKey.
            var query = EntityQueryEnumerator<ActorComponent>(); // Only need ActorComponent for the session/CKey
            while (query.MoveNext(out var uid, out var actor))
            {
                // Check CKey
                if (actor.PlayerSession.Name.Equals(ckeyIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    return uid;
                }
            }
            _sawmill.Warning($"Could not find player with CKey: {ckeyIdentifier}");
            return null;
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
