using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared._Stalker.ScreenGrabEvent;

namespace Content.Server._Stalker.ScreenGrab;

public sealed partial class ScreenGrabCommand : IConsoleCommand
{

    [Dependency] private readonly IEntityManager _e = default!;

    public string Command => "screengrab";
    public string Description => Loc.GetString("screengrab-command-description");
    public string Help => Loc.GetString("screengrab-command-help-text");


    [AdminCommand(AdminFlags.Host)]
    public void Execute(IConsoleShell shell, string argstr, string[] args)
    {
        string target;

        switch (args.Length)
        {
            case 1:
                target = args[0];
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-screengrab-arg-count"));
                shell.WriteLine(Help);
                return;
        }

       // RaiseNetworkEvent(new ScreengrabRequestEvent(), session);

    }
}
