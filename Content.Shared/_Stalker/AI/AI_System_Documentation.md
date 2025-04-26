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
    -   Manages the conversation history for each individual AI NPC internally (using a `Dictionary<EntityUid, List<OpenRouterMessage>>`). History includes speaker name and UserId (CKey) for user messages.
    -   Defines the available "tools" (C# methods like `TryChat`, `TryGiveItem`, `TryTakeItem`) that the AI can request to use.
    -   Provides JSON descriptions of these tools to `AIManager` for inclusion in the API request. The description for `TryGiveItem` is dynamically generated based on the NPC's `GivableItems` list.
    -   Calls `AIManager.GetActionAsync` asynchronously when an NPC needs to respond to speech.
    -   Processes the `AIResponse` returned by `AIManager` on the main game thread:
        -   If it's a text response, uses `ChatSystem` to make the NPC speak.
        -   If it's a tool call request, parses the arguments and invokes the corresponding local tool method (e.g., `TryGiveItem`).
    -   Handles the game logic for executing the tool actions (spawning items, initiating transfers via `SharedHandsSystem`, etc.). `TryGiveItem` now validates the requested item and quantity against the NPC's `GivableItems` list before proceeding.
    -   Manages cleanup of conversation history when an NPC entity is removed.
-   **Key Interactions:** Listens for `EntitySpokeEvent`; calls `AIManager`; calls `ChatSystem` and `SharedHandsSystem` to perform actions.

### 2.3. `AiNpcComponent` (Shared - `Content.Shared/_Stalker/AI/AiNpcComponent.cs`)

-   **Responsibilities:**
    -   A marker component (`[RegisterComponent]`) identifying an entity as being controlled by the `AINPCSystem`.
    -   Networked (`[NetworkedComponent]`) so clients are aware of the component's existence (though its state is minimal).
    -   Stores basic, potentially configurable parameters:
        -   `BasePrompt`: The initial system prompt/personality instruction sent to the LLM.
        -   `MaxHistory`: The maximum number of messages to retain in the server-side conversation history for this NPC.
        -   `GivableItems`: A list (`List<ManagedItemInfo>`) defining items the NPC can potentially give out (e.g., as rewards, trade), along with their `ProtoId`, `MaxQuantity` (per interaction), and `ItemRarity`. The `TryGiveItem` tool is restricted to this list.
        -   `QuestItems`: A list (`List<ManagedItemInfo>`) defining items relevant to quests (e.g., items the player needs to find), along with their `ProtoId`, `MaxQuantity`, and `ItemRarity`. (Currently used for context, future quest systems might leverage this more directly).
-   **Key Interactions:** Attached to NPC entities; read by `AINPCSystem`. **Crucially, it does NOT store the conversation history itself**, as that is server-only state managed by the system.

## 3. Interaction Flow (Example: Player Speaks to NPC)

1.  A player entity (with `ActorComponent`) speaks near an NPC (with `AiNpcComponent`).
2.  `ChatSystem` processes the speech and raises an `EntitySpokeEvent`.
3.  `AINPCSystem.OnEntitySpoke` receives the event.
4.  It identifies the speaker (player) and nearby AI NPCs within range.
5.  It filters out the NPC reacting to itself or non-player entities.
6.  For each relevant NPC, it retrieves/updates the internal conversation history, adding the player's message (with name and UserId).
7.  It gathers the NPC's `BasePrompt`, the current `history`, the player's `userMessage`, and the JSON descriptions of available tools.
8.  It calls `AIManager.GetActionAsync` in a background task, passing this data.
9.  `AIManager` constructs the JSON payload for the OpenRouter API (using OpenAI format).
10. `AIManager` sends the request via `HttpClient` and awaits the response.
11. `AIManager` parses the response, determining if it's text or a tool call, and creates an `AIResponse` record.
12. The background task queues a `ProcessAIResponseEvent` containing the `AIResponse` back to the main game thread, targeting the specific NPC.
13. `AINPCSystem.HandleAIResponse` receives the event on the main thread.
14. If the `AIResponse` contains text, `AINPCSystem` calls `TryChat` -> `ChatSystem.TrySendInGameICMessage` to make the NPC speak, and updates the history with the assistant's message.
15. If the `AIResponse` contains a tool call request, `AINPCSystem` calls `ExecuteToolCall`, which parses arguments and invokes the appropriate local method (`TryGiveItem`, `TryTakeItem`, etc.). The result of the tool execution is logged. The assistant's attempt (the tool call itself) should ideally be added to history.

## 4. Configuration

-   **API:** Configured via CVars in `Content.Shared._Stalker.CCCCVars`:
    -   `openrouter.apikey`: Your OpenRouter API key (Confidential).
    -   `openrouter.model`: The specific model ID to use (e.g., `mistralai/mistral-small-3.1-24b-instruct:free`).
    -   `openrouter.url`: The base URL for the API endpoint (e.g., `https://openrouter.ai/api/v1`).
-   **NPC:** Configured via `AiNpcComponent` fields in entity prototypes:
    -   `prompt`: Sets the base personality/instructions.
    -   `maxHistory`: Controls conversation memory length.
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
-   They are defined as public methods within `AINPCSystem` (e.g., `TryChat`, `TryGiveItem`).
-   Each tool has a corresponding JSON description method (`GetChatToolDescription`, etc.) that returns a schema matching the OpenAI function/tool definition format. The description for `TryGiveItem` dynamically includes the list of items the specific NPC is allowed to give, based on its `GivableItems` component data. This description is sent to the LLM.
-   When the LLM decides to use a tool, `AIManager` parses the requested tool name and arguments.
-   `AINPCSystem` receives the parsed tool request and calls the corresponding C# method via `ExecuteToolCall`. The `TryGiveItem` method now includes validation logic to ensure the requested item and quantity are permitted according to the NPC's `GivableItems` list.

## 6. Current Limitations / Future Improvements

-   **Entity Lookup:** Identifying players and items based on string names/identifiers provided by the AI is currently very basic (placeholder `FindPlayerByIdentifier`, `FindItemInHands`). A more robust lookup system (using CKey/UserId, EntityUid parsing, or better inventory searching) is needed.
-   **`TryTakeItem` Interaction:** The current implementation assumes player consent and simulates the item transfer. A proper implementation requires a player interaction flow (dialogue prompt, UI confirmation, etc.).
-   **Tool Result Feedback:** The system currently executes tools but doesn't report the success/failure result back to the LLM in a subsequent API call. Implementing this feedback loop would allow the AI to react to the outcome of its requested actions (e.g., confirming an item was given or explaining why it failed).
-   **Multiple Tool Calls:** The API supports multiple tool calls in one response, but the current implementation only processes the first one.
-   **Error Handling:** More nuanced error handling and potential fallback responses for the NPC could be added.
-   **Contextual Awareness:** The context provided to the AI is currently limited to conversation history. Adding information about the NPC's inventory, surroundings, or current game state could enable more complex behaviors.