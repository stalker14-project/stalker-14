using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server._Stalker.ScreenGrab;
using Robust.Server.Player;

namespace Content.Server._Stalker.ScreenGrab;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ScreenGrabCommand : IConsoleCommand
{
    public string Command => "spy";
    public string Description => "command-spy-desc";
    public string Help => "command-spy-help";

    public void Execute(IConsoleShell shell, string argstr, string[] args)
    {
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var screengrabSystem = EntitySystem.Get<ScreengrabSystem>();

        if (args.Length == 2)
        {


            var username = args[0];
            var i = int.Parse(args[1]);
            if (!playerManager.TryGetSessionByUsername(args[0], out var data))
                return;

            screengrabSystem.SendScreengrabRequest(data, i);
            shell.WriteLine($"Send screengrab event to {username}.");
        }
    }
}
