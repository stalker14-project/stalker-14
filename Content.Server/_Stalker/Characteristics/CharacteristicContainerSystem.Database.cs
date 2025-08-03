using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Stalker.Characteristics;
using Robust.Shared.Asynchronous;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Characteristics;

public sealed partial class CharacteristicContainerSystem : SharedCharacteristicContainerSystem
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

    private readonly Dictionary<(string, CharacteristicType), DateTime?> _lastTrainedByCharacteristic = [];

    private void InitializeDatabase()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerBeforeSpawn);
    }

    private async void OnPlayerBeforeSpawn(PlayerAttachedEvent args)
    {
        await LoadCharacteristicsAsync(args.Player);
    }

    private async Task LoadCharacteristicsAsync(ICommonSession player)
    {
        if (!TryComp(player.AttachedEntity, out CharacteristicContainerComponent? container))
            return;

        foreach (var (_, characteristic) in container.Characteristics)
        {
            var stats = await _dbManager.GetStalkerStatAsync(player.Name, characteristic.Type);
            if (stats is not null)
            {
                _lastTrainedByCharacteristic[(player.Name, characteristic.Type)] = stats.LastTrained;
                TryInitCharacteristic((player.AttachedEntity.Value, container), characteristic.Type, (int) stats.Value);
            }
        }
    }

    private async Task SaveCharacteristicAsync(Entity<CharacteristicContainerComponent> entity,
        CharacteristicType type,
        int level,
        DateTime? trainTime = null)
    {
        var login = GetLogin(entity);

        if (login is null)
            return;

        var characteristic = entity.Comp.Characteristics[type];

        await _dbManager.SetStalkerStatsAsync(login, characteristic.Type, level, trainTime);
        if (trainTime == null)
            return;

        _lastTrainedByCharacteristic[(login, type)] = trainTime;
    }

    public async Task<bool> IsTrainTimeConditionMet(Entity<CharacteristicContainerComponent> entity, CharacteristicType type)
    {
        var login = GetLogin(entity);
        if (login is null)
            return false;

        var characteristic = entity.Comp.Characteristics[type];

        var whenLastTrained = DateTime.MinValue.ToUniversalTime();

        if (_lastTrainedByCharacteristic.TryGetValue((login, type), out var lastTrained) && lastTrained.HasValue)
        {
            whenLastTrained = lastTrained.Value;
        }

        var date = DateOnly.FromDateTime(whenLastTrained);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return today > date;
    }

    public string? GetLogin(Entity<CharacteristicContainerComponent> entity)
    {
        if (!TryComp<ActorComponent>(entity.Comp.Owner, out var actor))
            return null;

        return actor.PlayerSession.Name;
    }
}
