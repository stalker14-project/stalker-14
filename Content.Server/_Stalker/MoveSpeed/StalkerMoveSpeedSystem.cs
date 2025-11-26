using Content.Shared._Stalker.MoveSpeed;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;

namespace Content.Server._Stalker.MoveSpeed;

public sealed class StalkerMoveSpeedSystem : StalkerMoveSpeedSystemShared
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MovementSpeedModifierComponent, ComponentInit>(InstallComponentSpeed);
        SubscribeAllEvent<StalkerMSSetBonusWalkEvent>(OnStalkerMSSetBonusWalkEvent);
        SubscribeAllEvent<StalkerMSSetBonusSprintEvent>(OnStalkerMSSetBonusSprintEvent);
    }

    private void OnStalkerMSSetBonusSprintEvent(StalkerMSSetBonusSprintEvent msg, EntitySessionEventArgs args)
    {
        if (!TryComp(msg.Entity, out StalkerMoveSpeedComponent? moveSpeedComponent) ||
            args.SenderSession.AttachedEntity == null)
            return;

        SetBonusSpeedSprint(args.SenderSession.AttachedEntity.Value, msg.NameBonus,msg.ValueBonus,moveSpeedComponent);
    }

    private void OnStalkerMSSetBonusWalkEvent(StalkerMSSetBonusWalkEvent msg, EntitySessionEventArgs args)
    {
        if (!TryComp(msg.Entity, out StalkerMoveSpeedComponent? moveSpeedComponent) ||
            args.SenderSession.AttachedEntity == null)
            return;

        SetBonusSpeedWalk(args.SenderSession.AttachedEntity.Value, msg.NameBonus,msg.ValueBonus,moveSpeedComponent);
    }


    private void InstallComponentSpeed(EntityUid uid, MovementSpeedModifierComponent component, ComponentInit args)
    {
        var stalkerSpeedComp = AddComp<StalkerMoveSpeedComponent>(uid);
        stalkerSpeedComp.StartWalkSpeed = component._baseWalkSpeedVVpublic;
        stalkerSpeedComp.StartSprintSpeed = component._baseSprintSpeedVVpublic;
    }

    public void SetBonusSpeedWalk(EntityUid uid, string nameBonus, float valueBonus,StalkerMoveSpeedComponent stalkerSpeedComp)
    {
        stalkerSpeedComp.BonusSpeedWalkProcent[nameBonus] = valueBonus;
        CalculateSpeedByFormula(stalkerSpeedComp);
        SyncWalkSpeed((uid, stalkerSpeedComp));
    }

    public void SetBonusSpeedSprint(EntityUid uid, string nameBonus,float valueBonus,StalkerMoveSpeedComponent stalkerSpeedComp)
    {
        stalkerSpeedComp.BonusSpeedSprintProcent[nameBonus] = valueBonus;
        CalculateSpeedByFormula(stalkerSpeedComp);
        SyncSprintSpeed((uid, stalkerSpeedComp));
    }

    public void SyncWalkSpeed(Entity<StalkerMoveSpeedComponent> stalkerSpeedComp)
    {
        if (!TryComp<MovementSpeedModifierComponent>(stalkerSpeedComp.Owner, out var movementSpeed))
            return;
        movementSpeed._baseWalkSpeedVVpublic=stalkerSpeedComp.Comp.SumBonusSpeedWalk;
    }

    public void SyncSprintSpeed(Entity<StalkerMoveSpeedComponent> stalkerSpeedComp)
    {
        if (!TryComp<MovementSpeedModifierComponent>(stalkerSpeedComp.Owner, out var movementSpeed))
            return;
        movementSpeed._baseSprintSpeedVVpublic=stalkerSpeedComp.Comp.SumBonusSpeedSprint;
    }

    public void CalculateSpeedByFormula(StalkerMoveSpeedComponent stalkerSpeedComp)
    {
        var sumProcentWalk = 0f;
        foreach (var keyValuePair in stalkerSpeedComp.BonusSpeedWalkProcent)
        {
            sumProcentWalk += keyValuePair.Value;
        }
        stalkerSpeedComp.SumBonusSpeedWalk = stalkerSpeedComp.StartWalkSpeed + stalkerSpeedComp.StartWalkSpeed / 100f * sumProcentWalk;

        var sumProcentSprint = 0f;
        foreach (var keyValuePair in stalkerSpeedComp.BonusSpeedSprintProcent)
        {
            sumProcentSprint += keyValuePair.Value;
        }
        stalkerSpeedComp.SumBonusSpeedSprint = stalkerSpeedComp.StartSprintSpeed + stalkerSpeedComp.StartSprintSpeed / 100f * sumProcentSprint;
    }
}
