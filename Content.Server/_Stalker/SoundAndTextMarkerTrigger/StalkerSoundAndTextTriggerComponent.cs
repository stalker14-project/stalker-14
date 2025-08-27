using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Server._Stalker.SoundAndTextTrigger;

[RegisterComponent]
public sealed partial class StalkerSoundAndTextTriggerComponent : Component
{

    [DataField]
    public bool AllowAll = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? Sound;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? Text = null;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(0);

    public TimeSpan LastUsed = new TimeSpan();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Chance = 1;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SoundExit;
}