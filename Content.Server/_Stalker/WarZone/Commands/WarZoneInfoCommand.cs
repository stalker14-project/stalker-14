using System;
using System.Text;
using Content.Server._Stalker.WarZone;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Console;
using Content.Server.Administration;
using Robust.Shared.Prototypes;
using Content.Shared._Stalker.Bands;
using Content.Shared.NPC.Prototypes;

namespace Content.Server._Stalker.WarZone.Commands;

[AnyCommand]
public sealed class WarZoneInfoCommand : IConsoleCommand
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    public string Command => "warzoneinfo";
    public string Description => "Lists warzones, their owners, cooldowns, and points for bands and factions.";
    public string Help => "Usage: warzoneinfo";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var warZoneSystem = _entityManager.System<WarZoneSystem>();
        var sb = new StringBuilder();

        sb.AppendLine("=== Warzones ===");
        foreach (var (uid, wzComp) in warZoneSystem.GetAllWarZones())
        {
            var zoneId = wzComp.ZoneProto;
            var owner = "None";
            if (!string.IsNullOrEmpty(wzComp.DefendingBandProtoId))
                owner = $"Band {wzComp.DefendingBandProtoId}";
            else if (!string.IsNullOrEmpty(wzComp.DefendingFactionProtoId))
                owner = $"Faction {wzComp.DefendingFactionProtoId}";

            var cooldown = wzComp.CooldownEndTime.HasValue
                ? (wzComp.CooldownEndTime.Value - warZoneSystem.CurrentTime).TotalSeconds
                : 0;

            if (cooldown < 0)
                cooldown = 0;

            // Resolve attacker name
            string attacker = "None";
            if (!string.IsNullOrEmpty(wzComp.CurrentAttackerBandProtoId))
            {
                if (_prototypeManager.TryIndex<STBandPrototype>(wzComp.CurrentAttackerBandProtoId, out var attackerBandProto))
                    attacker = $"Band {attackerBandProto.Name}";
                else
                    attacker = $"Band {wzComp.CurrentAttackerBandProtoId}";
            }
            else if (!string.IsNullOrEmpty(wzComp.CurrentAttackerFactionProtoId))
            {
                if (_prototypeManager.TryIndex<NpcFactionPrototype>(wzComp.CurrentAttackerFactionProtoId, out var attackerFactionProto))
                    attacker = $"Faction {attackerFactionProto.ID}";
                else
                    attacker = $"Faction {wzComp.CurrentAttackerFactionProtoId}";
            }

            // Resolve defender name (more explicit than 'owner')
            string defender = "None";
            if (!string.IsNullOrEmpty(wzComp.DefendingBandProtoId))
            {
                if (_prototypeManager.TryIndex<STBandPrototype>(wzComp.DefendingBandProtoId, out var defenderBandProto))
                    defender = $"Band {defenderBandProto.Name}";
                else
                    defender = $"Band {wzComp.DefendingBandProtoId}";
            }
            else if (!string.IsNullOrEmpty(wzComp.DefendingFactionProtoId))
            {
                if (_prototypeManager.TryIndex<NpcFactionPrototype>(wzComp.DefendingFactionProtoId, out var defenderFactionProto))
                    defender = $"Faction {defenderFactionProto.ID}";
                else
                    defender = $"Faction {wzComp.DefendingFactionProtoId}";
            }

            // Capture progress (assuming property exists)
            float progress = 0f;
            try
            {
                progress = wzComp.CaptureProgress;
            }
            catch
            {
                // If property does not exist, default to 0
            }

            sb.AppendLine($"Zone: {zoneId}, Owner: {owner}, Cooldown: {cooldown:F0}s, Attacker: {attacker}, Defender: {defender}, Progress: {progress * 100:F1}%, EntityUid: {uid}");
        }

        sb.AppendLine("\n=== Band Points ===");
        foreach (var kvp in warZoneSystem.BandPoints)
        {
            string name = "Unknown";
            if (_prototypeManager.TryIndex<Content.Shared._Stalker.Bands.STBandPrototype>(kvp.Key, out var bandProto))
                name = bandProto.Name;

            sb.AppendLine($"Band Proto ID: {kvp.Key}, Name: {name}, Points: {kvp.Value}");
        }

        sb.AppendLine("\n=== Faction Points ===");
        foreach (var kvp in warZoneSystem.FactionPoints)
        {
            string name = "Unknown";
            if (_prototypeManager.TryIndex<Content.Shared.NPC.Prototypes.NpcFactionPrototype>(kvp.Key, out var factionProto))
                name = factionProto.ID;

            sb.AppendLine($"Faction Proto ID: {kvp.Key}, Name: {name}, Points: {kvp.Value}");
        }

        shell.WriteLine(sb.ToString());
    }
}