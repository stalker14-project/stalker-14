using Content.Shared._Stalker.Anomaly.Prototypes;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Stalker.Anomaly.Generation.Systems;

public sealed partial class STAnomalyGeneratorSystem
{
    [Dependency] private readonly IConsoleHost _consoleHost = default!;

    private void InitializeCommands()
    {
        _consoleHost.RegisterCommand("st_anomaly_generation_get_data_uid",
            Loc.GetString("st-anomaly-generation-get-data-uid"),
            "st_anomaly_generation_get_active",
            StartGenerationGetDataUidCallback);

        _consoleHost.RegisterCommand("st_anomaly_generation_get_active",
        Loc.GetString("st-anomaly-generation-get-active"),
        "st_anomaly_generation_get_active",
            StartGenerationGetActiveCallback);

        _consoleHost.RegisterCommand("st_anomaly_generation_start",
            Loc.GetString("st-anomaly-generation-start"),
            "st_anomaly_generation_start <mapId> <protoId>",
            StartGenerationCallback,
            StartGenerationCallbackHelper);

        _consoleHost.RegisterCommand("st_anomaly_generation_clear",
            Loc.GetString("st-anomaly-generation-clear"),
            "st_anomaly_generation_clear <mapId>",
            StartGenerationClearCallback,
            StartGenerationClearCallbackHelper);
    }

    [AdminCommand(AdminFlags.MassBan)]
    private void StartGenerationGetDataUidCallback(IConsoleShell shell, string argstr, string[] args)
    {
        shell.WriteLine($"Data entity: {ToPrettyString(Data.Owner)}");
    }

    [AdminCommand(AdminFlags.MassBan)]
    private void StartGenerationGetActiveCallback(IConsoleShell shell, string argstr, string[] args)
    {
        var result = string.Empty;
        foreach (var (job, _) in _jobs)
        {
            result += $"Job {job.AsTask.Id} for {job.Options.MapId}\r\n";
        }

        shell.WriteLine(result);
    }

    [AdminCommand(AdminFlags.MassBan)]
    private void StartGenerationCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-argument-count-must-be", ("value", 2)));
            return;
        }

        if (!int.TryParse(args[0], out var mapIndex))
        {
            shell.WriteError(Loc.GetString("shell-invalid-int", ("value", args[0])));
            return;
        }

        var mapId = new MapId(mapIndex);
        if (!_map.MapExists(mapId))
        {
            shell.WriteError($"Map {mapId} doesn't exist!");
            return;
        }

        if (!_prototype.TryIndex<STAnomalyGenerationOptionsPrototype>(args[1], out var proto))
        {
            shell.WriteError(Loc.GetString("shell-invalid-prototype", ("value", args[1])));
            return;
        }

        var task = StartGeneration(mapId, proto.Options);
        shell.WriteLine($"Create generation {task.Id}");
    }

    private CompletionResult StartGenerationCallbackHelper(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(CompletionHelper.MapIds()),
            2 => CompletionResult.FromOptions(CompletionHelper.PrototypeIDs<STAnomalyGenerationOptionsPrototype>()),
            _ => CompletionResult.Empty
        };
    }

    [AdminCommand(AdminFlags.MassBan)]
    private void StartGenerationClearCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-argument-count-must-be", ("value", 1)));
            return;
        }


        if (!int.TryParse(args[0], out var mapIndex))
        {
            shell.WriteError(Loc.GetString("shell-invalid-int", ("value", args[0])));
            return;
        }

        var mapId = new MapId(mapIndex);
        if (!_map.MapExists(mapId))
        {
            shell.WriteError($"Map {mapId} doesn't exist!");
            return;
        }

        ClearGeneration(mapId);
    }

    private CompletionResult StartGenerationClearCallbackHelper(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(CompletionHelper.MapIds()),
            _ => CompletionResult.Empty
        };
    }
}
