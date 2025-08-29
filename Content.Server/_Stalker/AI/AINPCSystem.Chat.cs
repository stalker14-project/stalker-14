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
    public sealed partial class AINPCSystem : SharedAiNpcSystem
    {
        private string GetChatToolDescription()
        {
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


        /// <summary>
        /// Makes the NPC speak. Handles the npcResponse parameter to avoid double-speaking if called from ExecuteToolCall.
        /// </summary>
        public bool TryChat(EntityUid npc, string message, string? npcResponse = null)
        {
            var messageToSpeak = !string.IsNullOrWhiteSpace(npcResponse) ? npcResponse : message;

            if (string.IsNullOrWhiteSpace(messageToSpeak))
                return false;

            if (messageToSpeak != npcResponse)
                _sawmill.Debug($"NPC {ToPrettyString(npc)} executing chat: {messageToSpeak}");

            _chatSystem.TrySendInGameICMessage(npc, messageToSpeak, InGameICChatType.Speak, hideChat: false);
            return true;
        }

    }
}