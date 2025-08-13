using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors.SponsorManager;

public sealed partial class SponsorsManager
{
    private HashSet<SponsorPrototype> _sponsorPrototypes = new();
    private ContributorPrototype? _contributorPrototype;

    public ContributorPrototype? ContributorPrototype => _contributorPrototype;

    private void InitializeHelpers()
    {
        _prototype.PrototypesReloaded += ReEnumerateSponsors;
        ReEnumerateSponsors();
    }

    public HashSet<SponsorPrototype> GetSponsorPrototypesLowerThan(int priority)
    {
        return _sponsorPrototypes
            .Where(p => p.SponsorPriority < priority)
            .ToHashSet();
    }

    public bool TryGetSponsorRepositoryItems(SponsorData data, [NotNullWhen(true)] out HashSet<ProtoId<EntityPrototype>>? items)
    {
        items = null;
        if (data.SponsorProtoId is null)
            return false;

        var sponsorProtoId = data.SponsorProtoId.Value;
        if (!_prototype.TryIndex(sponsorProtoId, out var sponsorProto))
            return false;

        var lessPriorPrototypes = GetSponsorPrototypesLowerThan(sponsorProto.SponsorPriority);
        lessPriorPrototypes.Add(sponsorProto);

        var tempItems = lessPriorPrototypes.SelectMany(p => p.RepositoryItems).ToList();

        if (data.Contributor && ContributorPrototype is not null)
            tempItems.AddRange(ContributorPrototype.ContributorItems);

        items = tempItems.ToHashSet();
        return true;
    }

    public bool HavePriorityJoin(NetUserId userId)
    {
        var hasPriorityJoin = false;
        if (!TryGetInfo(userId, out var info))
            return false;

        if (info.Contributor &&
            ContributorPrototype is not null)
        {
            hasPriorityJoin = ContributorPrototype.HasPriorityJoin;
        }

        if (info.SponsorProtoId is null)
            return hasPriorityJoin;

        var index = _prototype.Index(info.SponsorProtoId.Value);
        if (index.HasPriorityJoin)
            hasPriorityJoin = true;

        return hasPriorityJoin;
    }

    private void ReEnumerateSponsors(PrototypesReloadedEventArgs? args = null)
    {
        // dry run
        if (args is null)
        {
            _sponsorPrototypes = Enumerable
                .ToHashSet<SponsorPrototype>(_prototype
                    .EnumeratePrototypes<SponsorPrototype>());

            _contributorPrototype = Enumerable
                .ToHashSet<ContributorPrototype>(_prototype
                    .EnumeratePrototypes<ContributorPrototype>())
                .FirstOrDefault();

            return;
        }

        if (!args.WasModified<SponsorPrototype>() || !args.WasModified<ContributorPrototype>())
            return;

        _sponsorPrototypes = Enumerable
            .ToHashSet<SponsorPrototype>(_prototype
                .EnumeratePrototypes<SponsorPrototype>());

        _contributorPrototype = Enumerable
            .ToHashSet<ContributorPrototype>(_prototype
                .EnumeratePrototypes<ContributorPrototype>())
            .FirstOrDefault();

        ValidateContributorPrototype();
    }

    private void ValidateContributorPrototype()
    {
        if (_contributorPrototype is not null)
            return;

        _sawmill.Warning("Not found contributor prototypes, probably you haven't created one?");
    }
}
