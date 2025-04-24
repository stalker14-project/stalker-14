using System.Collections.Generic;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker.Restart;

public partial class RestartSystem
{
    [Dependency] private readonly IConsoleHost _consoleHost = default!;

    private void InitializeCommands()
    {
        _consoleHost.RegisterCommand(
            "delayed_restart",
            "",
            "delayed_restart <seconds>>",
            StartRestartCommand);

        _consoleHost.RegisterCommand(
            "home",
            "",
            "home >",
            TpToPurgatoriumCommand);
    }

    [AdminCommand(AdminFlags.Round)]
    private void StartRestartCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-argument-count-must-be", ("value", 1)));
            return;
        }

        if (!float.TryParse(args[0], out var seconds))
        {
            shell.WriteError(Loc.GetString("shell-invalid-float", ("value", args[0])));
            return;
        }
        StartRestart(TimeSpan.FromSeconds(seconds));
    }

    [AnyCommand]
    private void TpToPurgatoriumCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (shell.Player == null)
            return;

        TpToPurgatory(shell);
    }
}
