// author: https://github.com/DrakorissVere
// license: MIT
using Content.Shared.Weather;
using Robust.Shared.Prototypes;

namespace Content.Server.Weather;

/// <summary>
///     Allows weather effects randomly appear.
/// </summary>
[RegisterComponent, Access(typeof(RandomWeatherSystem))]
public sealed partial class RandomWeatherComponent : Component
{
    /// <summary>
    ///     Time when next weather effect will be started.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextWeatherStart = TimeSpan.Zero;

    /// <summary>
    ///     Weather durations is randomized but can't be lower that this value.
    /// </summary>
    [DataField]
    public int MinWeatherDuration = 3;

    /// <summary>
    ///     Weather durations is randomized but can't be higher that this value.
    /// </summary>
    [DataField]
    public int MaxWeatherDuration = 8;

    /// <summary>
    ///     Calm weather time between randomized ones, lower value.
    /// </summary>
    [DataField]
    public int MinCalmPeriodBetweenWeathers = 2;

    /// <summary>
    ///     Calm weather time between randomized ones, higher value.
    /// </summary>
    [DataField]
    public int MaxCalmPeriodBetweenWeathers = 6;

    /// <summary>
    ///     List to choose weather from. Float values is a chance and all chances must add up to 100.0f.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<WeatherPrototype>, float> AllowedWeather = new()
    {
        { new ProtoId<WeatherPrototype>("Hail"), 30.0f },
        { new ProtoId<WeatherPrototype>("Rain"), 25.0f },
        { new ProtoId<WeatherPrototype>("Storm"), 15.0f },
        { new ProtoId<WeatherPrototype>("Fallout"), 12.0f },
        { new ProtoId<WeatherPrototype>("AshfallLight"), 9.0f },
        { new ProtoId<WeatherPrototype>("Ashfall"), 6.0f },
        { new ProtoId<WeatherPrototype>("AshfallHeavy"), 3.0f },
    };
}
