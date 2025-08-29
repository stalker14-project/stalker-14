namespace Content.Server._Stalker.GuardFactionController;

[RegisterComponent]
public sealed partial class GuardFactionResistantComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "Stalker";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimeBeforeRemove = TimeSpan.FromSeconds(120);
}
