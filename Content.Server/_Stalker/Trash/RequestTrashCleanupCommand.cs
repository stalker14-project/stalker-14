using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker.Trash;

[AdminCommand(AdminFlags.Spawn)]
public sealed class RequestTrashCleanupCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public string Command => "request_trash_cleanup";
    public string Description => "обозначает следующую уборку мусора (по умолчанию раз в 15 минут)";
    public string Help => $"usage : {Command} <timeInSeconds>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 1)
        {
            shell.WriteError(Loc.GetString("shell-need-between-arguments",("lower", 1), ("upper", 1)));
            return;
        }

        if (!int.TryParse(args[0], out var timeInSeconds))
        {
            shell.WriteError("аргумент должен быть числом");
            return;
        }
        
        var trash = _entity.System<TrashDeletingSystem>();

        try
        {
            trash.SetNextCleanupTime(timeInSeconds);
        }
        catch (Exception e)
        {
            shell.WriteError(e.ToString());
            throw;
        }
    }
}
