using System.Linq;
using Content.Server._Stalker.Sponsors;
using Content.Server.Actions;
using Content.Server.Cargo.Systems;
using Content.Server.Mind;
using Content.Server.Store.Components;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Sponsors;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Stalker.Storage;

namespace Content.Server._Stalker.Shop;

/// <summary>
/// Server system to control Stalkers' shops
/// </summary>
public sealed partial class ShopSystem : SharedShopSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly EntityManager _entity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    private ISawmill _sawmill = default!;
    private PriceCache _priceCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShopComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<ShopComponent, ShopRequestUpdateInterfaceMessage>(OnRequestUpdate);

        SubscribeLocalEvent<ShopComponent, ShopRequestBuyMessage>(OnBuyListing);
        SubscribeLocalEvent<ShopComponent, ShopRequestSellMessage>(OnSellListing);

        SubscribeLocalEvent<CurrencyComponent, HandSelectedEvent>(OnSelected);
        SubscribeLocalEvent<CurrencyComponent, HandDeselectedEvent>(OnDeselected);

        SubscribeLocalEvent<ShopComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StorageAfterRemoveItemEvent>(OnAfterRemove);
        SubscribeLocalEvent<StorageAfterInsertItemIntoLocationEvent>(OnAfterInsert);

        _sawmill = Logger.GetSawmill("shops");
        
        InitializeSponsors();
    }

    #region UI updates
    private void OnAfterInsert(StorageAfterInsertItemIntoLocationEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUis(args.User);
    }
    private void OnAfterRemove(StorageAfterRemoveItemEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUis(args.User);
    }
    private void OnInit(EntityUid uid, ShopComponent component, ComponentInit args)
    {
        var prototype = _proto.Index<ShopPresetPrototype>(component.ShopPresetPrototype);
        var moneyPrefix = component.MoneyId;
        var traderName = component.ShopPresetPrototype;

        // Add selling items to the price cache
        foreach (var (item, price) in prototype.SellingItems)
        {
            _priceCache.AddOrUpdatePrice(moneyPrefix, item, price, traderName);
        }
        // Check for potential economy issues with items for sale. This DOESN'T INCLUDE CONTAINERS
        var itemsForSale = prototype.Categories.SelectMany(category => category.Items);
        foreach (var (item, sellingPrice) in itemsForSale)
        {
            if (_priceCache.TryGetPriceInfo(moneyPrefix, item, out PriceInfo? priceInfo) && priceInfo?.BuyingPrice > sellingPrice)
            {
                var message = $"[Stalker] {item} is selling at {traderName} for just {sellingPrice} {moneyPrefix}, " +
                    $"but could be bought by {priceInfo.Trader} for {priceInfo.BuyingPrice} {moneyPrefix}. Please fix the economy!";
                _sawmill.Error(message);
            }
        }

        component.ShopCategories.AddRange(GenerateListingData(prototype.Categories, component));
    }
    private void OnSelected(EntityUid uid, CurrencyComponent component, HandSelectedEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUis(args.User);
    }

    private void UpdateUis(EntityUid user)
    {
        var uis = _ui.GetActorUis(user);
        if (uis == null)
            return;
        foreach (var ui in uis)
        {
            if (ui.Key.Equals(ShopUiKey.Key))
                UpdateShopUI(user, ui.Entity);
        }
    }

    private void OnDeselected(EntityUid uid, CurrencyComponent comp, HandDeselectedEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUis(args.User);
    }
    private void BeforeUIOpen(EntityUid uid, ShopComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateShopUI(args.User, uid, component: component);
    }

    private bool CheckPermit(EntityUid buyer, ShopComponent shop)
    {
        if (shop.Permit == null)
            return true;

        var elements = GetContainersElements(buyer);
        var hasPermit = elements.Any(item =>
        {
            if (!_entity.TryGetComponent<MetaDataComponent>(item, out var meta))
                return false;

            return meta?.EntityPrototype?.ID == shop?.Permit;
        });
        return hasPermit;
    }

    private void OnRequestUpdate(EntityUid uid, ShopComponent component, ShopRequestUpdateInterfaceMessage args)
    {
        UpdateShopUI(args.Actor, GetEntity(args.Entity), component: component);
    }
    private void UpdateShopUI(EntityUid? user, EntityUid shop, int? sellBuyBalance = null, ShopComponent? component = null)
    {
        if (!Resolve(shop, ref component))
            return;

        if (user is null)
            return;

        if (!_ui.TryOpenUi(shop, ShopUiKey.Key, user.Value))
            return;

        if (user == null)
            return;
        if (component == null)
            return;

        // God help me
        // No -God
        var curProto = _proto.Index<CurrencyPrototype>(component.MoneyId);
        var proto = _proto.Index<ShopPresetPrototype>(component.ShopPresetPrototype);
        var categories = component.ShopCategories;

        #region Sponsors-Stuff
        
        if (!TryComp<ActorComponent>(user, out var actor))
            return;
        
        // TODO: GetSponsorCategories/GetContributorCategories/GetPersonalCategories results is not being cached like
        // component.ShopCategories, due this little shit, idk how fast it'd work. But im sure this should be rewrited. Im too lazy now...
        var sponsorCategory = GetSponsorCategories(actor.PlayerSession.UserId, component);
        var contribCategories = GetContributorCategories(actor.PlayerSession.UserId, component);
        var personalCategories = GetPersonalCategories(actor.PlayerSession.Name, component);
        #endregion

        var userItems = GetContainerItemsWithoutMoney(user.Value, component);
        var userListings = GetListingData(userItems, component, proto.SellingItems);

        var money = sellBuyBalance ?? GetMoneyFromList(GetContainersElements(user.Value), component);
        component.CurrentBalance = money;
        _sawmill.Debug($"Sent balance to client: {component.CurrentBalance}");
        var state = new ShopUpdateState(
            money,
            component.MoneyId,
            curProto.DisplayName,
            categories,
            sponsorCategory, // sponsor shop categories for people who pays MONEY
            contribCategories, // contrib shop categories for less favorite than personals :(
            personalCategories, // personal stuff for Valdis' favourites ;)
            userListings);

        _ui.SetUiState(shop, ShopUiKey.Key, state);
    }
    #endregion
    #region Containers logic
    private string GetItemProtoId(EntityUid uid)
    {
        if (!TryComp(uid, out MetaDataComponent? mets))
            return string.Empty;
        var entityPrototypeId = mets.EntityPrototype?.ID;
        return entityPrototypeId ?? string.Empty;
    }

    private List<EntityUid> GetContainersElements(EntityUid uid, ContainerManagerComponent? managerComponent = null)
    {
        var result = new List<EntityUid>();
        if (!Resolve(uid, ref managerComponent))
            return new List<EntityUid>();

        foreach (var container in managerComponent.Containers.Values)
        {
            foreach (var element in container.ContainedEntities)
            {
                if (TryComp<ContainerManagerComponent>(element, out var manager))
                {
                    result.AddRange(GetContainersElements(element, manager));
                }
                else
                {
                    result.Add(element);
                }
            }
        }
        return result;
    }

    private List<EntityUid> GetContainerItemsWithoutMoney(EntityUid uid, ShopComponent component, ContainerManagerComponent? managerComponent = null)
    {
        var result = new List<EntityUid>();
        if (!Resolve(uid, ref managerComponent))
            return result;

        // Lots of loops argh...
        foreach (var container in managerComponent.Containers.Values)
        {
            foreach (var element in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(element, out var stack) && stack.StackTypeId == component.MoneyId)
                    continue;

                if (TryComp<OrganComponent>(element, out _))
                    continue;

                if (TryComp<ContainerManagerComponent>(element, out var manager))
                    result.AddRange(GetContainerItemsWithoutMoney(element, component, manager));

                result.Add(element);
            }
        }

        return result;
    }

    private int GetMoneyFromList(List<EntityUid> list, ShopComponent shop)
    {
        var result = 0;
        foreach (var entity in list)
        {
            if (TryComp<StackComponent>(entity, out var stack) && stack.StackTypeId == shop.MoneyId)
            {
                result += stack.Count;
                continue;
            }

            if (GetItemProtoId(entity) == shop.MoneyId)
            {
                result += 1;
            }
        }

        return result;
    }

    private bool DeleteEntityFromContainer(EntityUid uid, ShopComponent component, string toDelete, int amount, ContainerManagerComponent? managerComponent = null)
    {
        if (!Resolve(uid, ref managerComponent))
            return false;

        var elements = GetContainerItemsWithoutMoney(uid, component, managerComponent);
        elements.Reverse(); // Reverse this list of elements to delete entites from containers first
        if (elements.All(p => GetItemProtoId(p) != toDelete))
        {
            return false;
        }
        foreach (var element in elements)
        {
            if (GetItemProtoId(element) != toDelete)
                continue;

            Del(element);
            amount--;

            if (amount == 0)
                return true;
        }
        return false;
    }
    #endregion

    #region Listings
    private List<CategoryInfo> GenerateListingData(List<CategoryInfo> items, ShopComponent component)
    {
        var result = new List<CategoryInfo>();
        foreach (var category in items)
        {
            var categoryInfo = new CategoryInfo(category); // copy the whole category here

            foreach (var item in category.Items)
            {
                var proto = _proto.Index<EntityPrototype>(item.Key);

                var listing = new ListingData
                {
                    Categories = new HashSet<ProtoId<StoreCategoryPrototype>>(),
                    Conditions = new List<ListingCondition>(),
                    OriginalCost = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>()
                    {
                        [component.MoneyId] = item.Value
                    },
                    Description = proto.Description,
                    Icon = null,
                    Name = proto.Name,
                    Priority = 0,
                    ProductAction = null,
                    ProductEntity = item.Key,
                };
                if (!categoryInfo.ListingItems.Contains(listing))
                    categoryInfo.ListingItems.Add(listing);
            }
            result.Add(categoryInfo);
        }

        return result;
    }
    
    private List<ListingData> GetListingData(List<EntityUid> items, ShopComponent component, Dictionary<string, int> sellItems)
    {
        var result = new List<ListingData>();
        foreach (var item in items)
        {
            if (AddCount(item, ref result))
                continue;

            if (!TryComp<CurrencyComponent>(item, out var currency) || TryComp<BodyPartComponent>(item, out _))
                continue;

            if (!currency.Price.TryGetValue(component.MoneyId, out var money))
                continue;
            var solPrice = 0.0d;
            if (TryComp<SolutionContainerManagerComponent>(item, out _))
            {
                // ST-TODO: to be honest I don't think we really need it.
                // Also it creates a headache going to food and medicine ingridients and
                // making them cost 0 or balance it out
                //solPrice = _pricing.GetSolutionsPrice(item);
            }

            var meta = MetaData(item);
            var listing = new ListingData
            {
                Categories = new HashSet<ProtoId<StoreCategoryPrototype>>(),
                Conditions = new List<ListingCondition>(),

                OriginalCost = sellItems.TryGetValue(meta.EntityPrototype!.ID, out var sellItem)
                    ? new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { [component.MoneyId] = sellItem + Math.Round(solPrice) }
                    : new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { [component.MoneyId] = money.Int() + Math.Round(solPrice) },

                Description = meta.EntityDescription,
                Icon = null,
                Name = meta.EntityName,
                Priority = 0,
                ProductAction = null,
                ProductEntity = meta.EntityPrototype?.ID,
                Count = 1 // Initialize count to 1 for the first item
            };

            result.Add(listing);
        }

        return result;
    }

    private bool AddCount(EntityUid item, ref List<ListingData> listings)
    {
        foreach (var listing in listings)
        {
            if (listing.ProductEntity != GetItemProtoId(item))
                continue;

            listing.Count++;
            return true;
        }

        return false;
    }

    private void OnBuyListing(EntityUid uid, ShopComponent component, ShopRequestBuyMessage msg)
    {
        var listing = msg.ListingToBuy;
        var balance = component.CurrentBalance;

        if (msg.Actor is not { Valid: true } buyer)
            return;
        if (!listing.OriginalCost.TryGetValue(component.MoneyId, out var money))
            return;
        // Check that user has Permit for that shop
        if (!CheckPermit(buyer, component))
        {
            if (component.Permit.HasValue)
            {
                _proto.TryIndex(component?.Permit.Value, out var permitPrototype);
                _popup.PopupEntity(Loc.GetString("st-shop-requires-permit", ("permit", permitPrototype?.Name ?? "unknown")), uid);
            }
            return;
        }
        // Check that we have enough money
        if (money > balance)
            return;

        // Subtract the cash
        SubtractBalance(buyer, component, money.Int());
        balance -= money.Int();

        if (listing.ProductEntity != null)
        {
            var product = Spawn(listing.ProductEntity, Transform(buyer).Coordinates);
            _hands.PickupOrDrop(buyer, product);
        }

        if (!string.IsNullOrWhiteSpace(listing.ProductAction))
        {
            _actions.AddAction(buyer, listing.ProductAction);
        }

        if (listing.ProductEvent != null)
        {
            RaiseLocalEvent(listing.ProductEvent);
        }

        listing.PurchaseAmount++;
        component.CurrentBalance = balance;
        _sawmill.Debug($"Sent balance to client(buy listing): {component.CurrentBalance}");
        UpdateShopUI(buyer, uid, component.CurrentBalance, component);
    }

    private void OnSellListing(EntityUid uid, ShopComponent component, ShopRequestSellMessage msg)
    {
        var listing = msg.ListingToSell;
        var balance = component.CurrentBalance;
        if (msg.Actor is not { Valid: true } seller)
            return;

        // What are we selling lol?
        if (listing.ProductEntity == null)
            return;

        // Check that user has Permit for that shop
        if (!CheckPermit(seller, component))
        {
            if (component.Permit.HasValue)
            {
                _proto.TryIndex(component?.Permit.Value, out var permitPrototype);
                _popup.PopupEntity(Loc.GetString("st-shop-requires-permit", ("permit", permitPrototype?.Name ?? "unknown")), uid);
            }
            return;
        }
        // Delete sold entity
        var cost = 0;
        var amount = msg.Count;
        while (amount != 0)
        {
            cost += GetRecursiveCost(seller, component, listing.ProductEntity, listing);
            amount--;
        }
        bool isSellSuccesfull = DeleteEntityFromContainer(seller, component, listing.ProductEntity, msg.Count);

        // Increase player's balance
        if (!isSellSuccesfull)
            return;
        IncreaseBalance(seller, component, cost);

        balance += cost;
        component.CurrentBalance = balance;
        UpdateShopUI(seller, uid, component.CurrentBalance, component);
    }
    #endregion

    #region BalanceOperations

    private int GetRecursiveCost(EntityUid seller, ShopComponent component, string entity, ListingData listing)
    {
        var elements = GetContainerItemsWithoutMoney(seller, component);
        var cost = 0;
        foreach (var element in elements)
        {
            if (GetItemProtoId(element) != entity)
                continue;

            if (!listing.OriginalCost.TryGetValue(component.MoneyId, out var money))
                continue;

            cost += money.Int();

            if (!TryComp<ContainerManagerComponent>(element, out _))
                return cost;

            cost += GetCost(element, component);
            return cost;
        }

        return cost;
    }

    public int GetCost(EntityUid uid, ShopComponent component, ContainerManagerComponent? container = null)
    {
        var elements = GetContainerItemsWithoutMoney(uid, component, container);
        var cost = 0;
        foreach (var element in elements)
        {
            if (!TryComp<CurrencyComponent>(element, out var currencyComponent))
                continue;

            if (TryComp<ContainerManagerComponent>(element, out _))
                cost += GetCost(element, component, container);

            if (!currencyComponent.Price.TryGetValue(component.MoneyId, out var money))
                continue;

            cost += money.Int();
        }

        return cost;
    }
    private void IncreaseBalance(EntityUid uid, ShopComponent component, int change)
    {
        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        var elements = GetContainersElements(uid); // Necessary due to money adding below.
        foreach (var element in elements)
        {
            if (!TryComp<StackComponent>(element, out var stack) || stack.StackTypeId != component.MoneyId)
                continue;

            _stack.SetCount(element, stack.Count + change);
            return;
        }

        var money = Spawn(component.MoneyId, xform.Coordinates);
        _stack.SetCount(money, change);
        _hands.PickupOrDrop(uid, money);
    }

    private void SubtractBalance(EntityUid uid, ShopComponent component, int change)
    {
        var elements = GetContainersElements(uid);
        foreach (var element in elements)
        {
            if (!TryComp<StackComponent>(element, out var stack) || stack.StackTypeId != component.MoneyId)
                continue;

            if (stack.Count < 0)
                throw new Exception("Stack count cannot be negative!");

            if (stack.Count > change)
            {
                // I just can't adjust stacks through their native systems
                var old = stack.Count;
                stack.Count -= change;

                var ev = new StackCountChangedEvent(old, stack.Count);
                RaiseLocalEvent(element, ev);
                return;
            }

            QueueDel(element);
            change -= stack.Count;
        }
    }
    #endregion

    #region Helpers

    private record PriceInfo(float BuyingPrice, string Trader);

    /// <summary>
    /// Price cache, to help find problems in economy
    /// </summary>
    private sealed class PriceCache
    {
        private Dictionary<(string, string), PriceInfo> _buyingPricesInCurrencyByPriceAndTrader = new();

        public void AddOrUpdatePrice(string currency, string item, float price, string trader)
        {
            var key = (currency, item);
            if (_buyingPricesInCurrencyByPriceAndTrader.TryGetValue(key, out var priceInfo))
            {
                priceInfo = new PriceInfo(Math.Max(priceInfo.BuyingPrice, price), trader);
            }
            else
            {
                _buyingPricesInCurrencyByPriceAndTrader[key] = new PriceInfo(price, trader);
            }
        }

        public bool TryGetPriceInfo(string currency, string item, out PriceInfo? priceInfo)
        {
            var key = (currency, item);
            if (_buyingPricesInCurrencyByPriceAndTrader.TryGetValue(key, out var byingPriceInfo))
            {
                priceInfo = new PriceInfo(byingPriceInfo.BuyingPrice, byingPriceInfo.Trader);
                return true;
            }
            priceInfo = null;
            return false;
        }
    }
    #endregion
}
