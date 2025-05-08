# Stalker AI NPC System Documentation

This document outlines the design and functionality of the AI-powered NPC interaction system implemented within the Stalker module for Space Station 14.

## 1. Goal

The primary goal of this system is to enable Non-Player Characters (NPCs) to engage in more dynamic and context-aware interactions with players using Large Language Models (LLMs) via the OpenRouter API. This includes basic conversation, responding to player speech, performing actions like giving or taking items, offering and completing quests, and reacting to player behavior based on AI decisions.

## 2. Core Components

The system is primarily composed of three parts: a server-side manager for API communication, a server-side entity system for NPC logic, and a shared component to mark AI-enabled NPCs.

### 2.1. `AIManager` (Server - `Content.Server/_Stalker/AI/AIManager.cs`)

-   **Responsibilities:**
    -   Acts as the sole interface with the external OpenRouter API (or any OpenAI-compatible endpoint).
    -   Manages API configuration (URL, Model Name, API Key) read from `CCCCVars` (`openrouter.url`, `openrouter.model`, `openrouter.apikey`).
    -   Constructs the request payload (including system prompt, conversation history, and available tools) in the format expected by the OpenAI Chat Completions API. The current user message is part of the conversation history.
    -   Sends requests to the LLM using `HttpClient`.
    -   Receives and parses the LLM's response.
    -   Determines if the response is a simple text message or a request to execute one or more "tools" (function calls).
    -   Packages the result (text or tool call details, including support for multiple tool calls) into an `AIResponse` record.
    -   Handles API errors and communication failures gracefully.
-   **Key Interactions:** Called by `AINPCSystem` to get an AI response; calls the OpenRouter API.

### 2.2. `AINPCSystem` (Server - `Content.Server/_Stalker/AI/AINPCSystem.cs`)

-   **Responsibilities:**
    -   Manages entities possessing the `AiNpcComponent`.
    -   Subscribes to `EntitySpokeEvent` to detect nearby speech from player characters (`ActorComponent`).
    -   Manages the conversation history for each AI NPC, storing separate histories for each interacting player (identified by their CKey) internally (using a `Dictionary<EntityUid, Dictionary<string, List<OpenRouterMessage>>>`). User messages in history are prepended with `[CKEY: {speakerCKey}]`. History includes speaker name and CKey for user messages, assistant responses (which can include multiple tool calls), and tool results (linked by `ToolCallId`).
    -   Defines the available "tools" (C# methods like `TryChat`, `TryOfferQuest`, `TryGiveItem`, `TryTakeItem`, `TryPunishPlayer`, `TryCompleteQuest`) that the AI can request to use. Each tool's availability can be toggled via `AiNpcComponent` flags (e.g., `CanChat`, `CanGiveItems`).
    -   Provides JSON descriptions of these tools to `AIManager` for inclusion in the API request. Descriptions for tools involving items (`TryGiveItem`, `TryOfferQuest`, `TryCompleteQuest`) dynamically include the relevant item lists (`GivableItems`, `QuestItems`) from the NPC's component data.
    -   Calls `AIManager.GetActionAsync` asynchronously when an NPC needs to respond to speech (after checking `Enabled` and `SponsorOnly` flags in `AiNpcComponent`).
    -   Processes the `AIResponse` returned by `AIManager` on the main game thread:
        -   If it's a text response, uses `ChatSystem` to make the NPC speak.
        -   If it's one or more tool call requests, parses the arguments and invokes the corresponding local tool method (e.g., `TryGiveItem`) for each call. Tool calls can include an `npcResponse` argument, causing the NPC to speak that text *before* executing the tool's primary action.
    -   Handles the game logic for executing the tool actions. `TryGiveItem` validates against `GivableItems`, spawns the item, attempts to place it in the player's hand, and drops it near the NPC if pickup fails. `TryTakeItem` requires the player to hold the item, which is then dropped by the player and moved to the NPC's location. `TryCompleteQuest` combines `TryTakeItem` and `TryGiveItem` logic.
    -   Manages cleanup of all associated player conversation histories when an NPC entity is removed.
-   **Key Interactions:** Listens for `EntitySpokeEvent`; calls `AIManager` (passing player-specific history); calls `ChatSystem` and `SharedHandsSystem` to perform actions.

### 2.3. `AiNpcComponent` (Shared - `Content.Shared/_Stalker/AI/AiNpcComponent.cs`)

-   **Responsibilities:**
    -   A marker component (`[RegisterComponent]`) identifying an entity as being controlled by the `AINPCSystem`.
    -   Networked (`[NetworkedComponent]`) so clients are aware of the component's existence (though its state is minimal).
    -   Stores basic, potentially configurable parameters:
        -   `BasePrompt`: The initial system prompt/personality instruction sent to the LLM.
        -   `Enabled`: Boolean, globally enables or disables AI interactions for this NPC.
        -   `SponsorOnly`: Boolean, if true, only players with sponsor status can interact.
        -   `CanChat`: Boolean, enables/disables the `TryChat` tool.
        -   `MaxHistoryPerPlayer`: The maximum number of messages (user, assistant, tool calls/results) to retain in the server-side conversation history *for each individual player* interacting with this NPC.
        -   `InteractionRange`: Float, the range at which the NPC "hears" and responds to player speech.
        -   `CanGiveItems`: Boolean, enables/disables the `TryGiveItem` tool.
        -   `GivableItems`: A list (`List<ManagedItemInfo>`) defining items the NPC can potentially give out (e.g., as rewards, trade), along with their `ProtoId`, `MaxQuantity` (per interaction), and `ItemRarity`. The `TryGiveItem` and `TryCompleteQuest` (for rewards) tools are restricted to this list.
        -   `CanOfferQuests`: Boolean, enables/disables the `TryOfferQuest` and `TryCompleteQuest` tools.
        -   `QuestItems`: A list (`List<ManagedItemInfo>`) defining items relevant to quests (e.g., items the player needs to find), along with their `ProtoId`, `MaxQuantity`, and `ItemRarity`. The `TryOfferQuest` and `TryCompleteQuest` (for required items) tools use this list.
        -   `CanTakeItems`: Boolean, enables/disables the `TryTakeItem` tool.
        -   `CanPunish`: Boolean, enables/disables the `TryPunishPlayer` tool.
        -   `PunishmentDamage`: A `DamageSpecifier` defining the damage dealt by `TryPunishPlayer`.
        -   `PunishmentSound`: A `SoundSpecifier` for sound played during punishment.
        -   `PunishmentWhitelist`: An `EntityWhitelist` for entities immune to `TryPunishPlayer`.
-   **Key Interactions:** Attached to NPC entities; read by `AINPCSystem`. **Crucially, it does NOT store the conversation history itself**, as that is server-only state managed by `AINPCSystem`.

## 3. Interaction Flow (Example: Player Speaks to NPC)

1.  A player entity (with `ActorComponent`) speaks near an NPC (with `AiNpcComponent`).
2.  `ChatSystem` processes the speech and raises an `EntitySpokeEvent`.
3.  `AINPCSystem.OnEntitySpoke` receives the event.
4.  It identifies the speaker (player) and their CKey.
5.  It iterates through nearby AI NPCs (with `AiNpcComponent`) within `InteractionRange`.
6.  It checks if the NPC is `Enabled`. If `SponsorOnly` is true, it verifies the player's sponsor status, potentially replying and skipping if non-sponsor.
7.  It checks if an AI request is already ongoing for this NPC; if so, it ignores the new speech.
8.  For each relevant NPC, it retrieves/updates the internal conversation history *specific to that player's CKey*. The player's message is added to history, prepended with `[CKEY: {speakerCKey}]`. History is trimmed if it exceeds `MaxHistoryPerPlayer`.
9.  It gathers the NPC's `BasePrompt`, the player's specific `history`, and the JSON descriptions of available tools (based on `Can<Action>` flags in `AiNpcComponent`).
10. It calls `AIManager.GetActionAsync` in a background task, passing this data.
11. `AIManager` constructs the JSON payload for the OpenRouter API, including the system prompt, full conversation history for that player, and tool descriptions.
12. `AIManager` sends the request via `HttpClient` and awaits the response.
13. `AIManager` parses the response, determining if it's text or one or more tool calls, and creates an `AIResponse` record.
14. The background task queues a `ProcessAIResponseEvent` containing the `AIResponse` and the original player's `CKey` back to the main game thread, targeting the specific NPC.
15. `AINPCSystem.HandleAIResponse` receives the event on the main thread and disposes of the ongoing request token.
16. If the `AIResponse` contains text:
    a.  `AINPCSystem` calls `TryChat` (if `CanChat` is true) to make the NPC speak.
    b.  The specific player's history is trimmed.
    c.  The assistant's text message is added to that player's history.
17. If the `AIResponse` contains one or more tool call requests:
    a.  The assistant's message (containing the tool call decisions) is added to the player's history.
    b.  For each `AIToolCall` in the response:
        i.  `AINPCSystem` calls `ExecuteToolCall`.
        ii. `ExecuteToolCall` first checks if the tool call includes an `npcResponse` argument. If so, the NPC speaks this text.
        iii. `ExecuteToolCall` then invokes the corresponding C# tool method (e.g., `TryGiveItem`, `TryCompleteQuest`), passing necessary arguments (like player CKey, item IDs).
        iv. The specific player's history is trimmed.
        v.  The tool's result message (success/failure) is added to that player's history, associated with the `ToolCallId`.

## 4. Configuration

-   **API:** Configured via CVars in `Content.Shared._Stalker.CCCCVars`:
    -   `openrouter.apikey`: Your OpenRouter API key (Confidential).
    -   `openrouter.model`: The specific model ID to use (e.g., `mistralai/mistral-small-3.1-24b-instruct:free`).
    -   `openrouter.url`: The base URL for the API endpoint (e.g., `https://openrouter.ai/api/v1`).
-   **NPC:** Configured via `AiNpcComponent` fields in entity prototypes:
    -   `prompt`: Sets the base personality/instructions (e.g., "You are a helpful Sidorovich type NPC...").
    -   `enabled`: `true` or `false`. Globally enables/disables AI for this NPC.
    -   `sponsorOnly`: `true` or `false`. If true, only sponsors can interact.
    -   `canChat`: `true` or `false`. Enables/disables the `TryChat` tool.
    -   `maxHistoryPerPlayer`: Integer (e.g., `20`). Controls conversation memory length per player.
    -   `interactionRange`: Float (e.g., `2.0`). Range in tiles for hearing players.
    -   `canGiveItems`: `true` or `false`. Enables/disables `TryGiveItem`.
    -   `givableItems`: Defines the list of items the NPC is allowed to give. Example structure:
        ```yaml
        - type: AiNpc
          prompt: "..."
          enabled: true
          interactionRange: 3
          canGiveItems: true
          givableItems:
            - protoId: Medkit
              maxQuantity: 2
              rarity: Common
            - protoId: CombatKnife
              maxQuantity: 1
              rarity: Uncommon
        ```
    -   `canOfferQuests`: `true` or `false`. Enables/disables `TryOfferQuest` and `TryCompleteQuest`.
    -   `questItems`: Defines items relevant for quests. Example structure:
        ```yaml
          questItems:
            - protoId: DogTail
              maxQuantity: 5 # Max quantity NPC might handle/expect for a quest turn-in
              rarity: Common
            - protoId: Artifact
              maxQuantity: 1
              rarity: Rare
        ```
    -   `canTakeItems`: `true` or `false`. Enables/disables `TryTakeItem`.
    -   `canPunish`: `true` or `false`. Enables/disables `TryPunishPlayer`.
    -   `punishmentDamage`: Damage specifier (e.g., `{ types: { Blunt: 5 } }`).
    -   `punishmentSound`: Sound path (e.g., `{ path: "/Audio/Effects/punch.ogg" }`).
    -   `punishmentWhitelist`: Entity whitelist (e.g., `{ components: [Admin] }`).

## 5. Tools

-   Tools represent actions the AI can request the NPC to perform.
-   They are defined as public methods within `AINPCSystem` (e.g., `TryChat`, `TryOfferQuest`, `TryGiveItem`, `TryTakeItem`, `TryPunishPlayer`, `TryCompleteQuest`). Their availability is controlled by `Can<Action>` flags in `AiNpcComponent`.
-   Each tool has a corresponding JSON description method (e.g., `GetChatToolDescription`, `GetOfferQuestToolDescription`) that returns a schema matching the OpenAI function/tool definition format. Descriptions for tools involving items (`TryGiveItem`, `TryOfferQuest`, `TryCompleteQuest`) dynamically include the relevant item lists (`GivableItems`, `QuestItems`) from the NPC's component data. These descriptions are sent to the LLM.
-   All tool descriptions include an optional `npcResponse` string parameter. If the LLM provides this parameter in a tool call, `ExecuteToolCall` will make the NPC speak the `npcResponse` text *before* executing the tool's primary action. This allows the AI to comment on its actions without needing a separate `TryChat` call. For some tools like `TryTakeItem`, `TryOfferQuest`, and `TryCompleteQuest`, `npcResponse` is a *required* argument for the AI to provide.
-   When the LLM decides to use one or more tools, `AIManager` parses the requested tool names and arguments.
-   `AINPCSystem` receives the parsed tool request(s) and calls the corresponding C# method via `ExecuteToolCall` for each tool. Validation logic (e.g., checking `GivableItems` for `TryGiveItem`, checking range/whitelist for `TryPunishPlayer`, checking for existing quests in `TryOfferQuest` [placeholder], player CKey lookup) is performed within the respective tool methods.

### 5.1. Available Tools

-   **`TryChat`**: Makes the NPC speak a given message. Requires `message` argument. Can use `npcResponse` for the speech. Enabled by `CanChat`.
-   **`TryOfferQuest`**: Offers a quest to a player. The AI should specify the quest details (e.g., item to fetch from `QuestItems`) within the `npcResponse`. Requires player `ckey` and a *required* `npcResponse` (containing the quest offer text). Placeholder for actual quest tracking. Enabled by `CanOfferQuests`.
-   **`TryGiveItem`**: Spawns and attempts to give an item (from the NPC's `GivableItems` list) to a player. Requires player `ckey`, `itemPrototypeId`, and optional `quantity`. Can use `npcResponse` for commentary. Enabled by `CanGiveItems`.
-   **`TryTakeItem`**: Attempts to take a specified item (by prototype ID) from a player's active hand. Requires player `ckey`, `requestedItemName` (prototype ID), and a *required* `npcResponse` (containing the request/instruction, e.g., "Let me see that item. Hold it out."). Enabled by `CanTakeItems`.
-   **`TryPunishPlayer`**: Applies damage (from `PunishmentDamage`) and plays a sound (from `PunishmentSound`) to a target player within range, respecting `PunishmentWhitelist`. Requires player `ckey` and `reason`. Can use `npcResponse` for a threat or comment. Enabled by `CanPunish`.
-   **`TryCompleteQuest`**: Attempts to complete a quest. First, it tries to take `questItemId` from the player (using `TryTakeItem` logic). If successful, it gives `rewardItemId` (from `GivableItems`) in `rewardQuantity` to the player (using `TryGiveItem` logic). Requires player `ckey`, `questItemId`, `rewardItemId`, optional `rewardQuantity`, and a *required* `npcResponse` (e.g., "Good work, here's your payment."). Enabled by `CanOfferQuests`.

## 6. Current Limitations / Future Improvements

-   **Quest Tracking:** The `TryOfferQuest` tool currently uses placeholder logic. A proper system is needed to track active quests per player to prevent multiple simultaneous quests from the same NPC.
-   **Entity/Item Lookup:** Player identification is done via CKey. Item lookup for `TryTakeItem` checks the player's active hand.
-   **`TryTakeItem` Interaction:** The player must hold the requested item. The item is then dropped by the player and moved to the NPC's location.
-   **Tool Result Feedback:** The system reports tool success/failure back to the LLM via "tool" role messages in the history, allowing the AI to potentially react to outcomes. Further refinement might be needed.
-   **Multiple Tool Calls:** The system supports processing multiple tool calls returned by the AI in a single response.
-   **Error Handling:** More nuanced error handling and potential fallback responses for the NPC could be added (e.g., specific chat messages on tool failure).
-   **Contextual Awareness:** Context remains limited to conversation history. Adding NPC inventory, game state, etc., could enable more complex behaviors.
-   **Punishment Nuance:** `TryPunishPlayer` is still basic. More varied negative responses could be implemented.