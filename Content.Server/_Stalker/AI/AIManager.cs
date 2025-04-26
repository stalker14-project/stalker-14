// Content.Server/_Stalker/AI/AIManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json; // For ReadFromJsonAsync, PostAsJsonAsync
using System.Text; // For Encoding
using System.Text.Json;
using System.Text.Json.Nodes; // For JsonNode, JsonObject
using System.Text.Json.Serialization; // For JsonNode, JsonObject
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Stalker.CCCCVars; // Assuming CVars might be shared
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects; // For EntityUid
using Robust.Shared.IoC;
using Robust.Shared.Log; // Added for ISawmill
using Robust.Shared.Reflection; // For ReflectionManager if needed later
using Robust.Shared.Timing; // For IGameTiming if needed later
using Content.Server._Stalker.AI;
using Content.Shared._Stalker.AI;

namespace Content.Server._Stalker.AI
{
    public sealed class AIManager : IPostInjectInit
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private ISawmill _sawmill = default!;
        private readonly HttpClient _httpClient = new();

        // Configuration
        private string _openRouterApiKey = string.Empty;
        private string _openRouterModel = string.Empty;
        private string _openRouterUrl = string.Empty; // Base URL from CVar

        // Constants/Headers
        private const string ChatCompletionsEndpoint = "/chat/completions";
        private const string OpenRouterReferer = "https://github.com/Stalker14"; // Replace with your actual project URL
        private const string OpenRouterTitle = "Stalker14 SS14"; // Replace with your actual project Title

        public void PostInject()
        {
            IoCManager.InjectDependencies(this);
            _sawmill = Logger.GetSawmill("ai.manager");
        }

        public void Initialize()
        {
            // Subscribe to CVar changes and update HttpClient accordingly
            _cfg.OnValueChanged(CCCCVars.OpenRouterApiKey, OnApiKeyChanged, true);
            _cfg.OnValueChanged(CCCCVars.OpenRouterModel, v => _openRouterModel = v, true);
            _cfg.OnValueChanged(CCCCVars.OpenRouterUrl, OnApiUrlChanged, true); // Use specific handler for URL

            // Set default headers recommended by OpenRouter
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", OpenRouterReferer);
            _httpClient.DefaultRequestHeaders.Add("X-Title", OpenRouterTitle);

            _sawmill.Info("AI Manager Initialized");
        }

        private void OnApiKeyChanged(string apiKey)
        {
            _openRouterApiKey = apiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                _sawmill.Info("OpenRouter API key set.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                _sawmill.Warning("OpenRouter API key cleared or not set.");
            }
        }

        private void OnApiUrlChanged(string url)
        {
            // Store the base URL provided by the CVar
            _openRouterUrl = url.TrimEnd('/'); // Remove trailing slash if present
            if (string.IsNullOrWhiteSpace(_openRouterUrl))
            {
                _sawmill.Warning("OpenRouter URL CVar is empty. AI requests will fail.");
            }
            else
            {
                _sawmill.Info($"OpenRouter base URL set to: {_openRouterUrl}");
            }
        }

        /// <summary>
        /// Sends context and available tools to the LLM and returns the desired action or response.
        /// </summary>
        /// <param name="npcUid">The UID of the NPC initiating the request.</param>
        /// <param name="npcPrompt">The base personality/instruction prompt for the NPC.</param>
        /// <param name="conversationHistory">List of messages representing the recent conversation.</param>
        /// <param name="currentUserMessage">The latest message from the user/player.</param>
        /// <param name="toolDescriptionsJson">List of JSON strings describing available tools.</param>
        /// <param name="cancel">Cancellation token.</param>
        /// <returns>An AIResponse containing either text or a tool call request.</returns>
        public async Task<AIResponse> GetActionAsync(
            EntityUid npcUid,
            string npcPrompt,
            List<OpenRouterMessage> conversationHistory,
            string currentUserMessage,
            List<string> toolDescriptionsJson,
            CancellationToken cancel = default)
        {
            if (string.IsNullOrEmpty(_openRouterApiKey) || string.IsNullOrEmpty(_openRouterUrl) || string.IsNullOrEmpty(_openRouterModel))
            {
                _sawmill.Warning($"AI request failed for {npcUid}: OpenRouter configuration missing (URL: {_openRouterUrl}, Model: {_openRouterModel}, Key set: {!string.IsNullOrEmpty(_openRouterApiKey)})");
                return AIResponse.Failure("OpenRouter configuration is incomplete.");
            }

            var messages = new List<OpenRouterMessage>
            {
                new() { Role = "system", Content = npcPrompt }
            };
            // The conversationHistory list already contains the latest user message with the speaker name,
            // added by AINPCSystem. Do not add currentUserMessage separately here.
            messages.AddRange(conversationHistory);
            // messages.Add(new OpenRouterMessage { Role = "user", Content = currentUserMessage }); // REMOVED redundant message add

            var tools = ParseToolDescriptions(toolDescriptionsJson);
            if (tools == null)
            {
                return AIResponse.Failure("Failed to parse tool descriptions.");
            }

            var requestPayload = new OpenRouterChatRequest
            {
                Model = _openRouterModel,
                Messages = messages,
                Tools = tools.Count > 0 ? tools : null, // Only include tools if there are any
                ToolChoice = tools.Count > 0 ? "auto" : null // Let the model decide whether to use tools if any are provided
            };

            _sawmill.Debug($"Sending AI request for model {_openRouterModel} (NPC: {npcUid})");

            // Construct the full URL
            var requestUrl = _openRouterUrl + ChatCompletionsEndpoint;
            _sawmill.Debug($"Request URL: {requestUrl}");


            try
            {
                // Log the request payload before sending
                try
                {
                    var payloadJson = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = true });
                    _sawmill.Debug($"OpenRouter Request Payload for {npcUid}:\n{payloadJson}");
                }
                catch (Exception jsonEx)
                {
                    _sawmill.Error($"Failed to serialize request payload for logging: {jsonEx.Message}");
                }

                var response = await _httpClient.PostAsJsonAsync(requestUrl, requestPayload, cancel);

                // Log raw response content regardless of status code for debugging
                string rawResponseContent = await response.Content.ReadAsStringAsync(cancel);
                _sawmill.Debug($"OpenRouter Raw Response for {npcUid} (Status: {response.StatusCode}):\n{rawResponseContent}");


                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancel);
                    _sawmill.Error($"HTTP request to OpenRouter failed with status {response.StatusCode}. URL: {requestUrl}. Body: {errorBody}");
                    return AIResponse.Failure($"Error communicating with AI service: {response.ReasonPhrase} ({response.StatusCode})");
                }

                // Attempt to deserialize the logged raw response
                var responseData = JsonSerializer.Deserialize<OpenRouterChatResponse>(rawResponseContent);

                // Original checks remain
                if (responseData == null || responseData.Choices == null || responseData.Choices.Count == 0)
                {
                    _sawmill.Warning($"Received empty or invalid response from OpenRouter for {npcUid}. URL: {requestUrl}");
                    return AIResponse.Failure("Received empty response from AI service.");
                }

                var choice = responseData.Choices[0];
                var message = choice.Message;

                // Check for tool calls first
                if (message?.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    // For now, only process the first tool call
                    var toolCall = message.ToolCalls[0];
                    if (toolCall.Function == null || string.IsNullOrWhiteSpace(toolCall.Function.Name) || string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
                    {
                        _sawmill.Warning($"Received invalid tool call structure from OpenRouter for {npcUid}: {JsonSerializer.Serialize(toolCall)}");
                        return AIResponse.Failure("Received invalid tool call from AI.");
                    }

                    _sawmill.Debug($"AI requested tool call: {toolCall.Function.Name} with args: {toolCall.Function.Arguments}");
                    try
                    {
                        // Attempt to parse arguments string into a JsonObject
                        var argumentsNode = JsonNode.Parse(toolCall.Function.Arguments);
                        if (argumentsNode is JsonObject argumentsObject)
                        {
                            // Return the tool call request for AINPCSystem to handle
                            return AIResponse.ToolCall(toolCall.Function.Name, argumentsObject);
                        }
                        else
                        {
                            _sawmill.Warning($"Could not parse tool call arguments as JSON object for {npcUid}: {toolCall.Function.Arguments}");
                            return AIResponse.Failure("Failed to parse tool call arguments.");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _sawmill.Warning($"Failed to parse tool call arguments JSON for {npcUid}: {jsonEx.Message}. Arguments: {toolCall.Function.Arguments}");
                        return AIResponse.Failure("Failed to parse tool call arguments JSON.");
                    }
                }
                // Check for text content if no tool calls
                else if (!string.IsNullOrWhiteSpace(message?.Content))
                {
                    _sawmill.Debug($"AI returned text response for {npcUid}: {message.Content}");
                    return AIResponse.Text(message.Content);
                }
                else
                {
                    _sawmill.Warning($"Received response with no content or tool calls from OpenRouter for {npcUid}. Finish Reason: {choice.FinishReason}");
                    return AIResponse.Failure($"AI returned no usable response (Finish Reason: {choice.FinishReason}).");
                }
            }
            catch (HttpRequestException e)
            {
                _sawmill.Error($"HTTP request to OpenRouter failed for {npcUid} at {requestUrl}: {e.Message}");
                return AIResponse.Failure($"Error communicating with AI service: {e.Message}");
            }
            catch (JsonException e)
            {
                _sawmill.Error($"Failed to serialize/deserialize JSON for OpenRouter for {npcUid} at {requestUrl}: {e.Message}");
                return AIResponse.Failure($"Error processing AI service response: {e.Message}");
            }
            catch (TaskCanceledException) // Handle cancellation explicitly
            {
                _sawmill.Info($"AI request cancelled for {npcUid}.");
                return AIResponse.Failure("AI request cancelled.");
            }
            catch (Exception e)
            {
                _sawmill.Error($"Unexpected error during AI request for {npcUid} at {requestUrl}: {e.ToString()}");
                return AIResponse.Failure($"Unexpected error: {e.Message}");
            }
        }

        private List<OpenRouterTool>? ParseToolDescriptions(List<string> toolDescriptionsJson)
        {
            var tools = new List<OpenRouterTool>();
            foreach (var jsonString in toolDescriptionsJson)
            {
                try
                {
                    _sawmill.Debug($"Attempting to parse tool description JSON: {jsonString}");
                    // Deserialize the JSON string which should represent the 'function' part of the tool
                    var function = JsonSerializer.Deserialize<OpenRouterFunction>(jsonString);

                    if (function == null)
                    {
                        _sawmill.Warning($"Deserialized function description is null for JSON: {jsonString}");
                        continue; // Skip this tool
                    }

                    if (string.IsNullOrWhiteSpace(function.Name))
                    {
                         _sawmill.Warning($"Deserialized function has missing name for JSON: {jsonString}");
                         continue; // Skip this tool
                    }

                    _sawmill.Debug($"Parsed function name: {function.Name}");

                    // Ensure parameters are represented as JsonObject if present
                    if (function.ParametersRaw != null)
                    {
                        _sawmill.Debug($"Function '{function.Name}' has ParametersRaw: {function.ParametersRaw.ToJsonString()}");
                        function.Parameters = function.ParametersRaw as JsonObject;
                        if (function.Parameters == null)
                        {
                            _sawmill.Warning($"Tool description parameters for '{function.Name}' could not be parsed as a JSON object: {function.ParametersRaw.ToJsonString()}");
                            // Decide if this is a fatal error or if the tool can be added without parameters. Let's add it without for now.
                        }
                        else
                        {
                             _sawmill.Debug($"Successfully parsed parameters for '{function.Name}' as JsonObject.");
                        }
                    }
                    else
                    {
                         _sawmill.Debug($"Function '{function.Name}' has no ParametersRaw field.");
                    }

                    tools.Add(new OpenRouterTool { Type = "function", Function = function });
                    _sawmill.Debug($"Successfully added tool '{function.Name}' to list.");
                }
                catch (JsonException e)
                {
                    _sawmill.Error($"Failed to parse tool description JSON: {e.Message}. JSON: {jsonString}");
                    return null; // Indicate failure - parsing one tool failed
                }
            }
            _sawmill.Debug($"Finished parsing tool descriptions. Total tools parsed: {tools.Count}");
            return tools;
        }


        // --- Helper Classes for JSON Serialization (OpenAI/OpenRouter Compatible) ---
    }

    
    public record OpenRouterMessage // Made public for AINPCSystem to potentially use
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty; // "system", "user", "assistant", "tool"

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Content is null for tool calls
        public string? Content { get; set; }

        [JsonPropertyName("name")] // Added field for speaker name (OpenAI compatible)
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; } // Character Name

        [JsonPropertyName("ckey")] // Changed field name to reflect CKey/Username usage
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CKey { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenRouterToolCall>? ToolCalls { get; set; } // Only for assistant messages with tool calls

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; } // Only for tool role messages (response to a tool call)
    }
    public record OpenRouterChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenRouterMessage> Messages { get; set; } = new();

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Don't include if null
        public List<OpenRouterTool>? Tools { get; set; }

        [JsonPropertyName("tool_choice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ToolChoice { get; set; } // Can be "auto", "none", or {"type": "function", "function": {"name": "my_function"}}

        // Add other parameters like temperature, max_tokens etc. if needed
        // [JsonPropertyName("temperature")]
        // public float? Temperature { get; set; }
    }


    public record OpenRouterTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function"; // Currently only "function" is supported

        [JsonPropertyName("function")]
        public OpenRouterFunction Function { get; set; } = new();
    }

    public record OpenRouterFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        // We deserialize the raw parameters description first
        [JsonPropertyName("parameters")]
        // [JsonIgnore(Condition = JsonIgnoreCondition.Always)] // REMOVED: This might interfere with deserialization.
        public JsonNode? ParametersRaw { get; set; }
    
        // We expose the parsed JsonObject for use, but don't serialize it directly
        [JsonIgnore]
        public JsonObject? Parameters { get; set; }
    }

    public record OpenRouterChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; } // e.g., "chat.completion"

        [JsonPropertyName("created")]
        public long? Created { get; set; } // Unix timestamp

        [JsonPropertyName("model")]
        public string? Model { get; set; } // Model used

        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenRouterUsage? Usage { get; set; }
    }

    public record OpenRouterChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; } // Assistant's response message

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; } // e.g., "stop", "length", "tool_calls"
    }

    public record OpenRouterUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    public record OpenRouterToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // Tool call ID

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenRouterToolFunction? Function { get; set; }
    }

    public record OpenRouterToolFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty; // Arguments are a JSON *string* from the API
    }


    // --- Internal Response Handling ---
    public record AIResponse
    {
        public bool Success { get; private init; }
        public string? TextResponse { get; private init; }
        public AIToolCall? ToolCallRequest { get; private init; }
        public string? ErrorMessage { get; private init; }

        private AIResponse() { } // Private constructor

        public static AIResponse Text(string text) => new() { Success = true, TextResponse = text };
        public static AIResponse ToolCall(string toolName, JsonObject arguments) => new() { Success = true, ToolCallRequest = new AIToolCall(toolName, arguments) };
        public static AIResponse Failure(string error) => new() { Success = false, ErrorMessage = error };
    }

    // Represents a request for AINPCSystem to execute a tool
    public record AIToolCall(string ToolName, JsonObject Arguments);
}
