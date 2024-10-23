// author: https://github.com/DrakorissVere
// license: MIT
using Content.Server.Light.EntitySystems;

namespace Content.Server.Light.Components;

/// <summary>
///     Holds necessary state for day-night ambient color transition.
/// </summary>
[RegisterComponent, Access(typeof(DayNightCycleSystem))]
public sealed partial class DayNightCycleComponent : Component
{
    /// <summary>
    ///     Real time value (in seconds) for 24 hours to pass.
    /// </summary>
    [DataField]
    public float CycleDuration = 2400f; // 40 minutes

    /// <summary>
    ///     Current time passed. Truncated to zero once trespassed CycleDuration value.
    /// </summary>
    [DataField]
    public float CurrentTime = 0f;

    /// <summary>
    ///     Light color for dawn.
    /// </summary>
    [DataField]
    public Color Dawn = Color.FromHex("#ffcc66");

    /// <summary>
    ///     Light color for day.
    /// </summary>
    [DataField]
    public Color Day = Color.FromHex("#e6cb8b");

    /// <summary>
    ///     Light color for dusk.
    /// </summary>
    [DataField]
    public Color Dusk = Color.FromHex("#cc4d1a");

    /// <summary>
    ///     Light color for night.
    /// </summary>
    [DataField]
    public Color Night = Color.FromHex("#010105");

    /// <summary>
    ///     Make color transition smoothest by interpolating between an average of buffer of colors.
    /// </summary>
    [DataField]
    public int AverageColors = 10;

    /// <summary>
    ///     Color buffer for making the average from it.
    /// </summary>
    public Queue<Color> ColorBuffer = new Queue<Color>();
}
