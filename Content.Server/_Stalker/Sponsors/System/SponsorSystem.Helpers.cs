using Robust.Shared.Network;

namespace Content.Server._Stalker.Sponsors.System;

public sealed partial class SponsorSystem
{
    public float GetRepositoryWeight(NetUserId userId, float defaultWeight)
    {
        var totalWeight = defaultWeight;

        if (!_sponsors.TryGetInfo(userId, out var info))
            return totalWeight;

        if (info.SponsorProtoId is not null)
        {
            var index = _prototype.Index(info.SponsorProtoId.Value);
            totalWeight += index.RepositoryWeight;
        }

        if (info.Contributor && _sponsors.ContributorPrototype is not null)
        {
            totalWeight += _sponsors.ContributorPrototype.RepositoryWeight;
        }

        return totalWeight;
    }
}
