using Content.Server.Chat.Managers;
using Content.Server.Spawners.Components;
using Robust.Server;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server._Stalker.Restart;

public partial class RestartSystem : EntitySystem
{
    [Dependency] private readonly IBaseServer _server = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    private ISawmill _sawmill = default!;
    private readonly HashSet<string> _usedHomeCommand = new();


    private readonly TimeSpan _updateDelay = TimeSpan.FromSeconds(60f);
    private readonly TimeSpan _teleportDelay = TimeSpan.FromMinutes(5f);
    private TimeSpan _updateTime;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("Restart");
        InitializeCommands();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_updateTime > _timing.CurTime)
            return;

        _updateTime = _timing.CurTime + _updateDelay;

        var data = GetData();
        if (data.Comp.Time == TimeSpan.Zero)
            return;

        if (data.Comp.Time <= _timing.CurTime)
        {
            _server.Shutdown(null);
            return;
        }

        if (data.Comp.IntervalLast >= _timing.CurTime)
            return;

        var delta = data.Comp.Time - _timing.CurTime;
        _chat.DispatchServerAnnouncement($"Перезапуск сервера через: {Math.Round(delta.TotalMinutes, 1)} минут");
        if (delta < _teleportDelay)
        {
            _chat.DispatchServerAnnouncement($"Вы можете использовать команду home для быстрого возврата в Чистилище");
        }

        data.Comp.IntervalLast = _timing.CurTime + data.Comp.IntervalDelay;
    }

    public void StartRestart(TimeSpan delay)
    {
        var data = GetData();
        _chat.DispatchServerAnnouncement($"Запущен авто-рестарт сервера через: {Math.Round(delay.TotalMinutes, 1)} минут");

        data.Comp.Time = _timing.CurTime + delay;
        data.Comp.IntervalLast = _timing.CurTime + data.Comp.IntervalDelay;
        _usedHomeCommand.Clear();
        _updateTime = TimeSpan.Zero;
    }

    public void TpToPurgatory(IConsoleShell shell)
    {
        var data = GetData();

        var spawns = _entityManager.EntityQuery<SpawnPointComponent>();
        var spawn = spawns.FirstOrDefault(spawn => spawn?.Job?.Id == "Stalker");
        var session = shell.Player;
        if (spawn == null)
        {
            shell.WriteError("Нет спавнера Stalker Job Spawn на картах");
            return;
        }
        if (session?.AttachedEntity == null)
        {
            shell.WriteError("Сущность не игрок");
            return;
        }
        if (data.Comp.Time == default)
        {
            shell.WriteError("Рестарт не запланирован");
            return;
        }

        var portalAvailableTime = data.Comp.Time - _teleportDelay;
        if (portalAvailableTime >= _timing.CurTime)
        {
            var message = $"Телепортация возможно только за {_teleportDelay} до рестарта";
            shell.WriteError(message);
            _sawmill.Info($"{session.AttachedEntity.Value.Id} {session.Name} пытался телепортироваться в чистилище");
            return;
        }

        var uid = session.UserId.ToString();

        if (_usedHomeCommand.Contains(uid))
        {
            var message = $"Телепортация возможнa только один раз";
            shell.WriteError(message);
            _sawmill.Info($"{session.AttachedEntity.Value.Id} {session.Name} пытался повторно телепортироваться в чистилище");
            return;
        }

        var transformSystem = _entityManager.System<SharedTransformSystem>();
        var targetCoords = new EntityCoordinates(spawn.Owner, Vector2.Zero);

        transformSystem.SetCoordinates(session.AttachedEntity.Value, targetCoords);
        transformSystem.AttachToGridOrMap(session.AttachedEntity.Value);
        _sawmill.Info($"{session.AttachedEntity.Value.Id} {session.Name} телепортировался в Чистилище");
        shell.WriteLine("Успешная телепортация");
        _usedHomeCommand.Add(uid);
    }

    private Entity<RestartComponent> GetData()
    {
        var query = EntityQueryEnumerator<RestartComponent>();
        while (query.MoveNext(out var uid, out var restart))
        {
            return (uid, restart);
        }

        var entity = Spawn(null, MapCoordinates.Nullspace);
        var component = EnsureComp<RestartComponent>(entity);

        return (entity, component);
    }
}
