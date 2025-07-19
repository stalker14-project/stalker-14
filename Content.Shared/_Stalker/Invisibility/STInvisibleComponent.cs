using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Invisibility;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STInvisibleComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Opacity = 1f;
}
