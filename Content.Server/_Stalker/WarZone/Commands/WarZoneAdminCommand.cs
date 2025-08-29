using System;
using Content.Server._Stalker.WarZone;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared._Stalker.WarZone;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Console;
using Content.Server.Administration;

namespace Content.Server._Stalker.WarZone.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class WarZoneAdminCommand : IConsoleCommand
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public string Command => "st_warzoneadmin";
    public string Description => "Admin command to modify warzone points and ownership.";
    public string Help => "Usage:\n" +
                          "st_warzoneadmin setpoints band <bandProtoId> <points>\n" +
                          "st_warzoneadmin setpoints faction <factionProtoId> <points>\n" +
                          "st_warzoneadmin setowner <zoneProtoId> band <bandProtoId>\n" +
                          "st_warzoneadmin setowner <zoneProtoId> faction <factionProtoId>\n" +
                          "st_warzoneadmin clearowner <zoneProtoId>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var warZoneSystem = _entityManager.System<WarZoneSystem>();

        if (args[0] == "setpoints")
        {
            if (args.Length != 4)
            {
                shell.WriteLine(Help);
                return;
            }

            var target = args[1];
            var protoId = args[2];

            if (!int.TryParse(args[3], out var points))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            if (target == "band")
            {
                warZoneSystem.SetBandPoints(protoId, points);
                await _dbManager.SetStalkerBandAsync(protoId, points);
                shell.WriteLine($"Set band '{protoId}' points to {points}");
            }
            else if (target == "faction")
            {
                warZoneSystem.SetFactionPoints(protoId, points);
                await _dbManager.SetStalkerFactionAsync(protoId, points);
                shell.WriteLine($"Set faction '{protoId}' points to {points}");
            }
            else
            {
                shell.WriteLine(Help);
            }

            return;
        }


        if (args[0] == "setowner")
        {
            if (args.Length != 4)
            {
                shell.WriteLine(Help);
                return;
            }

            var zoneProtoId = args[1];
            var target = args[2];

            var protoId = args[3];

            WarZoneComponent? foundComp = null;
            foreach (var (_, wzComp) in warZoneSystem.GetAllWarZones())
            {
                if (wzComp.ZoneProto == zoneProtoId)
                {
                    foundComp = wzComp;
                    break;
                }
            }

            if (foundComp == null)
            {
                shell.WriteLine($"Warzone with proto id {zoneProtoId} not found.");
                return;
            }

            ProtoId<Content.Shared._Stalker.Bands.STBandPrototype>? bandProtoId = null;
            ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>? factionProtoId = null;

            if (target == "band")
            {
                foundComp.DefendingBandProtoId = protoId;
                foundComp.DefendingFactionProtoId = null;
                bandProtoId = protoId;
            }
            else if (target == "faction")
            {
                foundComp.DefendingBandProtoId = null;
                foundComp.DefendingFactionProtoId = protoId;
                factionProtoId = protoId;
            }
            else
            {
                shell.WriteLine(Help);
                return;
            }

            await _dbManager.SetStalkerZoneOwnershipAsync(zoneProtoId, bandProtoId, factionProtoId);
            shell.WriteLine($"Set owner of warzone {zoneProtoId} to {target} '{protoId}'");
            return;
        }

        if (args[0] == "clearowner")
        {
            if (args.Length != 2)
            {
                shell.WriteLine(Help);
                return;
            }

            var zoneProtoId = args[1];

            WarZoneComponent? foundComp = null;
            foreach (var (_, wzComp) in warZoneSystem.GetAllWarZones())
            {
                if (wzComp.ZoneProto == zoneProtoId)
                {
                    foundComp = wzComp;
                    break;
                }
            }

            if (foundComp == null)
            {
                shell.WriteLine($"Warzone with proto id {zoneProtoId} not found.");
                return;
            }

            foundComp.DefendingBandProtoId = null;
            foundComp.DefendingFactionProtoId = null;
            foundComp.CooldownEndTime = null;

            await _dbManager.ClearStalkerZoneOwnershipAsync(zoneProtoId);

            shell.WriteLine($"Cleared ownership of warzone {zoneProtoId}");
            return;
        }

        shell.WriteLine(Help);
    }
}