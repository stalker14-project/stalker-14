// Content.Server/_Stalker/AI/AINPCSystem.cs
using Content.Server.Chat.Systems;
using Content.Shared.Hands.EntitySystems; // Added for SharedHandsSystem
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Collections.Generic;
using System.Linq; // For Linq operations on history
using System.Text.Json; // For JsonDocument, JsonElement
using System.Text.Json.Nodes; // For JsonObject
using Robust.Shared.Log;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using System.Threading.Tasks;
using System.Threading; // For CancellationTokenSource
using static Content.Server._Stalker.AI.AIManager;
using Content.Shared.Hands.Components;
using Robust.Shared.Player; // For AIResponse, OpenRouterMessage, AIToolCall
using Content.Server._Stalker.AI;
using Content.Shared._Stalker.AI;
using Robust.Shared.Prototypes;
using Content.Shared.Damage; // Added for DamageableSystem
using Content.Shared.NPC.Systems; // Added for NpcFactionSystem
using Content.Shared.Whitelist; // Added for EntityWhitelistSystem
using Robust.Server.Audio; // Added for AudioSystem
using Content.Shared.NPC.Prototypes; // Added for NpcFactionPrototype ProtoId

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
        [Dependency] private readonly DamageableSystem _damageable = default!; // Added
        [Dependency] private readonly NpcFactionSystem _npcFaction = default!; // Added
        [Dependency] private readonly AudioSystem _audio = default!; // Added
        [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!; // Added
        // [Dependency] private readonly InventorySystem _inventory = default!; // Might need later for searching inventory
        // [Dependency] private readonly SharedContainerSystem _container = default!; // Might need later

        private ISawmill _sawmill = default!;

        // Define the player faction ID (adjust if needed)
        private static readonly ProtoId<NpcFactionPrototype> PlayerFaction = "Stalker";

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("ai.npc.system");

            // Subscribe to the event raised *after* speech is processed and finalized by ChatSystem
            SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);

            // Subscribe to our internal event for processing AI responses on the main thread (Broadcast subscription)
            SubscribeLocalEvent<ProcessAIResponseEvent>(HandleAIResponse);

            _sawmill.Info("AI NPC System Initialized");
        }

        // Dictionary to keep track of ongoing AI requests per NPC to prevent spamming
        private readonly Dictionary<EntityUid, CancellationTokenSource> _ongoingRequests = new();
        // Dictionary to store conversation history per NPC
        private readonly Dictionary<EntityUid, List<OpenRouterMessage>> _conversationHistories = new();

        private void OnEntitySpoke(EntitySpokeEvent args)
        {
            // Ignore if message is empty, from an AI NPC, or not from a player character
            if (string.IsNullOrWhiteSpace(args.Message) ||
                HasComp<AiNpcComponent>(args.Source) ||
                !HasComp<ActorComponent>(args.Source)) // Added check for ActorComponent
                return;

            var speakerName = Name(args.Source); // Get the speaker's character name
            // Get speaker's NetUserId (CKey)
            string? speakerCKey = null; // Changed variable name
            if (TryComp<ActorComponent>(args.Source, out var actor))
            {
                speakerCKey = actor.PlayerSession.Name; // Use PlayerSession.Name for CKey
            }

            // Find nearby AI NPCs that heard this message
            var query = EntityQueryEnumerator<AiNpcComponent, TransformComponent>();
            while (query.MoveNext(out var npcUid, out var aiComp, out var npcTransform))
            {
                // Ignore if the speaker *is* this NPC (should be caught above, but double-check)
                if (npcUid == args.Source)
                    continue;

                // Check if the speaker (args.Source) is within interaction range of this NPC (npcUid)
                if (!EntityManager.TryGetComponent<TransformComponent>(args.Source, out var sourceTransform))
                    continue;

                // TODO: Define interaction range, maybe make it configurable on AiNpcComponent?
                const float interactionRange = 7.0f; // Increased range slightly
                if (!npcTransform.Coordinates.TryDistance(EntityManager, sourceTransform.Coordinates, out var distance) || distance > interactionRange)
                    continue;

                // Prevent spamming requests for the same NPC
                if (_ongoingRequests.ContainsKey(npcUid))
                {
                    _sawmill.Debug($"AI request already in progress for NPC {ToPrettyString(npcUid)}. Ignoring speech from {ToPrettyString(args.Source)}.");
                    continue;
                }

                _sawmill.Debug($"NPC {ToPrettyString(npcUid)} heard speech from {ToPrettyString(args.Source)}: \"{args.Message}\"");

                // Trim history *before* adding user message
                TrimHistory(npcUid, aiComp, 1); // Make space for 1 user message
                // Add user message to history, including the speaker's name and CKey
                AddMessageToHistory(npcUid, aiComp, "user", args.Message, speakerName: speakerName, speakerCKey: speakerCKey); // AddMessageToHistory no longer trims

                // Prepare data for AI Manager
                var tools = GetAvailableToolDescriptions(npcUid, aiComp);
                // Get history from our internal dictionary
                var history = GetHistoryForNpc(npcUid);
                var prompt = aiComp.BasePrompt;
                var userMessage = args.Message; // The message that was just spoken

                // Create a cancellation token for this request
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Added 30s timeout
                _ongoingRequests[npcUid] = cts;

                // Call AIManager asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        var response = await _aiManager.GetActionAsync(npcUid, prompt, history, userMessage, tools, cts.Token);

                        // Queue the response processing back to the main game thread using the system's method
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, response)); // Pass UID to event if needed
                    }
                    catch (OperationCanceledException)
                    {
                         _sawmill.Debug($"AI request for NPC {ToPrettyString(npcUid)} timed out or was cancelled.");
                         // Queue a failure response to ensure state is cleaned up
                         QueueLocalEvent(new ProcessAIResponseEvent(npcUid, AIResponse.Failure("Request timed out or cancelled.")));
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Unhandled exception during async AI request for {ToPrettyString(npcUid)}: {e}");
                        QueueLocalEvent(new ProcessAIResponseEvent(npcUid, AIResponse.Failure($"Internal error: {e.Message}")));
                    }
                    // No finally block needed here for removal, HandleAIResponse will do it.
                    // Dispose CTS on the main thread in HandleAIResponse.
                }, cts.Token);
            }
        }

        // Event to process AI response on the main thread
        private sealed class ProcessAIResponseEvent : EntityEventArgs
        {
            public EntityUid Target { get; } // Store the target UID
            public AIResponse Response { get; }
            public ProcessAIResponseEvent(EntityUid target, AIResponse response)
            {
                Target = target;
                Response = response;
            }
        }

        // Handler for the response processing event (runs on main game thread)
        // Handler for the response processing event (runs on main game thread)
        // Note: The event subscription is now just ProcessAIResponseEvent, not tied to AiNpcComponent directly
        private void HandleAIResponse(ProcessAIResponseEvent args)
        {
            var uid = args.Target;
            // Ensure the entity still exists and has the component before processing
            if (!TryComp<AiNpcComponent>(uid, out var component))
                return;

            // Clean up the cancellation token source and remove the ongoing request marker
            if (_ongoingRequests.Remove(uid, out var cts))
            {
                cts.Dispose();
            }

            var response = args.Response;

            if (!response.Success)
            {
                _sawmill.Warning($"AI request failed for NPC {ToPrettyString(uid)}: {response.ErrorMessage}");
                // Optionally, make the NPC say something generic about being confused?
                // TryChat(uid, "...");
                return;
            }

            if (response.TextResponse != null)
            {
                _sawmill.Debug($"NPC {ToPrettyString(uid)} received text response: {response.TextResponse}");
                TryChat(uid, response.TextResponse);
                // Trim history *before* adding assistant message
                TrimHistory(uid, component, 1); // Make space for 1 assistant message
                // Add assistant's response to history (no name or UserId needed for assistant role)
                AddMessageToHistory(uid, component, "assistant", response.TextResponse); // AddMessageToHistory no longer trims
            }
            else if (response.ToolCallRequests != null && response.ToolCallRequests.Count > 0) // Check for multiple tool calls
            {
                 _sawmill.Debug($"NPC {ToPrettyString(uid)} received {response.ToolCallRequests.Count} tool call requests.");

                // 1. Prepare new messages (without adding yet)
                var assistantToolCalls = response.ToolCallRequests.Select(tc => new OpenRouterToolCall
                {
                    Id = tc.ToolCallId, // Use the ID passed from AIManager
                    Type = "function",
                    Function = new OpenRouterToolFunction { Name = tc.ToolName, Arguments = tc.Arguments.ToJsonString() } // Convert args back to string for history
                }).ToList();
                AddMessageToHistory(uid, component, "assistant", null, toolCalls: assistantToolCalls);

                // 2. Execute each tool call sequentially and add its result to history
                foreach (var toolCall in response.ToolCallRequests)
                {
                    _sawmill.Debug($"Executing tool call: {toolCall.ToolName} (ID: {toolCall.ToolCallId})");
                    var (success, resultMessage) = ExecuteToolCall(uid, component, toolCall); // Execute the tool

                    // Add the result of this specific tool call to the history
                    AddMessageToHistory(uid, component, "tool", resultMessage, toolCallId: toolCall.ToolCallId);

                    _sawmill.Info($"Tool '{toolCall.ToolName}' (ID: {toolCall.ToolCallId}) executed for {ToPrettyString(uid)}. Success: {success}. Result: {resultMessage}");

                    // TODO: Consider if we need to immediately re-query the AI after *each* tool result,
                    // or only after all requested tools in a batch are executed.
                    // Current implementation executes all requested tools before potentially re-querying (if implemented).
                }
            }
            else
            {
                 _sawmill.Warning($"AI response for NPC {ToPrettyString(uid)} was successful but contained neither text nor tool call.");
            }
        }

        /// <summary>
        /// Executes the requested tool call and returns success status and a result message.
        /// </summary>
        private (bool Success, string ResultMessage) ExecuteToolCall(EntityUid uid, AiNpcComponent component, AIToolCall toolCall)
        {
            bool success = false;
            string resultMessage = "Unknown tool name."; // Default failure message

            try
            {
                switch (toolCall.ToolName)
                {
                    case nameof(TryChat): // Use nameof for safety
                        if (TryGetStringArgument(toolCall.Arguments, "message", out var message))
                        {
                            success = TryChat(uid, message);
                            if (success)
                            {
                                // History is now added in HandleAIResponse for tool results.
                                resultMessage = "Chat action performed.";
                            }
                            else
                            {
                                resultMessage = "Chat action failed.";
                            }
                        }
                        else resultMessage = "Missing or invalid 'message' argument for TryChat.";
                        break;

                    case nameof(TryGiveItem): // Use nameof
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out var targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "itemPrototypeId", out var itemPrototypeId))
                        {
                            // Ensure quantity is positive
                            TryGetIntArgument(toolCall.Arguments, "quantity", out var quantity);
                            quantity = Math.Max(1, quantity);
                            success = TryGiveItem(uid, targetPlayer, itemPrototypeId, quantity); // Pass new args
                            resultMessage = success ? "Give item action performed." : "Give item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryGiveItem (expected targetPlayer, itemPrototypeId, quantity).";
                        break;

                    case nameof(TryTakeItem): // Use nameof
                         if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "requestedItemName", out var requestedItemName))
                        {
                            success = TryTakeItem(uid, targetPlayer, requestedItemName);
                            resultMessage = success ? "Take item action performed." : "Take item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryTakeItem.";
                        break;

                    case nameof(TryPunishPlayer): // Added case for the new tool
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "reason", out var reason)) // Added reason argument
                        {
                            success = TryPunishPlayer(uid, component, targetPlayer, reason); // Pass component and reason
                            resultMessage = success ? "Punish player action performed." : "Punish player action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryPunishPlayer (expected targetPlayer, reason).";
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
        /// Gets the conversation history list for a specific NPC, creating it if it doesn't exist.
        /// </summary>
        private List<OpenRouterMessage> GetHistoryForNpc(EntityUid npcUid)
        {
            if (!_conversationHistories.TryGetValue(npcUid, out var history))
            {
                history = new List<OpenRouterMessage>();
                _conversationHistories[npcUid] = history;
            }
            return history;
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
        /// Adds a message to the NPC's conversation history, managed by this system.
        /// </summary>
        private void AddMessageToHistory(EntityUid npcUid, AiNpcComponent component, string role, string? content, string? speakerName = null, string? speakerCKey = null, List<OpenRouterToolCall>? toolCalls = null, string? toolCallId = null) // Renamed parameter
        {
            var history = GetHistoryForNpc(npcUid);

            // Basic history addition, include speaker name and CKey if provided (typically for 'user' role)
            history.Add(new OpenRouterMessage { Role = role, Content = content, Name = speakerName, CKey = speakerCKey, ToolCalls = toolCalls, ToolCallId = toolCallId }); // Use CKey field

            // REMOVED trimming logic from here. Trimming is now done proactively before adding.
        }

        /// <summary>
        /// Trims the history list for an NPC if it exceeds the max limit,
        /// making space for a specified number of new messages.
        /// Removes messages from the beginning (oldest).
        /// </summary>
        private void TrimHistory(EntityUid npcUid, AiNpcComponent component, int spaceNeeded)
        {
            var history = GetHistoryForNpc(npcUid);
            int maxAllowed = component.MaxHistory;
            // Calculate how many messages *currently* exceed the limit if we add the new ones
            int removeCount = (history.Count + spaceNeeded) - maxAllowed;

            if (removeCount > 0)
            {
                // Ensure we don't try to remove more messages than exist
                int actualRemoveCount = Math.Min(removeCount, history.Count);
                if (actualRemoveCount > 0)
                {
                    history.RemoveRange(0, actualRemoveCount);
                    _sawmill.Debug($"Trimmed {actualRemoveCount} messages from history for {ToPrettyString(npcUid)} to make space for {spaceNeeded}. New count: {history.Count}");
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
            descriptions.Add(GetChatToolDescription(component));
            // Only add GiveItem tool if the NPC actually has givable items defined
            if (component.GivableItems.Count > 0) // Use GivableItems
            {
                descriptions.Add(GetGiveItemToolDescription(component)); // Pass component data
            }
            descriptions.Add(GetTakeItemToolDescription());
            // Add the new punish tool description if the component allows it (e.g., has damage defined)
            if (component.PunishmentDamage != null || component.PunishmentSound != null)
            {
                descriptions.Add(GetPunishPlayerToolDescription(component));
            }
            return descriptions;
        }

        private string GetChatToolDescription(AiNpcComponent component)
        {
            // Build the list of allowed items for the description from GivableItems
            var allowedItemsList = component.QuestItems // Use GivableItems
                .Select(item => $"- {item.ProtoId} (Max Quantity: {item.MaxQuantity}, Rarity: {item.Rarity})")
                .ToList();
            var allowedItemsString = allowedItemsList.Count > 0
                ? string.Join("\n", allowedItemsList)
                : "None";
            // OpenAI/OpenRouter tool format requires parameters as a JSON schema object

            var description = $@"Respond verbally to a user or initiate conversation.
                Allowed Quest Items: {allowedItemsString}";

            return $@"{{
                ""name"": ""TryChat"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                        ""message"": {{
                            ""type"": ""string"",
                            ""description"": ""The message the NPC should say.""
                        }}
                    }},
                    ""required"": [""message""]
                }}
            }}";
        }


        private string GetGiveItemToolDescription(AiNpcComponent component)
        {
            // Build the list of allowed items for the description from GivableItems
            var allowedItemsList = component.GivableItems // Use GivableItems
                .Select(item => $"- {item.ProtoId} (Max Quantity: {item.MaxQuantity}, Rarity: {item.Rarity})")
                .ToList();
            var allowedItemsString = allowedItemsList.Count > 0
                ? string.Join("\n", allowedItemsList)
                : "None";

            // Dynamically generate the description including allowed items
            // Use System.Text.Json for safe encoding within the JSON string
            var description = $@"Spawn and give a specified quantity of an item (by prototype ID) to a target player.
                Allowed Reward Items: {allowedItemsString}";

             return $@"{{
                ""name"": ""TryGiveItem"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The unique CKey of the player who should receive the item(s). Use the speaker's CKey if they ask for themselves.""
                        }},
                        ""itemPrototypeId"": {{
                            ""type"": ""string"",
                            ""description"": ""The exact, case-sensitive prototype ID string of the item to spawn and give from the allowed list.""
                        }},
                        ""quantity"": {{
                            ""type"": ""integer"",
                            ""description"": ""The number of items to give (must not exceed the Max Quantity for the item)."",
                            ""default"": 1
                        }}
                    }},
                    ""required"": [""targetPlayer"", ""itemPrototypeId""]
                }}
            }}";
        }


        private string GetTakeItemToolDescription()
        {
             return @"{
                ""name"": ""TryTakeItem"",
                ""description"": ""Request an item from a target player and take it if offered. IMPORTANT: When using this tool, ALSO use the 'TryChat' tool in the same response to inform the player what you are taking from them "",
                ""parameters"": {
                    ""type"": ""object"",
                    ""properties"": {
                         ""targetPlayer"": {
                            ""type"": ""string"",
                            ""description"": ""The unique CKey (username) to request the item from.""
                        },
                        ""requestedItemName"": {
                            ""type"": ""string"",
                            ""description"": ""The exact name of the item the NPC wants to receive (e.g., 'CombatKnife', 'Medkit').""
                        }
                    },
                    ""required"": [""targetPlayer"", ""requestedItemName""]
                }
            }";
        }

        private string GetPunishPlayerToolDescription(AiNpcComponent component)
        {
            // Use System.Text.Json for safe encoding within the JSON string
            // Simplified description
            var description = $@"Punish a player perceived as rude, lying, or hostile by attacking them. Requires a reason.";

            return $@"{{
                ""name"": ""TryPunishPlayer"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""targetPlayer"": {{
                            ""type"": ""string"",
                            ""description"": ""The unique CKey (username) of the player to punish.""
                        }},
                        ""reason"": {{
                            ""type"": ""string"",
                            ""description"": ""A brief explanation why the player is being punished""
                        }}
                    }},
                    ""required"": [""targetPlayer"", ""reason""]
                }}
            }}";
        }


        // Actual tool methods, invoked locally after AI response parsing
        public bool TryChat(EntityUid npc, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            _sawmill.Debug($"NPC {ToPrettyString(npc)} executing chat: {message}");
            // This is called from HandleAIResponse, which is already on the main thread via QueueEntityEvent
            _chatSystem.TrySendInGameICMessage(npc, message, InGameICChatType.Speak, hideChat: false); // Ensure hideChat is false
            return true;
        }


        // Updated TryGiveItem to spawn based on prototype ID and quantity
        public bool TryGiveItem(EntityUid npc, string targetPlayerIdentifier, string itemPrototypeId, int quantity)
        {
             _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting give item: Proto='{itemPrototypeId}', Qty={quantity}, Target='{targetPlayerIdentifier}'");

            // --- 0. Get NPC Component ---
            if (!TryComp<AiNpcComponent>(npc, out var aiComp))
            {
                _sawmill.Error($"NPC {ToPrettyString(npc)} is missing AiNpcComponent in TryGiveItem.");
                return false; // Should not happen if called correctly
            }

            // --- 1. Find Target Player ---
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryGiveItem.");
                return false;
            }

            // --- 2. Validate Item Against Givable List & Quantity ---
            var givableItemInfo = aiComp.GivableItems.FirstOrDefault(item => item.ProtoId.Id.Equals(itemPrototypeId, StringComparison.OrdinalIgnoreCase)); // Use GivableItems

            if (givableItemInfo == null) // Check givableItemInfo
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give non-givable item '{itemPrototypeId}'. Denying.");
                // Optionally provide feedback to AI? "I cannot give that item."
                return false;
            }

            if (quantity <= 0)
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give zero or negative quantity ({quantity}) of '{itemPrototypeId}'. Setting to 1.");
                quantity = 1; // Default to 1 if invalid quantity requested
            }

            if (quantity > givableItemInfo.MaxQuantity) // Use givableItemInfo
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to give {quantity} of '{itemPrototypeId}', but max allowed is {givableItemInfo.MaxQuantity}. Clamping quantity.");
                quantity = givableItemInfo.MaxQuantity; // Clamp to max allowed using givableItemInfo
            }

            // --- 3. Validate Prototype ID (Redundant check, but safe) ---
            if (!_proto.HasIndex<EntityPrototype>(itemPrototypeId))
            {
                 _sawmill.Warning($"Invalid prototype ID '{itemPrototypeId}' requested by NPC {ToPrettyString(npc)} for TryGiveItem (passed managed check somehow?).");
                 return false;
            }

            // --- 4. Check Range & Interaction ---
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > 2.0f) // Example range
            {
                 _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far for NPC {ToPrettyString(npc)} to give item.");
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
            for (int i = 0; i < quantity; i++) // Use the potentially clamped quantity
            {
                var spawnedItem = Spawn(itemPrototypeId, npcCoords);
                if (!_hands.TryPickupAnyHand(targetPlayer.Value, spawnedItem, checkActionBlocker: false)) // Attempt to put directly into player's hand
                {
                    // If pickup fails, try dropping it near the player as a fallback
                    _sawmill.Warning($"Failed direct pickup for item {i+1}/{quantity} ({ToPrettyString(spawnedItem)}) by {ToPrettyString(targetPlayer.Value)}. Dropping near NPC.");
                    Transform(spawnedItem).Coordinates = npcCoords;
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

            return givenCount > 0; // Return true if at least one item was successfully given
        }


        public bool TryTakeItem(EntityUid npc, string targetPlayerIdentifier, string requestedItemName)
        {
             _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting take item: ItemName='{requestedItemName}', Target='{targetPlayerIdentifier}'");

            // --- 1. Find Target Player ---
            // TODO: Implement robust player lookup
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryTakeItem.");
                return false;
            }

            // --- 2. Find Item in Player's Inventory/Hands ---
            // TODO: Implement robust item lookup in Player inventory (hands primarily?)
            // Placeholder: Check only hands for now.
            EntityUid? itemToTake = FindItemInHands(targetPlayer.Value, requestedItemName);
             if (itemToTake == null || !itemToTake.Value.Valid)
            {
                 _sawmill.Warning($"Player {ToPrettyString(targetPlayer.Value)} could not find item '{requestedItemName}' in hands for TryTakeItem.");
                 return false;
            }

            // --- 3. Check Range & Interaction ---
            // TODO: Add range checks and CanInteract checks
             if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > 2.0f) // Example range
            {
                 _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far for NPC {ToPrettyString(npc)} to take item.");
                 return false;
            }

            // --- 4. Perform Transfer (Simplified - Assumes Consent) ---
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
                 return false;
            }
        }

        /// <summary>
        /// Attempts to punish a player by applying damage.
        /// </summary>
        public bool TryPunishPlayer(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string reason)
        {
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
                return false;
            }

            // --- 3. Check Range ---
            // Use a slightly longer range for punishment than giving items?
            const float punishRange = 5.0f;
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > punishRange)
            {
                _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far ({distance}m) for NPC {ToPrettyString(npc)} to punish.");
                // Optionally, make the NPC say something like "Get back here!"?
                return false;
            }

            // --- 4. Apply Punishment ---
            bool damageApplied = false;
            if (aiComp.PunishmentDamage != null)
            {
                var damageResult = _damageable.TryChangeDamage(targetPlayer.Value, aiComp.PunishmentDamage, ignoreResistances: true); // Apply damage, ignore resistance for punishment?
                damageApplied = damageResult != null;
                _sawmill.Info($"NPC {ToPrettyString(npc)} applied damage to {ToPrettyString(targetPlayer.Value)}. Success. Reason: {reason}");
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

            // Optionally, make the NPC say something aggressive based on the reason?
            // TryChat(npc, $"You dare insult me?! ({reason})"); // Example

            return damageApplied; // Return true if damage was successfully applied
        }


        // --- Placeholder Helper Methods ---

        // TODO: Replace with robust entity lookup system (by CKey first, then maybe name)
        private EntityUid? FindPlayerByIdentifier(string identifier)
        {
            // Extremely basic placeholder - iterates all players and checks CKey then name. VERY INEFFICIENT.
            var query = EntityQueryEnumerator<MetaDataComponent, ActorComponent>(); // Assuming players have ActorComponent
            while (query.MoveNext(out var uid, out var meta, out var actor))
            {
                // Check CKey first
                if (actor.PlayerSession.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return uid;
                }
                // Fallback to checking character name
                if (meta.EntityName.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return uid;
                }
                // Could also try parsing identifier as EntityUid or NetEntity here
            }
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
                if (hand.HeldEntity is {} heldEntityValue)
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
