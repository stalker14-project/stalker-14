using Robust.Shared.Map;

namespace Content.Server.Movement.Components;

[RegisterComponent]
public sealed partial class LagCompensationComponent : Component
{
    [ViewVariables]
    public readonly Queue<ValueTuple<TimeSpan, EntityCoordinates, Angle>> Positions = new();

    // Tracks the last time this entity moved (for optimization)
    public TimeSpan LastMoveTime = TimeSpan.Zero;
}
