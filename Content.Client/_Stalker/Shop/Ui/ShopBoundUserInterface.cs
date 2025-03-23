using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using JetBrains.Annotations;

namespace Content.Client._Stalker.Shop.Ui;

/// <summary>
/// Stalker shops BUI to handle events raising and send data to server.
/// </summary>
[UsedImplicitly]
public sealed class ShopBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ShopMenu? _menu;

    public ShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _menu = new ShopMenu();
        _menu.OpenCentered();

        _menu.OnClose += () =>
        {
            Close();
        };

        _menu.OnCategoryButtonPressed += (_, category) =>
        {
            _menu.CurrentCategory = category;
            SendMessage(new ShopRequestUpdateInterfaceMessage());
        };


        _menu.OnListingButtonPressed += (_, listing, sell, balance, count) =>
        {
            switch (sell)
            {
                case false:
                    if (_menu?.CurrentCategory == null || listing.ProductEntity == null)
                        return;

                    var categoryId = _menu.CurrentCategory;
                    var productId = listing.ProductEntity.ToString();

                    if (string.IsNullOrEmpty(productId))
                        return;

                    SendMessage(new ShopRequestBuyMessage(categoryId, productId));
                    break;

                default:
                    if (count == null)
                        return;

                    var sellProductId = listing.ProductEntity.ToString();
                    if (string.IsNullOrEmpty(sellProductId))
                        return;
                    SendMessage(new ShopRequestSellMessage(sellProductId, count.Value));
                    break;
            }
        };
        _menu.OnRefreshButtonPressed += () =>
        {
            SendMessage(new ShopRequestUpdateInterfaceMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ShopUpdateState updateState || _menu == null)
            return;

        var allCategories = new List<ShopCategory>(updateState.Categories);
        if (updateState.SponsorCategories != null)
            allCategories.AddRange(updateState.SponsorCategories);
        if (updateState.ContribCategories != null)
            allCategories.AddRange(updateState.ContribCategories);
        if (updateState.PersonalCategories != null)
            allCategories.AddRange(updateState.PersonalCategories);

        _menu.PopulateStoreCategoryButtons(allCategories, updateState.UserListings);
        _menu.UpdateListing(allCategories, updateState.UserListings);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
        _menu?.Dispose();
    }
}
