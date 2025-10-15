using System;
using Content.Server._Stalker.StalkerDB;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Commands
{
    [AdminCommand(AdminFlags.Host)]
    public sealed class ResetAllStashesCommand : IConsoleCommand
    {
        public string Command => "stalker_resetallstashes";

        public string Description => "Reset all stalker stashes in the DB and in-world to default starting items (requires confirmation).";

        public string Help => "Usage: stalker_resetallstashes confirm\nThis will set every stash record to the default starting items and reload in-world repositories.";

        public async void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1 || args[0] != "confirm")
            {
                shell.WriteLine(Help);
                return;
            }

            try
            {
                // Resolve IEntityManager at runtime in Execute to be robust when commands are created by the console host.
                var entityManager = IoCManager.Resolve<IEntityManager>();
                var system = entityManager.System<StalkerDbSystem>();
                shell.WriteLine("Reset started: resetting all stashes to default. You will be notified when complete.");

                // Start the async reset without blocking. The reset method will await DB ops and resume on the server main thread
                // (so entity operations inside ResetAllStashes run on the correct thread).
                var resetTask = system.ResetAllStashes();

                // Attach a continuation to report completion or error back to the console.
                resetTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var baseEx = t.Exception?.GetBaseException();
                        shell.WriteError($"Failed to reset all stashes: {baseEx?.Message ?? "unknown error"}");
                    }
                    else
                    {
                        shell.WriteLine("Reset complete: all stashes set to defaults and in-world repositories reloaded.");
                    }
                });
            }
            catch (Exception e)
            {
                // If we hit an exception starting the background job, report it immediately.
                shell.WriteError($"Failed to start reset task: {e.Message}");
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
                return CompletionResult.FromHint("Type 'confirm' to perform the reset.");

            return CompletionResult.Empty;
        }
    }
}
