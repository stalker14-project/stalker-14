using Robust.Shared.Player;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Shop;

[Serializable, NetSerializable]
public sealed class ShopUpdateState : BoundUserInterfaceState
{
    public readonly Dictionary<string, int> Balances;
    public readonly List<ShopCategory> Categories;
    public readonly List<ShopCategory>? SponsorCategories;
    public readonly List<ShopCategory>? ContribCategories;
    public readonly List<ShopCategory>? PersonalCategories;
    public readonly List<ListingData> UserListings;

    public ShopUpdateState(
        Dictionary<string, int> balances,
        List<ShopCategory> categories,
        List<ShopCategory>? sponsorCategories,
        List<ShopCategory>? contribCategories,
        List<ShopCategory>? personalCategories,
        List<ListingData> userListings)
    {
        Balances = balances;
        Categories = categories;
        SponsorCategories = sponsorCategories;
        ContribCategories = contribCategories;
        PersonalCategories = personalCategories;
        UserListings = userListings;
    }
}

[Serializable, NetSerializable]
public sealed class ShopRequestBuyMessage : BoundUserInterfaceMessage
{
    public string CategoryId;
    public string ProductId;

    public ShopRequestBuyMessage(string categoryId, string productId)
    {
        CategoryId = categoryId;
        ProductId = productId;
    }
}

[Serializable, NetSerializable]
public sealed class ShopRequestSellMessage : BoundUserInterfaceMessage
{
    public ListingData ListingToSell;
    public int Count;

    public ShopRequestSellMessage(ListingData listingData, int count = 1)
    {
        ListingToSell = listingData;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class ShopCategory
{
    public string Id { get; }
    public string Name { get; }
    public int Priority { get; }
    public List<ListingData> Listings { get; }

    public ShopCategory(string id, string name, int priority, List<ListingData> listings)
    {
        Id = id;
        Name = name;
        Priority = priority;
        Listings = listings;
    }
}
