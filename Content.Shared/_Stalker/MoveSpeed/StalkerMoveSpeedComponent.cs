namespace Content.Server._Stalker.MoveSpeed;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class StalkerMoveSpeedComponent : Component
{
    public bool SyncedWalkSpeed = true;

    public bool SyncedSprintSpeed = true;

    [ViewVariables(VVAccess.ReadWrite)]
    public float StartWalkSpeed = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float StartSprintSpeed = 0f;

    public Dictionary<string, float> BonusSpeedWalkProcent = new(0);
    public Dictionary<string, float> BonusSpeedSprintProcent = new(0);

    [ViewVariables(VVAccess.ReadWrite)]
    public float SumBonusSpeedWalk = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SumBonusSpeedSprint = 0f;
}
