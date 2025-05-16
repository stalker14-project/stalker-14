using Content.Shared.Tools.Components;
using Robust.Shared.Audio;

namespace Content.Server._DZ.FarGunshot;

[RegisterComponent]
public sealed partial class FarGunshotComponent : Component
{
    /// <summary>
    /// The start max range of farsound, non modified
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 260f;

    /// <summary>
    /// The base sound to use when the gun is fired from far dist.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/_DZ/Effects/FarGunshots/rifle1.ogg");

    /// <summary>
    /// How much should silencer decrease range (by miltiply)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SilencerDecrease = 1f;

    // TODO: silencer impl
}
