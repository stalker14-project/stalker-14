using Content.Server.Hands.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared.Store;
using System.Linq;
using Content.Shared.FixedPoint;

namespace Content.Server._Stalker.Shop;

public sealed class ShopSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly STCurrencySystem _currency = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShopComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShopComponent, ShopRequestBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<ShopComponent, ShopRequestSellMessage>(OnSellRequest);
    }

    private void OnInit(EntityUid uid, ShopComponent component, ComponentInit args)
    {
        if (!_proto.TryIndex<ShopPresetPrototype>(component.ShopPreset, out var preset))
            return;

        // Получаем первую валюту из списка
        var mainCurrency = component.Currencies.FirstOrDefault();

        component.Categories = preset.Categories
            .Select(c => new ShopCategory(
                c.Id,
                c.Name,
                c.Priority,
                GenerateListings(c.Items, mainCurrency)
            ))
            .ToDictionary(c => c.Id);
    }

    private List<ListingData> GenerateListings(Dictionary<string, int> items, ProtoId<CurrencyPrototype> currency)
    {
        var listings = new List<ListingData>();

        foreach (var (itemId, price) in items)
        {
            listings.Add(new ListingData(
                name: _proto.Index<EntityPrototype>(itemId).Name,
                discountCategory: null,
                description: _proto.Index<EntityPrototype>(itemId).Description,
                conditions: null,
                icon: null,
                priority: 0,
                productEntity: itemId,
                productAction: null,
                productUpgradeId: null,
                productActionEntity: null,
                productEvent: null,
                raiseProductEventOnUser: false,
                purchaseAmount: 0,
                id: $"listing_{itemId}",
                categories: new HashSet<ProtoId<StoreCategoryPrototype>>(),
                originalCost: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>
                {
                    [currency] = FixedPoint2.New(price)
                },
                restockTime: TimeSpan.Zero,
                dataDiscountDownTo: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>(),
                disableRefund: false,
                count: 1
            ));
        }

        return listings;
    }

    private void OnBuyRequest(EntityUid uid, ShopComponent shop, ShopRequestBuyMessage msg)
    {
        if (!shop.Categories.TryGetValue(msg.CategoryId, out var category))
            return;

        var listing = category.Listings.FirstOrDefault(l => l.ProductEntity == msg.ProductId);
        if (listing == null || !_currency.TryDeductCurrencies(msg.Actor, listing.OriginalCost))
            return;

        var product = Spawn(listing.ProductEntity, Transform(msg.Actor).Coordinates);
        _hands.TryPickupAnyHand(msg.Actor, product);

        UpdateShopUi(uid, msg.Actor, shop);
    }

    private void OnSellRequest(EntityUid uid, ShopComponent shop, ShopRequestSellMessage msg)
    {
        var items = GetContainedItems(msg.Actor)
            .Where(e => MetaData(e).EntityPrototype?.ID == msg.ProductId)
            .Take(msg.Count)
            .ToList();

        if (items.Count < msg.Count)
            return;

        foreach (var item in items)
            Del(item);

        var currency = shop.Currencies.FirstOrDefault();
        var totalValue = msg.PricePerItem * msg.Count;
        _currency.AddCurrency(msg.Actor, currency, totalValue);

        UpdateShopUi(uid, msg.Actor, shop);
    }

    private void UpdateShopUi(EntityUid shopUid, EntityUid user, ShopComponent shop)
    {
        var balances = _currency.GetBalances(user, shop.Currencies); // TODO tryBalance not exists
        var userListings = GetUserListings(user);

        var state = new ShopUpdateState(
            balances,
            shop.Categories.Values.ToList(),
            null, // TODO sponsor
            null, // TODO contrib
            null, // TODO personal
            userListings
        );

        _ui.TrySetUiState(shopUid, ShopUiKey.Key, state); // TODO ui state updates here 
    }

    private List<EntityUid> GetContainedItems(EntityUid uid)
    {
        return _container.GetAllContainers(uid)
            .SelectMany(c => c.ContainedEntities)
            .ToList();
    }

    private List<ListingData> GetUserListings(EntityUid uid)
    {
        return GetContainedItems(uid)
            .GroupBy(e => MetaData(e).EntityPrototype?.ID)
            .Select(g => new ListingData
            {
                ProductEntity = g.Key!,
                Count = g.Count()
            })
            .ToList();
    }
}
