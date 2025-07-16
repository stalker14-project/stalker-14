using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker.Anomaly;

[RegisterComponent]
public sealed partial class STAnomalyTipsComponent : Component
{
    [DataField]
    public Vector2 Offset = new(-0.5f, -0.5f);

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Stalker/Interface/Overlays/anomaly_tips.rsi"), "unknown");

    [DataField]
    public float Visibility = 0.5f;
}
