namespace Content.Server._Stalker.GuardFactionController;

[RegisterComponent]
public sealed partial class GuardFactionControllerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ToggleOnAction = default!;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ToggleOffAction = default!;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string TargetFaction = "Stalker";
}
