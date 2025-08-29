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
        private string GetPunishPlayerToolDescription()
        {
            var description = "Punish a player perceived as rude, lying, or hostile by attacking them. Use sparingly.";

            return $@"{{
                ""name"": ""TryPunishPlayer"",
                ""description"": ""{JsonEncodedText.Encode(description)}"",
                ""parameters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                         ""ckey"": {{
                            ""type"": ""string"",
                            ""description"": ""The CKey (not name) of the player to punish.""
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
                    ""required"": [""ckey"", ""reason""]
                }}
            }}";
        }
        
        /// <summary>
        /// Attempts to punish a player by applying damage.
        /// </summary>
        public bool TryPunishPlayer(EntityUid npc, AiNpcComponent aiComp, string targetPlayerIdentifier, string reason, string? npcResponse = null)
        {
            _sawmill.Debug($"NPC {ToPrettyString(npc)} attempting to punish player: Target='{targetPlayerIdentifier}', Reason='{reason}'");

            EntityUid? targetPlayer = FindPlayerByIdentifier(targetPlayerIdentifier);
            if (targetPlayer == null || !targetPlayer.Value.Valid)
            {
                _sawmill.Warning($"Could not find target player '{targetPlayerIdentifier}' for TryPunishPlayer.");
                return false;
            }


            if (aiComp.PunishmentWhitelist != null && _whitelistSystem.IsWhitelistPass(aiComp.PunishmentWhitelist, targetPlayer.Value))
            {
                _sawmill.Info($"Target player {ToPrettyString(targetPlayer.Value)} is whitelisted. Punishment aborted.");
                return false;
            }

            const float punishRange = 5.0f;
            if (!Transform(npc).Coordinates.TryDistance(EntityManager, Transform(targetPlayer.Value).Coordinates, out var distance) || distance > punishRange)
            {
                _sawmill.Warning($"Target player {ToPrettyString(targetPlayer.Value)} too far ({distance}m) for NPC {ToPrettyString(npc)} to punish.");
                return false;
            }

            bool damageApplied = false;
            if (aiComp.PunishmentDamage != null)
            {
                var damageResult = _damageable.TryChangeDamage(targetPlayer.Value, aiComp.PunishmentDamage, ignoreResistances: true);
                damageApplied = damageResult != null;
            }
            else
            {
                _sawmill.Warning($"NPC {ToPrettyString(npc)} tried to punish, but no PunishmentDamage is defined in its AiNpcComponent.");
            }

            if (aiComp.PunishmentSound != null)
            {
                _audio.PlayPvs(aiComp.PunishmentSound, Transform(npc).Coordinates);
            }

            return damageApplied;
        }

    }
}