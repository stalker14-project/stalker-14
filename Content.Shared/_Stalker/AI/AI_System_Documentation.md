# Stalker AI NPC System Documentation

This document outlines the design and functionality of the AI-powered NPC interaction system implemented within the Stalker module for Space Station 14.

## 1. Goal

The primary goal of this system is to enable Non-Player Characters (NPCs) to engage in more dynamic and context-aware interactions with players using Large Language Models (LLMs) via the OpenRouter API. This includes basic conversation, responding to player speech, and performing simple actions like giving or taking items based on AI decisions.

## 2. Core Components

The system is primarily composed of three parts: a server-side manager for API communication, a server-side entity system for NPC logic, and a shared component to mark AI-enabled NPCs.

### 2.1. `AIManager` (Server - `Content.Server/_Stalker/AI/AIManager.cs`)

-   **Responsibilities:**
    -   Acts as the sole interface with the external OpenRouter API (or any OpenAI-compatible endpoint).
    -   Manages API configuration (URL, Model Name, API Key) read from `CCCCVars` (`openrouter.url`, `openrouter.model`, `openrouter.apikey`).
    -   Constructs the request payload (including system prompt, conversation history, current user message, and available tools) in the format expected by the OpenAI Chat Completions API.
    -   Sends requests to the LLM using `HttpClient`.
    -   Receives and parses the LLM's response.
    -   Determines if the response is a simple text message or a request to execute a specific "tool" (function call).
    -   Packages the result (text or tool call details) into an `AIResponse` record.
    -   Handles API errors and communication failures gracefully.
-   **Key Interactions:** Called by `AINPCSystem` to get an AI response; calls the OpenRouter API.

### 2.2. `AINPCSystem` (Server - `Content.Server/_Stalker/AI/AINPCSystem.cs`)

-   **Responsibilities:**
    -   Manages entities possessing the `AiNpcComponent`.
    -   Subscribes to `EntitySpokeEvent` to detect nearby speech from player characters (`ActorComponent`).
    -   Manages the conversation history for each AI NPC, storing separate histories for each interacting player (identified by CKey) internally (using a `Dictionary<EntityUid, Dictionary<string, List<OpenRouterMessage>>>`). History includes speaker name and CKey for user messages, assistant responses, tool calls, and tool results.
    -   Defines the available "tools" (C# methods like `TryChat`, `TryOfferQuest`, `TryGiveItem`, `TryTakeItem`, `TryPunishPlayer`) that the AI can request to use.
    -   Provides JSON descriptions of these tools to `AIManager` for inclusion in the API request. Descriptions for tools involving items (`TryGiveItem`, `TryOfferQuest`) dynamically include the relevant item lists (`GivableItems`, `QuestItems`) from the NPC's component data.
    -   Calls `AIManager.GetActionAsync` asynchronously when an NPC needs to respond to speech.
    -   Processes the `AIResponse` returned by `AIManager` on the main game thread:
        -   If it's a text response, uses `ChatSystem` to make the NPC speak.
        -   If it's a tool call request, parses the arguments and invokes the corresponding local tool method (e.g., `TryGiveItem`).
    -   Handles the game logic for executing the tool actions (spawning items, initiating transfers via `SharedHandsSystem`, etc.). `TryGiveItem` now validates the requested item and quantity against the NPC's `GivableItems` list before proceeding.
    -   Manages cleanup of all associated player conversation histories when an NPC entity is removed.
-   **Key Interactions:** Listens for `EntitySpokeEvent`; calls `AIManager` (passing player-specific history); calls `ChatSystem` and `SharedHandsSystem` to perform actions.

### 2.3. `AiNpcComponent` (Shared - `Content.Shared/_Stalker/AI/AiNpcComponent.cs`)

-   **Responsibilities:**
    -   A marker component (`[RegisterComponent]`) identifying an entity as being controlled by the `AINPCSystem`.
    -   Networked (`[NetworkedComponent]`) so clients are aware of the component's existence (though its state is minimal).
    -   Stores basic, potentially configurable parameters:
        -   `BasePrompt`: The initial system prompt/personality instruction sent to the LLM.
        -   `MaxHistoryPerPlayer`: The maximum number of messages (user, assistant, tool calls/results) to retain in the server-side conversation history *for each individual player* interacting with this NPC.
        -   `GivableItems`: A list (`List<ManagedItemInfo>`) defining items the NPC can potentially give out (e.g., as rewards, trade), along with their `ProtoId`, `MaxQuantity` (per interaction), and `ItemRarity`. The `TryGiveItem` tool is restricted to this list.
        -   `QuestItems`: A list (`List<ManagedItemInfo>`) defining items relevant to quests (e.g., items the player needs to find), along with their `ProtoId`, `MaxQuantity`, and `ItemRarity`. The `TryOfferQuest` tool uses this list.
-   **Key Interactions:** Attached to NPC entities; read by `AINPCSystem`. **Crucially, it does NOT store the conversation history itself**, as that is server-only state managed by `AINPCSystem`.

## 3. Interaction Flow (Example: Player Speaks to NPC)

1.  A player entity (with `ActorComponent`) speaks near an NPC (with `AiNpcComponent`).
2.  `ChatSystem` processes the speech and raises an `EntitySpokeEvent`.
3.  `AINPCSystem.OnEntitySpoke` receives the event.
4.  It identifies the speaker (player) and nearby AI NPCs within range.
5.  It filters out the NPC reacting to itself or non-player entities.
6.  For each relevant NPC, it retrieves/updates the internal conversation history *specific to that player* (using the player's CKey), adding the player's message (with name and CKey). History is trimmed if it exceeds `MaxHistoryPerPlayer`.
7.  It gathers the NPC's `BasePrompt`, the player's specific `history`, and the JSON descriptions of available tools.
8.  It calls `AIManager.GetActionAsync` in a background task, passing this data (history only, no separate user message needed).
9.  `AIManager` constructs the JSON payload for the OpenRouter API (using OpenAI format), including only the messages from the specific player's history.
10. `AIManager` sends the request via `HttpClient` and awaits the response.
11. `AIManager` parses the response, determining if it's text or tool calls, and creates an `AIResponse` record.
12. The background task queues a `ProcessAIResponseEvent` containing the `AIResponse` and the original player's `CKey` back to the main game thread, targeting the specific NPC.
13. `AINPCSystem.HandleAIResponse` receives the event on the main thread.
14. If the `AIResponse` contains text, `AINPCSystem` calls `TryChat` to make the NPC speak, trims the specific player's history, and updates that player's history with the assistant's message.
15. If the `AIResponse` contains one or more tool call requests, `AINPCSystem` adds the assistant's tool call decision to the player's history. It then calls `ExecuteToolCall` for each request. `ExecuteToolCall` handles the optional `npcResponse` for immediate speech, executes the tool's logic, trims the specific player's history, and adds the tool's result message to that player's history.

## 4. Configuration

-   **API:** Configured via CVars in `Content.Shared._Stalker.CCCCVars`:
    -   `openrouter.apikey`: Your OpenRouter API key (Confidential).
    -   `openrouter.model`: The specific model ID to use (e.g., `mistralai/mistral-small-3.1-24b-instruct:free`).
    -   `openrouter.url`: The base URL for the API endpoint (e.g., `https://openrouter.ai/api/v1`).
-   **NPC:** Configured via `AiNpcComponent` fields in entity prototypes:
    -   `prompt`: Sets the base personality/instructions.
    -   `maxHistoryPerPlayer`: Controls conversation memory length per player.
    -   `givableItems`: Defines the list of items the NPC is allowed to give. Example structure:
        ```yaml
        - type: AiNpc
          prompt: "..."
          givableItems:
            - protoId: Medkit
              maxQuantity: 2
              rarity: Common
            - protoId: CombatKnife
              maxQuantity: 1
              rarity: Uncommon
        ```
    -   `questItems`: Defines items relevant for quests. Example structure:
        ```yaml
          questItems:
            - protoId: DogTail
              maxQuantity: 5
              rarity: Common
            - protoId: Artifact
              maxQuantity: 1
              rarity: Rare
        ```

## 5. Tools

-   Tools represent actions the AI can request the NPC to perform.
-   They are defined as public methods within `AINPCSystem` (e.g., `TryChat`, `TryOfferQuest`, `TryGiveItem`, `TryTakeItem`, `TryPunishPlayer`).
-   Each tool has a corresponding JSON description method (`GetChatToolDescription`, `GetOfferQuestToolDescription`, etc.) that returns a schema matching the OpenAI function/tool definition format. Descriptions for tools involving items (`TryGiveItem`, `TryOfferQuest`) dynamically include the relevant item lists (`GivableItems`, `QuestItems`) from the NPC's component data. These descriptions are sent to the LLM.
-   All tool descriptions now include an optional `npcResponse` string parameter. If the LLM provides this parameter in a tool call, `ExecuteToolCall` will make the NPC speak the `npcResponse` text *before* executing the tool's primary action. This allows the AI to comment on its actions without needing a separate `TryChat` call.
-   When the LLM decides to use a tool, `AIManager` parses the requested tool name and arguments.
-   `AINPCSystem` receives the parsed tool request and calls the corresponding C# method via `ExecuteToolCall`. Validation logic (e.g., checking `GivableItems` for `TryGiveItem`, checking range/whitelist for `TryPunishPlayer`, checking for existing quests in `TryOfferQuest` [placeholder]) is performed within the respective tool methods.

### 5.1. Available Tools

-   **`TryChat`**: Makes the NPC speak a given message. Primarily for standard conversation turns where no other action is taken. Can use `npcResponse` as an alternative message.
-   **`TryOfferQuest`**: Offers a quest to retrieve one item from the NPC's `QuestItems` list. Checks for existing quests (placeholder). Requires `targetPlayer` and `npcResponse` (containing the quest offer text).
-   **`TryGiveItem`**: Spawns and attempts to give an item (from the NPC's `GivableItems` list) to a player, typically as a quest reward. Requires `targetPlayer` and `itemPrototypeId`. Can use `npcResponse` for commentary (e.g., "Here's your reward.").
-   **`TryTakeItem`**: Attempts to take a specified item (by prototype ID) from a player's active hand, typically a requested quest item. Requires `targetPlayer`, `requestedItemName`, and `npcResponse` (containing the request/instruction, e.g., "Let me see that item. Hold it out.").
-   **`TryPunishPlayer`**: Applies damage and plays a sound (if configured in `AiNpcComponent`) to a target player within range. Requires `targetPlayer` and `reason`. Can use `npcResponse` for a threat or comment during the attack.

## 6. Current Limitations / Future Improvements

-   **Quest Tracking:** The `TryOfferQuest` tool currently uses placeholder logic. A proper system is needed to track active quests per player to prevent multiple simultaneous quests from the same NPC.
-   **Entity/Item Lookup:** Identifying players (by CKey/name) and items (by prototype ID in hand) remains basic. More robust lookups are needed.
-   **`TryTakeItem` Interaction:** The current implementation only checks the active hand and simulates the take (no actual transfer/deletion yet). A proper interaction flow is required.
-   **Tool Result Feedback:** The system reports tool success/failure back to the LLM via "tool" role messages in the history, allowing the AI to potentially react to outcomes. Further refinement might be needed.
-   **Multiple Tool Calls:** The system supports processing multiple tool calls returned by the AI in a single response.
-   **Error Handling:** More nuanced error handling and potential fallback responses for the NPC could be added (e.g., specific chat messages on tool failure).
-   **Contextual Awareness:** Context remains limited to conversation history. Adding NPC inventory, game state, etc., could enable more complex behaviors.
-   **Punishment Nuance:** `TryPunishPlayer` is still basic. More varied negative responses could be implemented.