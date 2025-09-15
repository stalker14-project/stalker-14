using Content.Shared._Stalker_EN.FactionTeleport;
using Content.Shared._Stalker.Bands;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
// Added: EntityQueryEnumerator lives here.
// Added: TransformComponent lives here.
// Needed for TransformSystem

namespace Content.Server._Stalker_EN.FactionTeleport;

/// <summary>
/// Server side: populates UI with valid destinations and handles teleport requests.
/// </summary>
public sealed class FactionTeleportSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FactionTeleportUserInterfaceComponent, BoundUIOpenedEvent>(HandleBoundUiOpen);

        // Handle client requests to teleport.
        SubscribeLocalEvent<FactionTeleportUserInterfaceComponent, FactionTeleportRequestTeleportMessage>(OnRequestTeleport);
    }

    private void HandleBoundUiOpen(Entity<FactionTeleportUserInterfaceComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent, args.Actor);
    }

    private void UpdateUiState(Entity<FactionTeleportUserInterfaceComponent> ent, EntityUid? actor)
    {
        if (actor == null)
        {
            _ui.SetUiState(ent.Owner, FactionTeleportUiKey.Key, new FactionTeleportUiState(new()));
            return;
        }

        var bandName = GetActorBandName(actor.Value);
        var list = new List<FactionTeleportDestination>();

        var query = EntityQueryEnumerator<FactionTeleportComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!IsAllowed(comp, bandName))
                continue;

            var name = MetaData(uid).EntityName;
            if (string.IsNullOrWhiteSpace(name))
                name = "Unknown";

            var netEnt = GetNetEntity(uid);
            if (netEnt == default)
                continue;

            // Include the color from the component so the client doesn't have to wait for local replication.
            list.Add(new FactionTeleportDestination(netEnt, name, comp.NameColor));
        }

        _ui.SetUiState(ent.Owner, FactionTeleportUiKey.Key, new FactionTeleportUiState(list));
    }

    private string? GetActorBandName(EntityUid actor)
    {
        if (!TryComp(actor, out BandsComponent? bands))
            return null;

        return bands.BandProto.ToString();
    }

    private static bool IsAllowed(FactionTeleportComponent comp, string? bandName)
    {
        const string defaultFaction = "STStalkerBand";

        // If explicitly marked as Everyone, allow regardless of band.
        foreach (var faction in comp.Factions)
        {
            if (string.Equals(faction, "Everyone", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check once if the destination includes the default faction.
        var hasDefault = false;
        foreach (var faction in comp.Factions)
        {
            if (string.Equals(faction, defaultFaction, StringComparison.OrdinalIgnoreCase))
            {
                hasDefault = true;
                break;
            }
        }

        if (hasDefault)
        {
            // Default: allow all except empty band and two disallowed bands.
            if (string.IsNullOrEmpty(bandName))
                return false;

            if (string.Equals(bandName, "STBanditsBand", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(bandName, "STRenegatsBand", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        // No default: require explicit match with player's band.
        if (string.IsNullOrEmpty(bandName))
            return false;

        foreach (var f in comp.Factions)
        {
            if (string.Equals(f, bandName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }


    private void OnRequestTeleport(Entity<FactionTeleportUserInterfaceComponent> ent, ref FactionTeleportRequestTeleportMessage msg)
    {
        // Actor is the entity that sent the BUI message (player's controlled entity).
        var user = msg.Actor;

        if (!TryGetEntity(msg.Destination, out var dest))
            return;

        if (!TryComp(dest, out FactionTeleportComponent? tp))
            return;

        var band = GetActorBandName(user);
        if (!IsAllowed(tp, band))
            return;

        if (!TryComp(dest, out TransformComponent? destXform))
            return;

        // Ensure the user has a transform before moving them.
        if (!TryComp(user, out TransformComponent? _))
            return;

        _xform.SetCoordinates(user, destXform.Coordinates);

    }
}
