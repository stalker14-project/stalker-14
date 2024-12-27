using System.Linq;
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class JobGroupWhitelistAddCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "jobgroupwhitelistadd";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var player = args[0].Trim();
        var groupId = args[1].Trim();

        if (!_prototypes.TryIndex<WhitelistGroupPrototype>(groupId, out var groupPrototype))
        {
            shell.WriteError(Loc.GetString("cmd-jobgroupwhitelist-group-does-not-exist", ("group", groupId)));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data != null)
        {
            var guid = data.UserId;
            foreach (var jobId in groupPrototype.Jobs)
            {
                if (!_prototypes.TryIndex<JobPrototype>(jobId, out var jobPrototype))
                {
                    shell.WriteError(Loc.GetString("cmd-jobgroupwhitelist-job-invalid", ("job", jobId)));
                    continue;
                }

                var isWhitelisted = await _db.IsJobWhitelisted(guid, new ProtoId<JobPrototype>(jobId));
                if (isWhitelisted)
                {
                    shell.WriteLine(Loc.GetString("cmd-jobgroupwhitelist-already-whitelisted",
                        ("player", player),
                        ("jobId", jobId),
                        ("jobName", jobPrototype.LocalizedName)));
                    continue;
                }

                _jobWhitelist.AddWhitelist(guid, new ProtoId<JobPrototype>(jobId));
                shell.WriteLine(Loc.GetString("cmd-jobgroupwhitelist-added",
                    ("player", player),
                    ("jobId", jobId),
                    ("jobName", jobPrototype.LocalizedName)));
            }
            return;
        }

        shell.WriteError(Loc.GetString("cmd-jobwhitelist-player-not-found", ("player", player)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-jobgroupwhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<WhitelistGroupPrototype>().Select(p => p.ID),
                Loc.GetString("cmd-jobgroupwhitelist-hint-group"));
        }

        return CompletionResult.Empty;
    }
}
