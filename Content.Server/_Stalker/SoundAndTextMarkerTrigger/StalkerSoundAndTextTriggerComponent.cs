using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Server._Stalker.SoundAndTextTrigger;

[RegisterComponent]
public sealed partial class StalkerSoundAndTextTriggerComponent : Component
{
    public static readonly AudioParams DefaultParams = AudioParams.Default.WithVolume(-2f);

    [DataField]
    public bool AllowAll = true;

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public bool HaveText = false;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public string Text;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(0);

    public TimeSpan LastUsed = new TimeSpan();

    [DataField]
    public float Chance = 1;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundExit;
}