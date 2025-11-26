using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Player;

public sealed class StalkerPlayerSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IServerConsoleHost _consoleHost = default!;
    public override void Initialize()
    {
        base.Initialize();

        _playerMan.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.Session.Status == SessionStatus.Disconnected &&
            args.Session.AttachedEntity != null &&
            _mobState.IsCritical(args.Session.AttachedEntity.Value) &&
            TryComp<DamageableComponent>(args.Session.AttachedEntity.Value, out var damageableComponent))
        {
            _damageable.SetAllDamage(args.Session.AttachedEntity.Value, damageableComponent, 10);
            _consoleHost.ExecuteCommand(args.Session, "respawnnow");
        }
        else if (args.Session.Status == SessionStatus.Disconnected &&
                 args.Session.AttachedEntity != null &&
                 _mobState.IsDead(args.Session.AttachedEntity.Value))
        {
            _consoleHost.ExecuteCommand(args.Session, "respawnnow");
        }
    }
}
