using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Shop;

[Serializable, NetSerializable]
public enum ShopUiKey
{
    Key
}

/// <summary>
/// Main state of stalker shops for UI updates.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShopUpdateState : BoundUserInterfaceState
{
    public readonly List<ListingData> UserItems;
    public readonly int Balance;
    public readonly string MoneyId;
    public readonly string LocMoneyId;
    public readonly List<CategoryInfo> Categories;
    public readonly List<CategoryInfo>? SponsorCategories;
    public readonly List<CategoryInfo>? ContribCategories;
    public readonly List<CategoryInfo>? PersonalCategories;

    public ShopUpdateState(
        int balance,
        string moneyId,
        string locMoneyId,
        List<CategoryInfo> categories,
        List<CategoryInfo>? sponsorCategories,
        List<CategoryInfo>? contribCategories,
        List<CategoryInfo>? personalCategories,
        List<ListingData> userItems)
    {
        Balance = balance;
        MoneyId = moneyId;
        LocMoneyId = locMoneyId;
        Categories = categories;
        SponsorCategories = sponsorCategories;
        ContribCategories = contribCategories;
        PersonalCategories = personalCategories;
        UserItems = userItems;
    }
}
[Serializable, NetSerializable]
public sealed class ShopRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{
    public ShopRequestUpdateInterfaceMessage()
    {
    }
}
[Serializable, NetSerializable]
public sealed class ShopClosedMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ShopRequestBuyMessage : BoundUserInterfaceMessage
{
    public ListingData ListingToBuy;
    public int Balance;
    public ShopRequestBuyMessage(ListingData listingData, int balance)
    {
        ListingToBuy = listingData;
        Balance = balance;
    }
}

[Serializable, NetSerializable]
public sealed class ShopRequestSellMessage : BoundUserInterfaceMessage
{
    public ListingData ListingToSell;
    public int Balance;
    public int Count;
    public ShopRequestSellMessage(ListingData listingData, int balance, int count = 1)
    {
        ListingToSell = listingData;
        Balance = balance;
        Count = count;
    }
}
