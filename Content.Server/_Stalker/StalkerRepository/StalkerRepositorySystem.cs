using System.Linq;
using System.Threading.Tasks;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.Storage;
using Content.Server._Stalker.Teleports;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Storage;
using Content.Shared._Stalker.Weight;
using Content.Shared.Actions;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using RepositoryEjectMessage = Content.Shared._Stalker.StalkerRepository.RepositoryEjectMessage;
using Content.Server._Stalker.Sponsors.SponsorManager;
using Content.Shared.Verbs;

namespace Content.Server._Stalker.StalkerRepository;
public sealed class StalkerRepositorySystem : EntitySystem
{
    #region Init
    [Dependency] private readonly UserInterfaceSystem _ui = default!; // ui updates
    [Dependency] private readonly IPrototypeManager _prototypeMan = default!; // getting prototypes from entities
    [Dependency] private readonly StalkerStorageSystem _stalkerStorageSystem = default!; // saving to database
    [Dependency] private readonly MindSystem _mind = default!; // checking for player(actor component, is shit lol)
    [Dependency] private readonly TagSystem _tags = default!; // checking for blacklisted tags
    [Dependency] private readonly InventorySystem _inventory = default!; // dropping dependent items from slots
    [Dependency] private readonly SharedContainerSystem _container = default!; // iterating through containers
    [Dependency] private readonly SharedTransformSystem _xforms = default!; // dropping dependent items from slots
    [Dependency] private readonly IAdminLogManager _adminLogger = default!; // logging for admins(useless shit)
    [Dependency] private readonly SponsorsManager _sponsors = default!; // sponsors stuff
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!; // for searching by ckey
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!; // for checks for whitelist
    private ISawmill _sawmill = default!;

    // caching new records in database to get them later inside sponsors stuff
    private List<string> _newRecords = new();
    public override void Initialize()
    {
        base.Initialize();

        // sponsor stuff
        SubscribeLocalEvent<StalkerRepositoryComponent, RepositoryAdminSetEvent>(OnAdminSet);
        SubscribeLocalEvent<StalkerRepositoryComponent, NewRecordAddedEvent>(OnNewRecord);

        // base ui update messages
        SubscribeLocalEvent<StalkerRepositoryComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivate);
        SubscribeLocalEvent<StalkerRepositoryComponent, RequestUpdateRepositoryMessage>(OnRequestUpdate);

        // repository interacting messages
        SubscribeLocalEvent<StalkerRepositoryComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<StalkerRepositoryComponent, RepositoryInjectFromUserMessage>(OnInjectMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, RepositoryEjectMessage>(OnEjectMessage);

        // else updating shit, so it won't be hacked by others
        SubscribeLocalEvent<ItemComponent, HandSelectedEvent>(OnSelected);
        SubscribeLocalEvent<ItemComponent, HandDeselectedEvent>(OnDeselected);
        SubscribeLocalEvent<StorageAfterRemoveItemEvent>(OnAfterRemove);
        SubscribeLocalEvent<StorageAfterInsertItemIntoLocationEvent>(OnAfterInsert);


        _sawmill = Logger.GetSawmill("repository");
    }

    #endregion

    #region SponsorsStuff

    private void OnNewRecord(EntityUid uid, StalkerRepositoryComponent component, NewRecordAddedEvent args)
    {
        _newRecords.Add(args.Login);
    }

    private void OnAdminSet(EntityUid uid, StalkerRepositoryComponent component, RepositoryAdminSetEvent args)
    {
        if (!_playerManager.TryGetSessionByUsername(args.Admin, out var session))
            return;

        if (!_sponsors.TryGetInfo(session.UserId, out var sponsorData) ||
            sponsorData.SponsorProtoId is null)
            return;

        // already gave
        if (sponsorData.IsGiven)
            return;

        if (!_sponsors.TryGetSponsorRepositoryItems(sponsorData, out var items))
            return;

        foreach (var item in items)
        {
            var info = GenerateItemInfoByPrototype(item);
            InsertToRepo((uid, component), info);
        }

        Task.Run(() => _sponsors.SetGiven(session.UserId, true));
        _stalkerStorageSystem.SaveStorage(component);
    }

    public void GiveLoadout(Entity<StalkerRepositoryComponent> entity, List<EntProtoId> items)
    {
        foreach (var item in items)
        {
            var info = GenerateItemInfoByPrototype(item);
            InsertToRepo(entity, info);
        }
        _stalkerStorageSystem.SaveStorage(entity);
    }

    #endregion
    #region UiOperations

    // region with else updating shit, so it won't be hacked by other players
    #region UpdateOnChanges

    private void UpdateUiOnChanges(ICommonSession session, EntityUid uid)
    {
        var uis = _ui.GetActorUis(uid);
        if (uis == null)
            return;
        foreach (var ui in uis)
        {
            if (ui.Key.Equals(StalkerRepositoryUiKey.Key))
                UpdateUiState(uid, ui.Entity);
        }
    }
    private void OnAfterInsert(StorageAfterInsertItemIntoLocationEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUiOnChanges(session, args.User);
    }
    private void OnAfterRemove(StorageAfterRemoveItemEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUiOnChanges(session, args.User);
    }
    private void OnSelected(EntityUid uid, ItemComponent component, HandSelectedEvent args)
    {
        if (!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUiOnChanges(session, args.User);
    }

    private void OnDeselected(EntityUid uid, ItemComponent component, HandDeselectedEvent args)
    {
        if(!_mind.TryGetMind(args.User, out _, out var mindComp) || !_mind.TryGetSession(mindComp, out var session))
            return;

        UpdateUiOnChanges(session, args.User);
    }

    #endregion
    // base updating methods, like main UpdateUIState and others
    #region UpdateUiState

    private void OnBeforeActivate(EntityUid uid, StalkerRepositoryComponent component,
        BeforeActivatableUIOpenEvent args)
    {
        UpdateUiState(args.User, uid, component);
    }

    private void UpdateUiState(EntityUid? user, EntityUid repository, StalkerRepositoryComponent? component = null)
    {
        if (!Resolve(repository, ref component))
            return;

        var userItems = new List<RepositoryItemInfo>();

        if (user == null)
            return;

        userItems = GenerateItemsInfo(GetRecursiveContainerElements(user.Value), true);

        if (!_ui.TryOpenUi(repository, StalkerRepositoryUiKey.Key, user.Value))
            return;

        var items = component.ContainedItems;
        _ui.SetUiState(repository, StalkerRepositoryUiKey.Key, new RepositoryUpdateState(items, userItems, component.MaxWeight));
    }

    private void OnRequestUpdate(EntityUid uid, StalkerRepositoryComponent component,
        RequestUpdateRepositoryMessage msg)
    {
        UpdateUiState(msg.Actor, GetEntity(msg.Entity), component);
    }

    #endregion

    private HashSet<EntityUid> _currentlyProcessingEjects = new HashSet<EntityUid>();
    private readonly object _ejectLock = new();

    private void OnEjectMessage(EntityUid uid, StalkerRepositoryComponent component, RepositoryEjectMessage msg)
    {
        if (msg.Actor == null || _currentlyProcessingEjects.Contains(msg.Actor))
            return;

        _currentlyProcessingEjects.Add(msg.Actor);

        try
        {
            lock (_ejectLock)
            {
                if (msg.Item.Weight < 0)
                {
                    var sum = component.CurrentWeight - msg.Item.Weight;
                    if (Math.Round(sum, 2) > component.MaxWeight)
                    {
                        _sawmill.Debug($"Could not eject an item due to its weight. {msg.Item.Identifier} | item weight: {msg.Item.Weight} | repo weight: {component.CurrentWeight}");
                        return;
                    }
                }

                var item = GetFirstItem(component.ContainedItems, msg.Item.Identifier);
                if (item == null || item.Count < msg.Count)
                    return;

                item.Count -= msg.Count;

                if (item.SStorageData is IItemStalkerStorage stalker)
                {
                    stalker.CountVendingMachine -= (uint)msg.Count;
                }

                if (item.Count <= 0)
                    component.ContainedItems.Remove(item);

                EjectItems(GetEntity(msg.Entity), item, msg.Count);
                _adminLogger.Add(LogType.Action, LogImpact.Low, $"Player {Name(msg.Actor):user} ejected {msg.Count} {msg.Item.Name} from repository");
                _stalkerStorageSystem.SaveStorage(component);
                UpdateUiState(msg.Actor, GetEntity(msg.Entity), component);
            }
        }
        finally
        {
            _currentlyProcessingEjects.Remove(msg.Actor);
        }
    }



    private void OnInjectMessage(EntityUid uid, StalkerRepositoryComponent component,
        RepositoryInjectFromUserMessage msg)
    {
        if (msg.Actor == null)
            return;
        // Check for weight is valid
        var sum = component.CurrentWeight + msg.Item.Weight;
        if (Math.Round(sum, 2) > component.MaxWeight)
        {
            _sawmill.Debug($"Could not insert an item due to its weight. {msg.Item.Identifier} | item weight: {msg.Item.Weight} | repo weight: {component.CurrentWeight}");
            return;
        }
        // checks for items is really containing in players inventory, so it won't be hacked by putting one item out :D
        if (!CheckContaining(msg.Actor, msg.Item, msg.Count))
            return;

        // inserts selected item and checks for container, so its recursive
        // this method also returns us a hashset of entities to delete, so we are sure, we are deleting needed entity
        var toDelete = InsertToRepositoryRecursively(msg.Actor, (uid, component), msg.Item, msg.Count);
        if (toDelete == null)
            return;

        // removing items
        RemoveItems(msg.Actor, toDelete.Value.Item1, toDelete.Value.Item2);

        // logging, saving, ui updating
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Player {Name(msg.Actor):user} inserted {msg.Count} {msg.Item.Name} into repository");
        _stalkerStorageSystem.SaveStorage(component);
        RaiseLocalEvent(msg.Actor, new RepositoryItemInjectedEvent(uid, msg.Item));
        UpdateUiState(msg.Actor, GetEntity(msg.Entity), component);
    }

    private void OnInteractUsing(EntityUid uid, StalkerRepositoryComponent component, InteractUsingEvent args)
    {
        // generate new item info for clicked entity
        var itemInfo = GenerateItemInfo(args.Used, true);
        // check for valid weight
        var sum = component.CurrentWeight + itemInfo.Weight;
        if (Math.Round(sum, 2) > component.MaxWeight)
        {
            _sawmill.Debug($"Could not insert an item due to its weight. {itemInfo.Identifier} | item weight: {itemInfo.Weight} | repo weight: {component.CurrentWeight}");
            return;
        }
        // inserts new item and checks for container, so its recursive
        // this method also returns us a hashset of entities to delete, so we are sure, we are deleting needed entity
        var toDelete = InsertToRepositoryRecursively(args.User, (uid, component), itemInfo);

        // logging, saving, event raising
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Player {Name(args.User):user} inserted 1 {Name(args.Used)} into repository");
        _stalkerStorageSystem.SaveStorage(component);
        RaiseLocalEvent(args.User, new RepositoryItemInjectedEvent(args.Target, itemInfo));

        // removing by hashset we got from above
        // i had to move it here because of references
        if (toDelete == null)
            return;

        // removing items
        RemoveItems(args.User, toDelete.Value.Item1, toDelete.Value.Item2);
    }

    #endregion

    #region ItemsOperations
    /// <summary>
    /// Generates <see cref="RepositoryItemInfo"/> by item prototype, it'll be not so good as generating by entity, but still...
    /// </summary>
    /// <param name="item">Item prototype id</param>
    /// <returns>New instance of <see cref="RepositoryItemInfo"/></returns>
    public RepositoryItemInfo GenerateItemInfoByPrototype(string item)
    {
        // indexing prototype to get its data
        if (!_prototypeMan.TryIndex<EntityPrototype>(item, out var proto))
            return new RepositoryItemInfo();

        var spawned = Spawn(item, MapCoordinates.Nullspace);

        return GenerateItemInfo(spawned);
    }
    /// <summary>
    /// Generates full item info out of entityUid
    /// </summary>
    /// <param name="item">EntityUid of item you need info</param>
    /// <param name="userItem">Sets if its player item now, or contains inside our repository. Not so good practice, but its easiest</param>
    /// <returns>New instance of <see cref="RepositoryItemInfo"/></returns>
    private RepositoryItemInfo GenerateItemInfo(EntityUid item, bool userItem = false)
    {
        // gets item category or sets default
        var catName = Loc.GetString("repository-misc-category");
        var metaData = MetaData(item);
        if (TryComp<RepositoryItemComponent>(item, out var repoItem))
        {
            catName = repoItem.CategoryName;
        }

        // gets item weight
        TryComp<STWeightComponent>(item, out var weightComp);
        if (metaData.EntityPrototype == null)
            return new RepositoryItemInfo();

        // Conversion for database type
        var newSStorageData = _stalkerStorageSystem.ConvertToIItemStalkerStorage(item)[0];
        // Identifier for items that had to be unique, items with the same identifier can be stacked.
        var newIdentifier = "";

        if (newSStorageData is IItemStalkerStorage iss)
        {
            newIdentifier = iss.Identifier();
        }

        // get net entity, so we can greatly speedup looking over items
        NetEntity? entity = null;
        if (EntityManager.EntityExists(item))
            entity = GetNetEntity(item);

        // creating new instance of repo info itself
        var info = new RepositoryItemInfo
        {
            Category = catName,
            ProductEntity = metaData.EntityPrototype!.ID,
            Count = 1,
            Name = metaData.EntityName,
            Icon = null,
            Desc = metaData.EntityDescription,
            UserItem = userItem,
            Weight = weightComp?.Self ?? 0.05f,
            SumWeight = weightComp?.Total ?? 0.05f,
            Entities = entity == null ? null : new List<NetEntity> {entity.Value},
            SStorageData = newSStorageData,
            Identifier = newIdentifier
        };

        return info;
    }
    /// <summary>
    /// Generates items info for a list of items by entityUid
    /// </summary>
    /// <param name="itemsToConvert">Self explanatory</param>
    /// <param name="userItems">If this items is contained by player or inside our repository</param>
    /// <returns>List of new instances of <see cref="RepositoryItemInfo"/></returns>
    private List<RepositoryItemInfo> GenerateItemsInfo(List<EntityUid> itemsToConvert, bool userItems = false)
    {
        var repositoryInfoList = new List<RepositoryItemInfo>();
        foreach (var item in itemsToConvert)
        {
            // check for duplicates and increase count
            if (AddCount(item, ref repositoryInfoList))
                continue;

            // get entity proto
            var metaData = MetaData(item);
            if (metaData.EntityPrototype == null)
                continue;

            // generate item info by another method
            var info = GenerateItemInfo(item, userItems);
            repositoryInfoList.Add(info);
        }

        return repositoryInfoList;
    }
    /// <summary>
    /// Increases count for item if it has duplicates
    /// </summary>
    /// <param name="item">Item to check for duplicates</param>
    /// <param name="items">Already generated <see cref="RepositoryItemInfo"/> items</param>
    /// <returns>True/False depending on result. True if increased.</returns>
    private bool AddCount(EntityUid item, ref List<RepositoryItemInfo> items)
    {
        foreach (var element in items)
        {
            // generates stalker storage data
            var ident = _stalkerStorageSystem.ConvertToIItemStalkerStorage(item)[0];
            var identifier = string.Empty;

            // gets identifier to determine duplicates
            if (ident is IItemStalkerStorage itemStalker)
            {
                identifier = itemStalker.Identifier();
            }

            // sieve non-duplicates
            if (identifier != element.Identifier)
                continue;

            // if it has storageComp, we should not stack it, because of bugs
            if (HasComp<StorageComponent>(item))
                continue;
            element.Count++;
            if (ident is IItemStalkerStorage stalker)
            {
                stalker.CountVendingMachine++;
            }
            return true;
        }

        return false;
    }

    #endregion

    #region ContainersLogic
    /// <summary>
    /// Ejecting method, used in <see cref="OnEjectMessage"/>
    /// </summary>
    /// <param name="repository">EntityUid of our repository to eject from</param>
    /// <param name="item"><see cref="RepositoryItemInfo"/> to eject</param>
    /// <param name="amount">Amount of items to eject</param>
    private void EjectItems(EntityUid repository, RepositoryItemInfo item, int amount = 1)
    {
        // get transform of repository to determine coordinates to spawn items
        var xform = Transform(repository);
        if (!TryComp<StalkerRepositoryComponent>(repository, out var repoComp))
            return;
        // loop while we are not ejected all items
        while (amount != 0)
        {
            // spawn and reduce weight
            var spawned = Spawn(item.ProductEntity, xform.Coordinates);
            repoComp.CurrentWeight -= item.Weight;
            if (item.SStorageData is IItemStalkerStorage iss)
            {
                // call spawnedItem method to restore data inside all components of that item
                _stalkerStorageSystem.SpawnedItem(spawned,iss);
            }
            else
            {
                // usually not used, but still...
                RemoveItemsInsideContainer(spawned);
            }
            amount--;
        }
    }
    /// <summary>
    /// Checks for items to contain inside player inventory, so its unreal to "hack" our repository by throwing away one item of the same type.
    /// </summary>
    /// <param name="uid">Player Uid</param>
    /// <param name="toCheck"><see cref="RepositoryItemInfo"/> to check</param>
    /// <param name="toCheckCount">Amount of items player should have</param>
    /// <returns>True/False depending on containing or not.</returns>
    private bool CheckContaining(EntityUid uid, RepositoryItemInfo toCheck, int toCheckCount, ContainerManagerComponent? contMan = null)
    {
        // get all items from player
        var items = GetRecursiveContainerElements(uid);
        var foundAmount = 0;

        // iterate through all items and increase foundAmount
        foreach (var element in items)
        {
            if (toCheck.Identifier == GenerateIdentifier(element))
            {
                foundAmount++;
            }
        }
        return foundAmount >= toCheckCount;
    }
    /// <summary>
    /// Removing items from container by depth iterating
    /// </summary>
    /// <param name="uid">Uid of item containing other items</param>
    private void RemoveItemsInsideContainer(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containerMan))
            return;
        foreach (var container in containerMan.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                // we don't need to delete solutions
                if (HasComp<SolutionComponent>(item))
                    continue;
                QueueDel(item);
            }
        }
    }
    /// <summary>
    /// Modern version of remove items method, i hope it'll work correctly
    /// </summary>
    /// <param name="toRemove">Hashset of entities to delete</param>
    private void RemoveItems(EntityUid player, EntityUid uid, int amount)
    {
        if (amount - 1 == 0)
        {
            Del(uid);
            return;
        }

        var itemId = GenerateIdentifier(uid);
        Del(uid);
        amount--;

        var items = GetRecursiveContainerElements(player);
        foreach (var item in items)
        {
            if (amount == 0)
                return;

            var identifier = GenerateIdentifier(item);
            if (identifier != itemId)
                continue;

            Del(item);
            amount--;
        }
    }
    /// <summary>
    /// Obsolete remove items method, used to iterate through players' items and delete on match
    /// </summary>
    /// <param name="uid">Uid of player</param>
    /// <param name="item">Item to delete</param>
    /// <param name="amount">Amount of items to delete</param>
    /// <returns>If we deleted needed amount</returns>
    [Obsolete("Use hashset overload")]
    private bool RemoveItems(EntityUid uid, RepositoryItemInfo item, int amount = 1)
    {
        var items = GetRecursiveContainerElements(uid);

        foreach (var element in items)
        {
            if (amount == 0)
                return true;
            var info = GenerateItemInfo(element);
            if (info.Identifier == item.Identifier)
            {
                Del(element);
                amount--;
            }

            if (amount == 0)
                return true;

            if (!TryComp<ContainerManagerComponent>(element, out _))
                continue;
            if (RemoveItems(element, item, amount))
                return true;
        }
        return false;
    }
    /// <summary>
    /// Gets container items recursively, so you'll get all items from all containers that contained containers etc.
    /// </summary>
    /// <param name="uid">Start point, like player entity</param>
    /// <returns>List of entities' uids found from start point</returns>
    private List<EntityUid> GetRecursiveContainerElements(EntityUid uid,
        ContainerManagerComponent? managerComponent = null)
    {
        // pretending player won't have 200 items...
        var result = new List<EntityUid>(capacity: 200);

        if (!Resolve(uid, ref managerComponent))
            return new List<EntityUid>();

        foreach (var container in managerComponent.Containers)
        {
            if (container.Key == "toggleable-clothing") // We don't need anything from this container
                continue;
            foreach (var element in container.Value.ContainedEntities)
            {
                // don't look here, go further, just another shitty blacklist
                if (HasComp<OrganComponent>(element) ||
                    HasComp<InstantActionComponent>(element) ||
                    HasComp<WorldTargetActionComponent>(element) ||
                    HasComp<EntityTargetActionComponent>(element) ||
                    HasComp<SubdermalImplantComponent>(element) ||
                    HasComp<BodyPartComponent>(element) ||
                    HasComp<CartridgeComponent>(element) ||
                    HasComp<VirtualItemComponent>(element) ||
                    HasComp<MindContainerComponent>(element)) // Do not insert alive objects(mice, etc.)
                    continue;
                // recursively call the same method and add its result to our
                if (TryComp<ContainerManagerComponent>(element, out var manager))
                    AddRange(GetRecursiveContainerElements(element, manager), ref result);

                result.Add(element);
            }
        }
        return result;
    }
    /// <summary>
    /// Method that inserts items into repository recursively, so contained items will be inserted too
    /// </summary>
    /// <param name="user">Player that wants to insert</param>
    /// <param name="entity">Repository entity</param>
    /// <param name="toInsertItem"><see cref="RepositoryItemInfo"/> to insert</param>
    /// <param name="amount">Amount of such items to insert</param>
    /// <returns>Hashset of inserted items, so you can delete them later</returns>
    private (EntityUid, int)? InsertToRepositoryRecursively(EntityUid user, Entity<StalkerRepositoryComponent> entity,
        RepositoryItemInfo toInsertItem, int amount = 1)
    {
        var allowInsert = true;
        // little hack to not iterate through player items to find needed
        EntityUid? playerItem;
        switch (toInsertItem.Entities)
        {
            case null:
            {
                var playerItems = GetRecursiveContainerElements(user);
                playerItem = GetFirstItem(playerItems, toInsertItem.Identifier);
                break;
            }
            default:
                playerItem = GetEntity(toInsertItem.Entities.First(u => GenerateIdentifier(GetEntity(u)) == toInsertItem.Identifier));
                break;
        }

        if (playerItem == null)
            return null;
        // if we dont have containerManager we don't need to insert it recursively
        if (!TryComp<ContainerManagerComponent>(playerItem, out var containerMan))
        {
            allowInsert = CheckForWhitelist(entity, toInsertItem);
            if (!allowInsert)
                return null;
            InsertIntoRepository(entity, toInsertItem, amount);
            DropDependencies(user, playerItem.Value);
            return (playerItem.Value, amount);
        }
        // so we have contManComp, get all elements inside main entity
        var elements = GetRecursiveContainerElements(playerItem.Value);

        bool allowInsertRecursively = true;
        var items = new List<EntityUid>();

        foreach (var container in containerMan.Containers)
        {
            if (container.Key == "toggleable-clothing") // We don't need to add something from this container
                continue;
            foreach (var item in container.Value.ContainedEntities)
            {
                allowInsertRecursively = CheckForWhitelist(entity, GenerateItemInfo(item));
                elements.Remove(item);
                // another large blacklist
                if (HasComp<SolutionComponent>(item) || // Do not insert solutions
                    HasComp<InstantActionComponent>(item) || // Do not insert actions
                    HasComp<CartridgeComponent>(item) && !_tags.HasTag(item, "Dogtag") ||
                    HasComp<BallisticAmmoProviderComponent>(playerItem) && _tags.HasTag(item, "Cartridge"))  // Do not insert program cartridges
                    continue;

                items.Add(item);
            }
        }

        if (allowInsertRecursively)
        {
            foreach (var item in items.Where(n => n != null))
            {
                // checking for inner entities contMan, if it is, call recursively, else just insert
                if (TryComp<ContainerManagerComponent>(item, out _))
                {
                    InsertToRepositoryRecursively(user, entity, GenerateItemInfo(item), amount);
                }
                else
                {
                    allowInsert = CheckForWhitelist(entity, toInsertItem);
                    if (!allowInsert)
                        continue;
                    InsertIntoRepository(entity, GenerateItemInfo(item), amount);
                }
            }
        }

        // inserting main entity, adding to deleting hashset
        allowInsert = CheckForWhitelist(entity, toInsertItem);
        if (!allowInsert)
            return null;
        InsertIntoRepository(entity, toInsertItem, amount);
        // dropping dependent items
        DropDependencies(user, playerItem.Value);
        return (playerItem.Value, amount);
    }
    /// <summary>
    /// Method to drop dependent items, like gun in back slot, but we inserted our coat into repository, so we need to drop the gun
    /// </summary>
    /// <param name="user">Player</param>
    /// <param name="playerItem">Item inserted</param>
    private void DropDependencies(EntityUid user, EntityUid playerItem)
    {
        // if it wasn't clothing, do nothing
        if (!TryComp<ClothingComponent>(playerItem, out var comp))
            return;
        // check if it was innerclothing
        if (comp.Slots.HasFlag(SlotFlags.INNERCLOTHING))
        {
            // Check for item is on player
            if (!_inventory.TryGetSlotContainer(user, "jumpsuit", out var jumpsuitSlot, out _) ||
                !jumpsuitSlot.ContainedEntities.Contains(playerItem))
                return;

            if (_inventory.TryGetSlotContainer(user, "pocket1", out var slotCont, out var _) &&
                _inventory.TryGetSlotContainer(user, "pocket2", out var slotCont2, out _))
            {
                foreach (var item in slotCont.ContainedEntities)
                {
                    if(!_container.TryGetContainingContainer(user, item, out var container))
                        return;
                    _container.Remove(item, container);
                    _xforms.DropNextTo(user, item);
                }
                foreach (var item in slotCont2.ContainedEntities)
                {
                    if(!_container.TryGetContainingContainer(user, item, out var container))
                        return;
                    _container.Remove(item, container);
                    _xforms.DropNextTo(user, item);
                }
            }
        }
        // check for it was outerclothing
        if (!comp.Slots.HasFlag(SlotFlags.OUTERCLOTHING))
            return;

        // new block to use the same name of the variable
        {
            // Check for item is on player
            if (!_inventory.TryGetSlotContainer(user, "outerClothing", out var outerSlot, out _) ||
                !outerSlot.ContainedEntities.Contains(playerItem))
                return;

            if (!_inventory.TryGetSlotContainer(user, "suitstorage", out var slotCont, out _))
                return;
            foreach (var item in slotCont.ContainedEntities)
            {
                if(!_container.TryGetContainingContainer(user, item, out var container))
                    return;
                _container.Remove(item, container);
                _xforms.DropNextTo(user, item);
            }
        }
    }

    /// <summary>
    /// Checks that the Item is suitable for install into this repository
    /// </summary>
    private bool CheckForWhitelist(Entity<StalkerRepositoryComponent> entity, RepositoryItemInfo toInsertItem)
    {
        if (entity.Comp.Whitelist is null)
            return true;
        if (toInsertItem.Entities is null)
            return true;

        bool allowInsert = true;
        foreach(var netEntity in toInsertItem.Entities)
        {
            var uid = EntityManager.GetEntity(netEntity);
            allowInsert = _whitelistSystem.IsWhitelistPass(entity.Comp.Whitelist, uid);
        }
        return allowInsert;
    }


    /// <summary>
    /// Method to insert ONE item into repository
    /// </summary>
    /// <param name="entity">Repository entity</param>
    /// <param name="toInsertItem"><see cref="RepositoryItemInfo"/> item to insert</param>
    /// <param name="amount">Amount to insert</param>
    public void InsertIntoRepository(Entity<StalkerRepositoryComponent> entity, RepositoryItemInfo toInsertItem, int? amount = null)
    {
        // get any repoInfo if it exists with our identifier
        var repoItem = GetFirstItem(entity.Comp.ContainedItems, toInsertItem.Identifier);

        // if not null -> increase count, update storage data and return
        if (repoItem != null)
        {
            repoItem.Count += amount ?? toInsertItem.Count;
            repoItem.UserItem = false;
            entity.Comp.CurrentWeight += toInsertItem.Weight * amount ?? toInsertItem.Count;
            if (repoItem.SStorageData is IItemStalkerStorage stalker)
            {
                stalker.CountVendingMachine += amount == null ? (uint) toInsertItem.Count : (uint) amount;
                repoItem.SStorageData = stalker;
            }
            return;
        }
        // if null, add new one with our amount and create new storage data
        toInsertItem.UserItem = false;
        toInsertItem.Count = amount ?? toInsertItem.Count;
        if (toInsertItem.SStorageData is IItemStalkerStorage stalkerItem)
        {
            stalkerItem.CountVendingMachine = amount == null ? (uint) toInsertItem.Count : (uint) amount;
            // when inserting with OnInteract for some reason ReagentQuantity will got cleaned up after RemoveItems
            // here we're creating copies
            if (stalkerItem is SolutionItemStalker solutionItem)
            {
                var newContents = new Dictionary<string, List<Shared.Chemistry.Reagent.ReagentQuantity>>();
                foreach (var kvp in solutionItem.Contents)
                {
                    newContents[kvp.Key] = kvp.Value.Select(reagent => _serializationManager.CreateCopy(reagent)).ToList();
                }
                solutionItem.Contents = newContents;
            }
        }
        entity.Comp.ContainedItems.Add(toInsertItem);
        entity.Comp.CurrentWeight += toInsertItem.Weight * amount ?? toInsertItem.Count;
    }
    /// <summary>
    /// Used in <see cref="StalkerStorageSystem"/>, idk why
    /// </summary>
    /// <param name="entity">Repository entity</param>
    /// <param name="toInsertItem">Item to insert</param>
    /// <param name="amount">Amount of items to insert</param>
    public void InsertToRepo(Entity<StalkerRepositoryComponent> entity, RepositoryItemInfo toInsertItem, int? amount = null)
    {
        var repoItem = GetFirstItem(entity.Comp.ContainedItems, toInsertItem.Identifier);

        if (repoItem != null)
        {
            repoItem.Count += amount ?? toInsertItem.Count;
            repoItem.UserItem = false;
            entity.Comp.CurrentWeight += toInsertItem.Weight * amount ?? toInsertItem.Count;
            if (toInsertItem.SStorageData is IItemStalkerStorage stalker)
            {
                stalker.CountVendingMachine = amount == null ? (uint) toInsertItem.Count : (uint) amount;
                toInsertItem.SStorageData = stalker;
            }
            return;
        }

        toInsertItem.UserItem = false;
        toInsertItem.Count = amount ?? toInsertItem.Count;
        if (toInsertItem.SStorageData is IItemStalkerStorage stalkerItem)
        {
            stalkerItem.CountVendingMachine = amount == null ? (uint) toInsertItem.Count : (uint) amount;
            toInsertItem.SStorageData = stalkerItem;
        }
        entity.Comp.ContainedItems.Add(toInsertItem);
        entity.Comp.CurrentWeight += toInsertItem.Weight * amount ?? toInsertItem.Count;
    }

    #endregion
    // methods made to replace LinQ expressions
    #region HelperMethods
    /// <summary>
    /// Helper method to remove some items out of repository by its id
    /// </summary>
    private int RemoveItemsInternal(Entity<StalkerRepositoryComponent> entity, string id, int amount = 1)
    {
        var comp = entity.Comp;
        var toRemove = comp.ContainedItems.Where(i => i.ProductEntity == id).ToList();
        if (toRemove.Count <= 0)
            return 0;

        // get all elements in given range, we don't care what are them, we just need to delete certain amount
        var range = toRemove.GetRange(0, amount);
        var removedCount = 0;
        foreach (var item in range)
        {
            if (!comp.ContainedItems.Contains(item))
            {
                _sawmill.Error($"Item to remove not found in {ToPrettyString(entity.Owner)} | toDelete: {item.Name}");
                return removedCount;
            }
            comp.ContainedItems.Remove(item);
            removedCount++;
        }
        _stalkerStorageSystem.SaveStorage(entity);
        return removedCount;
    }
    /// <summary>
    /// Public version of <see cref="RemoveItemsInternal"/>
    /// </summary>
    /// <param name="entity">Repository entity</param>
    /// <param name="id">Prototype id of item</param>
    /// <param name="ckey">Ckey, necessary if we are deleting with newRecordOnly</param>
    /// <param name="newRecordOnly">If we should delete only if it is a new record</param>
    /// <param name="amount">Amount of items removed</param>
    /// <returns></returns>
    public int RemoveItems(Entity<StalkerRepositoryComponent> entity, string id, string? ckey = null, bool newRecordOnly = true,  int amount = 1)
    {
        if (ckey == null && newRecordOnly)
        {
            _sawmill.Error("Tried to remove items on newRecord without passing any ckey");
            return 0;
        }

        if (ckey != null && newRecordOnly && !_newRecords.Contains(ckey))
            return 0;

        return RemoveItemsInternal(entity, id, amount);
    }


    /// <summary>
    /// Generates item's identifier
    /// </summary>
    /// <param name="item">Item to get identifier</param>
    /// <returns>Identifier in string</returns>
    private string GenerateIdentifier(EntityUid item)
    {
        var newSStorageData = _stalkerStorageSystem.ConvertToIItemStalkerStorage(item)[0];
        if (newSStorageData is IItemStalkerStorage iss)
        {
            return iss.Identifier();
        }
        return string.Empty;
    }
    /// <summary>
    /// Gets first item with needed identifier out of a list
    /// </summary>
    /// <param name="uids">List of Ids</param>
    /// <param name="identifier">Identifier to find</param>
    /// <returns>First found entity, or null if not</returns>
    private EntityUid? GetFirstItem(List<EntityUid> uids, string identifier)
    {
        foreach (var uid in uids)
        {
            if (GenerateIdentifier(uid) == identifier)
                return uid;
        }
        return null;
    }
    /// <summary>
    /// <see cref="GetFirstItem(System.Collections.Generic.List{Robust.Shared.GameObjects.EntityUid},string)"/> but with <see cref="RepositoryItemInfo"/>
    /// </summary>
    /// <param name="items">Items to iterate</param>
    /// <param name="identifier">Identifier to find</param>
    /// <returns>First found <see cref="RepositoryItemInfo"/>, or null if not</returns>
    private RepositoryItemInfo? GetFirstItem(List<RepositoryItemInfo> items, string identifier)
    {
        foreach (var item in items)
        {
            if (item.Identifier == identifier)
                return item;
        }
        return null;
    }
    /// <summary>
    /// Replace of <see cref="List{T}"/> add range method without LinQ
    /// </summary>
    /// <param name="toAdd">Main instance to add</param>
    /// <param name="adding">Adding list</param>
    private void AddRange(List<EntityUid> toAdd, ref List<EntityUid> adding)
    {
        foreach (var el in toAdd)
        {
            adding.Add(el);
        }
    }
#endregion
}
