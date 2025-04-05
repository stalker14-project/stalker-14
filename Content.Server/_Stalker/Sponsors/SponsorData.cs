using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Network;

namespace Content.Server._Stalker.Sponsors;

public sealed class SponsorData
{
    public static readonly Dictionary<string, SponsorLevel> RolesMap = new()
    {
        { "1172510785415684136", SponsorLevel.Bread },
        { "1158896573569318933", SponsorLevel.Backer },
        { "1233831223118266398", SponsorLevel.OMind },
        { "1233831339682172979", SponsorLevel.Pseudogiant }
    };

    public const string ContribRole = "1173624167053140108";

    public static SponsorLevel ParseRoles(List<string> roles)
    {
        var highestRole = SponsorLevel.None;
        foreach (var role in roles)
        {
            if (!RolesMap.ContainsKey(role))
                continue;

            if ((int)RolesMap[role] > (int)highestRole)
                highestRole = RolesMap[role];
        }

        return highestRole;
    }

    public static bool ParseContrib(List<string> roles)
    {
        foreach (var role in roles)
        {
            if (ContribRole == role)
                return true;
        }

        return false;
    }

    public SponsorData(SponsorLevel level, NetUserId userId, bool given, bool contributor = false)
    {
        Level = level;
        UserId = userId;
        IsGiven = given;
        Contributor = contributor;
    }

    public SponsorLevel Level;
    public NetUserId UserId;
    public bool IsGiven;
    public bool Contributor;
}
