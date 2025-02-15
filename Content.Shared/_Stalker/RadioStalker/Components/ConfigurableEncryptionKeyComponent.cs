
namespace Content.Shared._Stalker.RadioStalker.Components;


[RegisterComponent]
public sealed partial class ConfigurableEncryptionKeyComponent : Component
{
    [DataField("channels")]
    public string? Channel;
}
