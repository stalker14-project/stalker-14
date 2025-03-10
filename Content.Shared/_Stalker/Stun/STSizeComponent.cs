using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Stun;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSizeComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public STSizes Size = STSizes.Humanoid;
}


[Serializable, NetSerializable]
public enum STSizes : byte
{
    Small,
    Humanoid,
    VerySmallMutant,
    SmallMutant,
    Mutant,
    Big,
    Immobile
}
