using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared._Stalker.ScreenGrabEvent;

namespace Content.Server._Stalker.ScreenGrab;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ScreenGrabCommand : IConsoleCommand
{
    public string Command => "screengrab";
    public string Description => Loc.GetString("screengrab-command-description");
    public string Help => Loc.GetString("screengrab-command-help-text");

    public void Execute(IConsoleShell shell, string argstr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.EntityNetManager?.SendSystemNetworkMessage(new ScreengrabRequestEvent(), true);
    }
}
