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

namespace Content.Server._Stalker.AI
{
    public sealed class AINPCSystem : SharedAiNpcSystem
    {
        [Dependency] private readonly AIManager _aiManager = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly EntityManager _entity = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        // [Dependency] private readonly InventorySystem _inventory = default!; // Might need later for searching inventory
        // [Dependency] private readonly SharedContainerSystem _container = default!; // Might need later

        private ISawmill _sawmill = default!;

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
            // Ignore if message is empty or from the AI NPC itself
            if (string.IsNullOrWhiteSpace(args.Message) || HasComp<AiNpcComponent>(args.Source))
                return;

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

                // Add user message to history, managed by the system now
                AddMessageToHistory(npcUid, aiComp, "user", args.Message);

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
                // Add assistant's response to history
                AddMessageToHistory(uid, component, "assistant", response.TextResponse);
            }
            else if (response.ToolCallRequest != null)
            {
                _sawmill.Debug($"NPC {ToPrettyString(uid)} received tool call request: {response.ToolCallRequest.ToolName}");

                // Execute the tool call
                var (success, resultMessage) = ExecuteToolCall(uid, component, response.ToolCallRequest);

                // TODO: Add assistant's attempt and tool's result to history
                // This requires passing the tool call ID back from AIManager and potentially modifying AddMessageToHistory
                // Example (requires AIResponse to contain the original tool call ID):
                // if (response.ToolCallRequest.TryGetOriginalId(out var toolCallId)) // Fictional method
                // {
                //    AddMessageToHistory(uid, component, "assistant", null, toolCalls: new List<OpenRouterToolCall> { /* construct tool call representation */ });
                //    AddMessageToHistory(uid, component, "tool", resultMessage, toolCallId: toolCallId);
                // }

                _sawmill.Info($"Tool '{response.ToolCallRequest.ToolName}' executed for {ToPrettyString(uid)}. Success: {success}. Result: {resultMessage}");

                // TODO: After executing a tool, send the result back to the AI
                // This involves another call to GetActionAsync with a "tool" role message containing the result.
                // For now, we just execute the tool.
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
                            resultMessage = success ? "Chat action performed." : "Chat action failed.";
                        }
                        else resultMessage = "Missing or invalid 'message' argument for TryChat.";
                        break;

                    case nameof(TryGiveItem): // Use nameof
                        if (TryGetStringArgument(toolCall.Arguments, "targetPlayer", out var targetPlayer) &&
                            TryGetStringArgument(toolCall.Arguments, "itemToGive", out var itemToGive))
                        {
                            success = TryGiveItem(uid, targetPlayer, itemToGive);
                            resultMessage = success ? "Give item action performed." : "Give item action failed.";
                        }
                        else resultMessage = "Missing or invalid arguments for TryGiveItem.";
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

        /// <summary>
        /// Adds a message to the NPC's conversation history, managed by this system.
        /// </summary>
        private void AddMessageToHistory(EntityUid npcUid, AiNpcComponent component, string role, string? content, List<OpenRouterToolCall>? toolCalls = null, string? toolCallId = null)
        {
            var history = GetHistoryForNpc(npcUid);

            // Basic history addition
            history.Add(new OpenRouterMessage { Role = role, Content = content, ToolCalls = toolCalls, ToolCallId = toolCallId });

            // Trim history if it exceeds the maximum length defined in the component
            while (history.Count > component.MaxHistory)
            {
                history.RemoveAt(0); // Remove the oldest message
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
            descriptions.Add(GetChatToolDescription());
            // TODO: Add logic to conditionally add tools based on NPC state (e.g., has items to give?)
            descriptions.Add(GetGiveItemToolDescription());
            descriptions.Add(GetTakeItemToolDescription());
            return descriptions;
        }

        private string GetChatToolDescription()
        {
            // OpenAI/OpenRouter tool format requires parameters as a JSON schema object
            return @"{
                ""name"": ""TryChat"",
                ""description"": ""Respond verbally to a user or initiate conversation."",
                ""parameters"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""message"": {
                            ""type"": ""string"",
                            ""description"": ""The message the NPC should say.""
                        }
                    },
                    ""required"": [""message""]
                }
            }";
        }


        private string GetGiveItemToolDescription()
        {
             return @"{
                ""name"": ""TryGiveItem"",
                ""description"": ""Give an item from the NPC's inventory to a target player."",
                ""parameters"": {
                    ""type"": ""object"",
                    ""properties"": {
                         ""targetPlayer"": {
                            ""type"": ""string"",
                            ""description"": ""The identifier (e.g., name or entity UID string) of the player to give the item to.""
                        },
                        ""itemToGive"": {
                            ""type"": ""string"",
                            ""description"": ""The identifier (e.g., name or entity UID string) of the item in the NPC's inventory to give.""
                        }
                    },
                    ""required"": [""targetPlayer"", ""itemToGive""]
                }
            }";
        }


        private string GetTakeItemToolDescription()
        {
             return @"{
                ""name"": ""TryTakeItem"",
                ""description"": ""Request an item from a target player and take it if offered."",
                ""parameters"": {
                    ""type"": ""object"",
                    ""properties"": {
                         ""targetPlayer"": {
                            ""type"": ""string"",
                            ""description"": ""The identifier (e.g., name or entity UID string) of the player to request the item from.""
                        },
                        ""requestedItemName"": {
                            ""type"": ""string"",
                            ""description"": ""The name of the item the NPC wants to receive.""
                        }
                    },
                    ""required"": [""targetPlayer"", ""requestedItemName""]
                }
            }";
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


        public bool TryGiveItem(EntityUid npc, string targetPlayerIdentifier, string itemIdentifier)
        {
             _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting give item: Item='{itemIdentifier}', Target='{targetPlayerIdentifier}'");

            // --- 1. Find Target Player ---
            // TODO: Implement robust player lookup (e.g., by name, CKey, or UID string)
            // Placeholder: Assume identifier is the entity name for now.
            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryGiveItem.");
                return false;
            }

            // --- 2. Find Item in NPC's Inventory/Hands ---
            // TODO: Implement robust item lookup in NPC inventory (hands, pockets, backpack?)
            // Placeholder: Check only hands for now.
            EntityUid? itemToGive = FindItemInHands(npc, itemIdentifier);
            if (itemToGive == null || !itemToGive.Value.Valid)
            {
                 _sawmill.Warning($"NPC {ToPrettyString(npc)} could not find item '{itemIdentifier}' in hands for TryGiveItem.");
                 return false;
            }

            // --- 3. Check Range & Interaction ---
            // TODO: Add range checks and CanInteract checks between NPC and Player
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > 2.0f) // Example range
            {
                 _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far for NPC {ToPrettyString(npc)} to give item.");
                 return false;
            }

            // --- 4. Perform Transfer ---
            // Use PickupOrDrop similar to BandsSystem example. This handles putting it in hands or dropping it nearby.
            if (_hands.TryDrop(npc, itemToGive.Value)) // Drop it first
            {
                 if (_hands.TryPickupAnyHand(targetPlayer.Value, itemToGive.Value)) // Then try to make the target pick it up
                 {
                     _sawmill.Info($"NPC {ToPrettyString(npc)} successfully gave item {ToPrettyString(itemToGive.Value)} to {ToPrettyString(targetPlayer.Value)}.");
                     return true;
                 }
                 else
                 {
                     _sawmill.Warning($"NPC {ToPrettyString(npc)} dropped item {ToPrettyString(itemToGive.Value)}, but target {ToPrettyString(targetPlayer.Value)} failed to pick it up.");
                     // Item is dropped on the ground near the player. Still counts as a partial success?
                     return false; // Or maybe true depending on desired behaviour
                 }
            }
            else
            {
                 _sawmill.Warning($"NPC {ToPrettyString(npc)} failed to drop item {ToPrettyString(itemToGive.Value)}.");
                 return false;
            }
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
            // This simulates the player dropping and the NPC picking up, bypassing actual interaction.
            // A real implementation needs a request/response mechanism.
            if (_hands.TryDrop(targetPlayer.Value, itemToTake.Value)) // Player drops
            {
                if (_hands.TryPickupAnyHand(npc, itemToTake.Value)) // NPC picks up
                {
                    _sawmill.Info($"NPC {ToPrettyString(npc)} successfully took item {ToPrettyString(itemToTake.Value)} from {ToPrettyString(targetPlayer.Value)} (simulated).");
                    return true;
                }
                else
                {
                    _sawmill.Warning($"Player {ToPrettyString(targetPlayer.Value)} dropped item {ToPrettyString(itemToTake.Value)}, but NPC {ToPrettyString(npc)} failed to pick it up.");
                    // Item is on the ground. Maybe try to make player pick it back up?
                    _hands.TryPickupAnyHand(targetPlayer.Value, itemToTake.Value); // Attempt to give it back
                    return false;
                }
            }
            else
            {
                 _sawmill.Warning($"Player {ToPrettyString(targetPlayer.Value)} failed to drop item {ToPrettyString(itemToTake.Value)} for NPC {ToPrettyString(npc)} to take.");
                 return false;
            }
        }

        // --- Placeholder Helper Methods ---

        // TODO: Replace with robust entity lookup system
        private EntityUid? FindPlayerByIdentifier(string identifier)
        {
            // Extremely basic placeholder - iterates all players and checks name. VERY INEFFICIENT.
            var query = EntityQueryEnumerator<MetaDataComponent, ActorComponent>(); // Assuming players have ActorComponent
            while (query.MoveNext(out var uid, out var meta, out _))
            {
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
                if (hand.HeldEntity != null && Name(hand.HeldEntity.Value).Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    return hand.HeldEntity;
                }
            }
            return null;
        }
    }
}
