using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Maths;

// Shared messages / key / state used by both client & server.
namespace Content.Shared._Stalker_EN.FactionTeleport;

[Serializable, NetSerializable]
public enum FactionTeleportUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FactionTeleportUiState(List<FactionTeleportDestination> destinations)
    : BoundUserInterfaceState
{
    public List<FactionTeleportDestination> Destinations { get; } = destinations;
}

[Serializable, NetSerializable]
public sealed class FactionTeleportDestination
{
    // Destination entity (the entity with FactionTeleportComponent).
    [ViewVariables(VVAccess.ReadOnly)]
    public NetEntity Destination { get; init; }

    // Display name in the list (from entity name).
    [ViewVariables(VVAccess.ReadOnly)]
    public string Name { get; init; }

    // Color presence flag and non-nullable color to avoid nullable serialization issues.
    [ViewVariables(VVAccess.ReadOnly)]
    public bool HasColor { get; init; }

    [ViewVariables(VVAccess.ReadOnly)]
    public Color NameColor { get; init; }

    public FactionTeleportDestination(NetEntity destination, string name, Color? nameColor = null)
    {
        Destination = destination;
        Name = name;

        if (nameColor is { } col)
        {
            HasColor = true;
            NameColor = col;
        }
        else
        {
            HasColor = false;
            NameColor = default;
        }
    }
}

[Serializable, NetSerializable]
public sealed class FactionTeleportRequestTeleportMessage(NetEntity destination)
    : BoundUserInterfaceMessage
{
    public NetEntity Destination { get; } = destination;
}
