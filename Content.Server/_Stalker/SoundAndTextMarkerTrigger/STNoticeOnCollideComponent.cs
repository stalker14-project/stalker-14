using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Server._Stalker.NoticeOnCollide;

/// <summary>
/// Can use <see cref="AccessReaderComponent"/> for checking who can interract
/// </summary>
[RegisterComponent]
public sealed partial class STNoticeOnCollideComponent : Component
{
    /// <summary>
    /// Played on beginning of collide
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SoundEnter;

    /// <summary>
    /// Popup shown when collide
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? Text = null;

    /// <summary>
    /// Cooldown between uses in seconds at start and end collide at all
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(0);

    public TimeSpan LastUsed = new TimeSpan();

    /// <summary>
    /// Chance of activation
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Chance = 1;

    /// <summary>
    /// Played on ending of collide
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SoundExit;
}