using Content.Server.Administration;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker.WarZone;
using Content.Shared.Administration;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.WarZone;


[AdminCommand(AdminFlags.Admin)]
public sealed class STWarZoneChangeOwnerCommand : IConsoleCommand
{
    //[Dependency] SharedWarZoneSystem _sharedWarZone = default!;
    [Dependency] IPrototypeManager _prototype = default!;

    public string Command => "st_warzone_changeowner";
    public string Description => Loc.GetString("st-cmd-warzone_changeowner-desc");
    public string Help => Loc.GetString("st-cmd-warzone_changeowner-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("st-cmd-warzone_changeowner-error-args"));
            return;
        }


        shell.WriteLine(Loc.GetString("st-cmd-warzone_changeowner-succeed",
            ("zone", "Zone"),
            ("band", "Band")));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<STWarZonePrototype>(proto: _prototype),
                Loc.GetString("st-cmd-warzone_changeowner-arg-zone"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<STBandPrototype>(proto: _prototype),
                Loc.GetString("st-cmd-warzone_changeowner-arg-band"));
        }

        return CompletionResult.Empty;
    }
}
