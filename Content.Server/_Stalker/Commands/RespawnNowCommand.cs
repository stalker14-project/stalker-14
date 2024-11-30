using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players;
using System.Text;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

[AnyCommand] // Corvax: Allow use to everyone
public sealed class RespawnNowCommand : IConsoleCommand
{
    public string Command => "respawnnow";
    public string Description => "Respawn you when you die";
    public string Help => "Usage: respawnnow";

    [Dependency] private readonly IConsoleHost _console = default!;
   // [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("Try respawn");

        bool RespawnComplete = false;
        bool RespawnError = true;
        string? _jobId = null;

        var player = shell.Player;
        if (player== null)
        {
            return;
        }

        var playerMgr = IoCManager.Resolve<IPlayerManager>();
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var ticker = sysMan.GetEntitySystem<GameTicker>();
        var mind = sysMan.GetEntitySystem<SharedMindSystem>();
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var jobMan = sysMan.GetEntitySystem<SharedJobSystem>();
        var _damageableSystem = sysMan.GetEntitySystem<DamageableSystem>();

        //var _damageableSystem = IoCManager.Resolve<DamageableSystem>();


        if (player.AttachedEntity == null)
            return;
        if (entityManager.TryGetComponent(player.AttachedEntity, out GhostComponent? _))
        {
            shell.WriteLine("You cannot respawnnow from ghost");
            return;
        }

        if (!playerMgr.TryGetSessionById(player.UserId, out var targetPlayer))
        {
            if (!playerMgr.TryGetPlayerData(player.UserId, out var data))
            {
                shell.WriteLine("Unknown player");
                return;
            }

            mind.WipeMind(data.ContentData()?.Mind);
            shell.WriteLine("Player is not currently online, but they will respawn if they come back online");
            return;
        }

        if (playerMgr.TryGetPlayerData(player.UserId, out var dat))
        {
            if (!jobMan.MindTryGetJob(dat.ContentData()?.Mind, out var jobPrototype))
                _jobId = null;
            else
                _jobId = jobPrototype.ID;
        }

        EntityUid PlayerEntity;
        MobStateComponent? UserCMobState = null;

        if (shell.Player != null)
        {
            PlayerEntity = (EntityUid) shell.Player.AttachedEntity!;
        }
        else
        {
            return;
        }

        if (entityManager.TryGetComponent(PlayerEntity, out MetaDataComponent? MComponent))
        {
            if (MComponent.EntityPrototype != null)
            {
                if (MComponent.EntityPrototype.ID == "HeadHuman")
                {
                    shell.WriteLine("Respawning from head...");

                    ticker.Respawn(targetPlayer);
                    var newEnt = targetPlayer.AttachedEntity;
                    if (newEnt != null)
                        _entMan.EventBus.RaiseLocalEvent(newEnt.Value, new RespawnedByCommandEvent(_entMan.GetNetEntity(PlayerEntity)));

                    RespawnComplete = true;
                    RespawnError = false;
                    var sb = new StringBuilder();
                }
            }
        }

        if (entityManager.TryGetComponent(PlayerEntity, out MobStateComponent? CMobState) && RespawnComplete == false)
        {
            UserCMobState = CMobState;
            if (CMobState.CurrentState == MobState.Critical || CMobState.CurrentState == MobState.Alive)
            {
                shell.WriteLine("We can only be reborn when you are completely dead. Your current status is \"" +
                                CMobState.CurrentState + "\"");
                RespawnError = false;
            }

            if (CMobState.CurrentState == MobState.Dead)
            {
                shell.WriteLine("Respawning...");

                ticker.Respawn(targetPlayer);
                var newEnt = targetPlayer.AttachedEntity;
                if (newEnt != null)
                    _entMan.EventBus.RaiseLocalEvent(newEnt.Value, new RespawnedByCommandEvent(_entMan.GetNetEntity(PlayerEntity)));

                RespawnComplete = true;
                RespawnError = false;
                shell.WriteLine("Respawning... OK");

                var NewPlayerEntity = (EntityUid) shell.Player.AttachedEntity!;


                if (entityManager.TryGetComponent(PlayerEntity, out DamageableComponent? DComponent))
                {

                    shell.WriteLine("DamageableComponent OK");
                    //var Damage = new DamageSpecifier(prototype, FixedPoint2.New(99));

                   // IoCManager.Resolve<DamageableSystem>().TryChangeDamage(NewPlayerEntity, Damage, true);
                   //Damage(sysMan.GetEntitySystem<DamageableSystem>(),NewPlayerEntity);


                   if (!_protoMan.TryIndex<DamageTypePrototype>("Blunt", out var prototype))
                   {
                       return;
                   }
                   var Damage = new DamageSpecifier(prototype, FixedPoint2.New(80));
                   _damageableSystem.TryChangeDamage(NewPlayerEntity, Damage, true);

                   shell.WriteLine("TryChangeDamage OK");
                }

            }
        }

        if (RespawnError)
        {
            shell.WriteLine("You not respawned, ERROR!");

            if (UserCMobState != null)
            {
                shell.WriteLine("MobState="+UserCMobState);
            }
            else
            {
                shell.WriteLine("MobState NULL");
            }

            shell.WriteLine("PlayerEntity="+PlayerEntity);
            return;
        }

        if (RespawnComplete)
        {
            shell.WriteLine("You respawned");
        }
    }

    public void Damage(DamageableSystem DS, EntityUid NewPlayerEntity)
    {
        if (!_protoMan.TryIndex<DamageTypePrototype>("Blunt", out var prototype))
        {
            return;
        }
        var Damage = new DamageSpecifier(prototype, FixedPoint2.New(80));

        DS.TryChangeDamage(NewPlayerEntity, Damage, true);

    }

}
/// <summary>
/// Raised when player types <see cref="RespawnNowCommand"/> in console on previous player entity.
/// </summary>
public sealed class RespawnedByCommandEvent : EntityEventArgs
{
    public NetEntity OldEntity;

    public RespawnedByCommandEvent(NetEntity entity)
    {
        OldEntity = entity;
    }
}
