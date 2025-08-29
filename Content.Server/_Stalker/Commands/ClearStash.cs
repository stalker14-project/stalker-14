using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.Storage;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class ClearStash : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public string Command => "clear_stash";
    public string Description => "clears the stalker's personal stashes (ALL OF THEM)";
    public string Help => $"Usage: {Command} <username>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 1)
        {
            shell.WriteError(Loc.GetString("shell-need-between-arguments",("lower", 1), ("upper", 1)));
            return;
        }

        var stalkerDb = _entity.System<StalkerStorageSystem>();

        try
        {
            stalkerDb.ClearStorages(args[0]);
            shell.WriteError(Loc.GetString("clear-stash-command-process"));
        }
        catch (Exception exception)
        {
            shell.WriteError(exception.ToString());
        }
    }
}
