using Robust.Shared.Audio;

namespace Content.Server._Stalker.PdaMessenger;

[RegisterComponent]
public sealed partial class PdaMessengerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SendTimeCooldown = TimeSpan.FromSeconds(5);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextSendTime;

    [DataField]
    public SoundSpecifier PDASound = new SoundPathSpecifier("/Audio/_Stalker/PDA/PDA.ogg");

}
