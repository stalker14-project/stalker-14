using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Stalker.CCCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Content.Server._Stalker.AI;
using Content.Shared._Stalker.AI;

namespace Content.Server._Stalker.AI
{
    public sealed class AIManager : IPostInjectInit
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private ISawmill _sawmill = default!;
        private readonly HttpClient _httpClient = new();        private string _openRouterApiKey = string.Empty;
        private string _openRouterModel = string.Empty;
        private string _openRouterUrl = string.Empty;
        private const string ChatCompletionsEndpoint = "/chat/completions";
        private const string OpenRouterReferer = "https://github.com/Stalker14";
        private const string OpenRouterTitle = "Stalker14 SS14";

        public void PostInject()
        {
            IoCManager.InjectDependencies(this);
            _sawmill = Logger.GetSawmill("ai.manager");
        }

        public void Initialize()
        {
            _cfg.OnValueChanged(CCCCVars.OpenRouterApiKey, OnApiKeyChanged, true);
            _cfg.OnValueChanged(CCCCVars.OpenRouterModel, v => _openRouterModel = v, true);
            _cfg.OnValueChanged(CCCCVars.OpenRouterUrl, OnApiUrlChanged, true);

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
            _openRouterUrl = url.TrimEnd('/');
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
            messages.AddRange(conversationHistory);

            var tools = ParseToolDescriptions(toolDescriptionsJson);
            if (tools == null)
            {
                return AIResponse.Failure("Failed to parse tool descriptions.");
            }
            var requestPayload = new OpenRouterChatRequest
            {
                Model = _openRouterModel,
                Messages = messages,
                Tools = tools.Count > 0 ? tools : null,
                ToolChoice = tools.Count > 0 ? "auto" : null
            };

            _sawmill.Debug($"Sending AI request for model {_openRouterModel} (NPC: {npcUid})");
            var requestUrl = _openRouterUrl + ChatCompletionsEndpoint;
            _sawmill.Debug($"Request URL: {requestUrl}");


            try
            {
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
                string rawResponseContent = await response.Content.ReadAsStringAsync(cancel);
                _sawmill.Debug($"OpenRouter Raw Response for {npcUid} (Status: {response.StatusCode}):\n{rawResponseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancel);
                    _sawmill.Error($"HTTP request to OpenRouter failed with status {response.StatusCode}. URL: {requestUrl}. Body: {errorBody}");
                    return AIResponse.Failure($"Error communicating with AI service: {response.ReasonPhrase} ({response.StatusCode})");
                }

                var responseData = JsonSerializer.Deserialize<OpenRouterChatResponse>(rawResponseContent);
                if (responseData == null || responseData.Choices == null || responseData.Choices.Count == 0)
                {
                    _sawmill.Warning($"Received empty or invalid response from OpenRouter for {npcUid}. URL: {requestUrl}");
                    return AIResponse.Failure("Received empty response from AI service.");
                }

                var choice = responseData.Choices[0];
                var message = choice.Message;

                if (message?.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    var toolCallRequests = new List<AIToolCall>();
                    _sawmill.Debug($"AI returned {message.ToolCalls.Count} tool calls for {npcUid}.");

                    foreach (var toolCall in message.ToolCalls)
                    {
                        if (toolCall.Function == null || string.IsNullOrWhiteSpace(toolCall.Function.Name) || string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
                        {
                            _sawmill.Warning($"Received invalid tool call structure in multi-call response from OpenRouter for {npcUid}: {JsonSerializer.Serialize(toolCall)}");
                            continue;
                        }
                        _sawmill.Debug($"Processing tool call: {toolCall.Function.Name} with args: {toolCall.Function.Arguments}");
                        try
                        {
                            var argumentsNode = JsonNode.Parse(toolCall.Function.Arguments);
                            if (argumentsNode is JsonObject argumentsObject)
                            {
                                toolCallRequests.Add(new AIToolCall(toolCall.Id, toolCall.Function.Name, argumentsObject));
                            }
                            else
                            {
                                _sawmill.Warning($"Could not parse tool call arguments as JSON object for {npcUid} (Tool: {toolCall.Function.Name}): {toolCall.Function.Arguments}");
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            _sawmill.Warning($"Failed to parse tool call arguments JSON for {npcUid} (Tool: {toolCall.Function.Name}): {jsonEx.Message}. Arguments: {toolCall.Function.Arguments}");
                            // Optionally, return a failure for the entire request if one tool call is bad?
                            // For now, just skip this specific tool call.
                        }
                    }

                    // Return the list of successfully parsed tool call requests
                    if (toolCallRequests.Count > 0)
                    {
                        return AIResponse.ToolCalls(toolCallRequests); // Use the new factory method
                    }
                    else
                    {
                        _sawmill.Warning($"AI returned tool calls, but none could be successfully parsed for {npcUid}.");
                        return AIResponse.Failure("AI returned tool calls, but failed to parse arguments.");
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
    }

    public record OpenRouterMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore]
        public string? Name { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenRouterToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
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
        public object? ToolChoice { get; set; }
    }

    public record OpenRouterTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

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

        [JsonPropertyName("parameters")]
        public JsonNode? ParametersRaw { get; set; }

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
        public List<AIToolCall>? ToolCallRequests { get; private init; } // Changed to List<AIToolCall>
        public string? ErrorMessage { get; private init; }

        private AIResponse() { } // Private constructor

        public static AIResponse Text(string text) => new() { Success = true, TextResponse = text };
        // Updated factory method for multiple tool calls
        public static AIResponse ToolCalls(List<AIToolCall> toolCalls) => new() { Success = true, ToolCallRequests = toolCalls };
        public static AIResponse Failure(string error) => new() { Success = false, ErrorMessage = error };
    }

    // Represents a request for AINPCSystem to execute a tool
    // No changes needed here, AINPCSystem will handle the list
    public record AIToolCall(string ToolCallId, string ToolName, JsonObject Arguments); // Added ToolCallId
}
