using Robust.Client.Graphics;

namespace Content.Client._Stalker.Icon;

[RegisterComponent]
public sealed partial class STIconComponent : Component
{
    [DataField]
    public string Sprite;

    [DataField]
    public List<string> Layers = new();

    public Dictionary<object, RSI?> CachedTexture = new();
}
