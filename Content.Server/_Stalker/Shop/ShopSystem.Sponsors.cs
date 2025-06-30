using System.Linq;
using Content.Server._Stalker.Sponsors.SponsorManager;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Shop;

public sealed partial class ShopSystem
{
    [Dependency] private readonly SponsorsManager _sponsors = default!;

    private HashSet<PersonalShopPrototype> _personalShopPrototypes = new();
    
    private void InitializeSponsors()
    {
        _proto.PrototypesReloaded += ReEnumeratePrototypes;
        ReEnumeratePrototypes();
    }
    
    private List<CategoryInfo> GetSponsorCategories(NetUserId userId, ShopComponent component)
    {
        if (!_sponsors.TryGetInfo(userId, out var info) ||
            info.SponsorProtoId is null)
            return new List<CategoryInfo>();
        
        var shopPresetIndex = _proto.Index<ShopPresetPrototype>(component.ShopPresetPrototype);
        var sponsorIndex = _proto.Index<SponsorPrototype>(info.SponsorProtoId.Value);
        var sponsorPrototypes = _sponsors.GetSponsorPrototypesLowerThan(sponsorIndex.SponsorPriority);
        
        var validSponsorIds = sponsorPrototypes
            .Select(p => p.ID)
            .Append(info.SponsorProtoId.Value.Id)
            .ToHashSet();
        
        var categories = shopPresetIndex.SponsorCategories
            .Where(kv => validSponsorIds.Contains(kv.Key))
            .SelectMany(kv => kv.Value)
            .ToList();

        return GenerateListingData(categories, component);
    }

    private List<CategoryInfo> GetPersonalCategories(string username, ShopComponent component)
    {
        var categories = _personalShopPrototypes
            .Where(p => p.Username == username)
            .SelectMany(p => p.Categories)
            .ToList();

        return GenerateListingData(categories, component);
    }

    private List<CategoryInfo> GetContributorCategories(NetUserId userId, ShopComponent component)
    {
        if (!_sponsors.TryGetInfo(userId, out var info) ||
            !info.Contributor)
            return new List<CategoryInfo>();

        var index = _proto.Index<ShopPresetPrototype>(component.ShopPresetPrototype);
        return GenerateListingData(index.ContributorCategories, component);
    }

    private void ReEnumeratePrototypes(PrototypesReloadedEventArgs? args = null)
    {
        // dry-run
        if (args is null)
        {
            _personalShopPrototypes = _proto
                .EnumeratePrototypes<PersonalShopPrototype>()
                .ToHashSet();

            return;
        }

        if (!args.WasModified<PersonalShopPrototype>())
            return;

        _personalShopPrototypes = _proto
            .EnumeratePrototypes<PersonalShopPrototype>()
            .ToHashSet();
    }
}