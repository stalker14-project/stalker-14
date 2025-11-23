using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.BandChanger;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BandChangerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string BandName = "";
}
