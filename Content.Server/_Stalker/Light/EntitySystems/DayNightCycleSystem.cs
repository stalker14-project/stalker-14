// author: https://github.com/DrakorissVere
// license: MIT
using Content.Server.Light.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

/// <summary>
///     Allows to change MapLight color dynamically over time, imitating a day-night cycle by interpolating between color values.
///     In order for this to work you need MapLightComponent and DayNightCycleComponent on your map.
/// </summary>
public sealed class DayNightCycleSystem : EntitySystem
{
    private const int UpdateInterval = 30;
    private int _accumulatedTicks = 0;

    public override void Update(float frameTime)
    {
        _accumulatedTicks += 1;
        if (_accumulatedTicks < UpdateInterval)
            return;

        _accumulatedTicks = 0;

        var query = EntityQueryEnumerator<DayNightCycleComponent, MapLightComponent>();
        while (query.MoveNext(out var uid, out var cycle, out var light))
        {
            cycle.CurrentTime += 1f;
            if (cycle.CurrentTime > cycle.CycleDuration)
                cycle.CurrentTime = 0f;

            UpdateColor(uid, cycle, light);
        }
    }

    private void UpdateColor(EntityUid uid, DayNightCycleComponent cycle, MapLightComponent light)
    {
        float normalizedTime = cycle.CurrentTime / cycle.CycleDuration;

        Color currentColor;

        // Overall here's ~30 minutes for day and ~10 minutes for night
        // TODO: make all editable through VV
        if (normalizedTime < 0.15f)
        {
            currentColor = Color.InterpolateBetween(cycle.Night, cycle.Dawn, normalizedTime / 0.15f);
        }
        else if (normalizedTime < 0.25f)
        {
            currentColor = Color.InterpolateBetween(cycle.Dawn, cycle.Day, (normalizedTime - 0.15f) / 0.1f);
        }
        else if (normalizedTime < 0.55f)
        {
            currentColor = cycle.Day;
        }
        else if (normalizedTime < 0.65f)
        {
            currentColor = Color.InterpolateBetween(cycle.Day, cycle.Dusk, (normalizedTime - 0.55f) / 0.1f);
        }
        else if (normalizedTime < 0.8f)
        {
            currentColor = Color.InterpolateBetween(cycle.Dusk, cycle.Night, (normalizedTime - 0.65f) / 0.15f);
        }
        else
        {
            currentColor = cycle.Night;
        }

        // For smoothest transition
        cycle.ColorBuffer.Enqueue(currentColor);

        if (cycle.ColorBuffer.Count > cycle.AverageColors)
        {
            cycle.ColorBuffer.Dequeue();
        }

        Color average = new Color(0, 0, 0, 0);
        foreach (var color in cycle.ColorBuffer)
        {
            average.R += color.R;
            average.G += color.G;
            average.B += color.B;
            average.A += color.A;
        }

        average.R /= cycle.ColorBuffer.Count;
        average.B /= cycle.ColorBuffer.Count;
        average.G /= cycle.ColorBuffer.Count;
        average.A /= cycle.ColorBuffer.Count;

        light.AmbientLightColor = average;
        Dirty(uid, light);
    }

}
