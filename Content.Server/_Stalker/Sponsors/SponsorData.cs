using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors;

public sealed class SponsorData
{
    public ProtoId<SponsorPrototype>? SponsorProtoId;
    public NetUserId UserId;
    public bool IsGiven;
    public bool Contributor;

    public SponsorData(ProtoId<SponsorPrototype>? sponsorProtoId, NetUserId userId, bool isGiven, bool contributor)
    {
        SponsorProtoId = sponsorProtoId;
        UserId = userId;
        IsGiven = isGiven;
        Contributor = contributor;
    }
}
