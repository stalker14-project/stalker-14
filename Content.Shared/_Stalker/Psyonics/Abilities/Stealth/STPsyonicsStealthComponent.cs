using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Psyonics.Abilities.Stealth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STPsyonicsStealthComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "STActionPsyonicsStealth";

    [DataField, AutoNetworkedField]
    public float Opacity = 0.05f;
}
