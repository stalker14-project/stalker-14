using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;

namespace Content.Server._Stalker.Sponsors.System;

public sealed partial class SponsorSystem
{
    private ISawmill _sawmill = default!;

    private void InitializeSpecies()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        _sawmill = Logger.GetSawmill("sponsor.system");
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!_sponsorsEnabled)
            return;

        var player = ev.Player.UserId;
        var species = ev.Profile.Species;
        var speciesIndex = _prototype.Index(species);

        if (!speciesIndex.IsSponsor)
            return;

        if (!_sponsors.TryGetInfo(player, out var info) || info.SponsorProtoId is null)
        {
            _sawmill.Warning($"Player tried to join as {ev.Profile.Species}, but he is not sponsor");
            ev.Profile.Species = SharedHumanoidAppearanceSystem.DefaultSpecies;
            return;
        }

        var allowed = _sponsors.SponsorSpecies
            .Where(p => p.SponsorIds.Contains(info.SponsorProtoId.Value))
            .Select(p => p.SpeciesId)
            .ToList();

        if (allowed.Contains(species))
            return;

        _sawmill.Warning($"Player tried to join as {ev.Profile.Species}, but its not in his allowed species");
        ev.Profile.Species = SharedHumanoidAppearanceSystem.DefaultSpecies;
    }
}
