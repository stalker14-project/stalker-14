using System.Linq;
using System.Text.Json;
using Content.Server.VendingMachines;
using Content.Shared.Interaction;
using Content.Shared.VendingMachines;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using System.Text.Json.Nodes;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.StalkerRepository;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Light.Components;
using Content.Shared.Paper;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Server.Botany.Components;
using Content.Shared._Stalker;
using Content.Shared._Stalker.Storage;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Storage;

public sealed class StalkerStorageSystem : SharedStalkerStorageSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly StalkerDbSystem _stalkerDbSystem = default!;
    [Dependency] private readonly BatterySystem _batterySys = default!;
    [Dependency] private readonly StalkerRepositorySystem _stalkerRepositorySystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private delegate List<object> DelegateItemStalkerConverter(EntityUid inputEntityUid);

    //Коллекция конвентеров для предметов
    private readonly Dictionary<string, DelegateItemStalkerConverter> _convertersItemStalker = new(0);
    private readonly HashSet<Type> _blackListDelChildrenOnSpawnComponent = new(0);
    private readonly HashSet<string> _blackListContainerNames = new(0);
    private readonly Dictionary<EntProtoId, EntProtoId> _mapping = [];

    private void InstallLists()
    {
        _blackListDelChildrenOnSpawnComponent.Add(typeof(ToggleableClothingComponent));
        _blackListDelChildrenOnSpawnComponent.Add(typeof(HandheldLightComponent));
        _blackListDelChildrenOnSpawnComponent.Add(typeof(SolutionComponent));

        _blackListContainerNames.Add("toggleable-clothing");
        _blackListContainerNames.Add("actions");
    }

    private bool IsBlackListed(EntityUid inputItem)
    {
        foreach (var component in _entityManager.GetComponents(inputItem))
        {
            if (_blackListDelChildrenOnSpawnComponent.Contains(component.GetType()))
                return true;

        }
        return false;
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        InstallLists();

        SubscribeLocalEvent<StalkerStorageComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<StalkerStorageComponent, VendingMachineEjectMessage>(OnInventoryEjectMessage);

        // Обычный предмет
        _convertersItemStalker.Add("M", ConverterSimpleItemStalker);

        // Предмет с патронами(например магазин)
        _convertersItemStalker.Add("MB", ConverterAmmoItemStalker);

        // Предмет который имеет стак
        _convertersItemStalker.Add("MS", ConverterStackItemStalker);

        _convertersItemStalker.Add("MP", ConverterPaperItemStalker);
        _convertersItemStalker.Add("ML", ConverterBatteryItemStalker);
        _convertersItemStalker.Add("ME", ConverterSolutionItemStalker); // Solutions
        _convertersItemStalker.Add("MC", ConverterCartridgeItemStalker);
        // Доделать еще конвентеры для предметов с жидкостями и т.д.

        foreach (var migration in _prototype.EnumeratePrototypes<STStashMigrationPrototype>())
        {
            foreach (var (key, value) in migration.Mapping)
            {
                _mapping.TryAdd(key, value);
            }
        }
    }

    #region HelperMethods

    // Получить имя прототипа(например у лома имя прототипа будет "Crowbar") по его EntityUid
    public string GetPrototypeName(EntityUid inputItemUid)
    {
        if (!TryComp(inputItemUid, out MetaDataComponent? metaData))
            return "";
        return metaData.EntityPrototype?.ID == null ? "" : metaData.EntityPrototype.ID;
    }

    // Получить название ентити(имя в мире, например "Лом") по его EntityUid
    public string GetNameEntity(EntityUid inputItemUid)
    {
        return !TryComp(inputItemUid, out MetaDataComponent? metaData) ? "" : metaData.EntityName;
    }

    #endregion

    #region Converters

    public List<object> ConvertToIItemStalkerStorage(EntityUid InputItem)
    {
        List<object> ReturnList = new List<object>(0);
        string Components = "";

        if (TryComp(InputItem, out MetaDataComponent? _))
        {
            Components += "M";
        }
        if (TryComp(InputItem, out BallisticAmmoProviderComponent? _))
        {
            Components += "B";
        }
        if (TryComp(InputItem, out StackComponent? _))
        {
            Components += "S";
        }
        if (TryComp(InputItem, out PaperComponent? _))
        {
            Components += "P";
        }
        if (TryComp(InputItem, out BatteryComponent? _))
        {
            Components += "L";
        }

        if (TryComp(InputItem, out SolutionContainerManagerComponent? _) &&
            !HasComp<BatteryComponent>(InputItem) &&
            !HasComp<StackComponent>(InputItem) &&
            !HasComp<PaperComponent>(InputItem) &&
            !HasComp<SeedComponent>(InputItem))
        {
            Components += "E";
        }
        if (TryComp(InputItem, out CartridgeAmmoComponent? _)) // Cartridges
        {
            Components += "C";
        }

        if (_convertersItemStalker.ContainsKey(Components))
        {
            return _convertersItemStalker[Components](InputItem);
        }
        else
        {
            //Console.WriteLine("ConvertOneItemToIItemStalkerStorage ERROR ProrotypeName="+GetProrotypeName(InputItem));
        }
        return ReturnList;
    }

    public List<object> ConverterSimpleItemStalker(EntityUid inputItem)
    {
        var returnList = new List<object>(0) { new SimpleItemStalker(GetPrototypeName(inputItem)) };
        return returnList;
    }

    public List<object> ConverterStackItemStalker(EntityUid inputItem)
    {
        var returnList = new List<object>(0);

        if (TryComp(inputItem, out StackComponent? stackComponent))
            returnList.Add(new StackItemStalker(GetPrototypeName(inputItem), 1u, (uint)stackComponent.Count));

        return returnList;
    }

    private List<object> ConverterPaperItemStalker(EntityUid inputItem)
    {
        var returnList = new List<object>(0);

        if (!TryComp(inputItem, out PaperComponent? paperComponent))
            return returnList;

        var paper = new PaperItemStalker(GetPrototypeName(inputItem), 1, paperComponent.Content, paperComponent.ContentSize);

        if (paperComponent.StampState != null)
        {
            paper.StampState = paperComponent.StampState;
        }

        var newListStampStalkerData = new List<PaperItemStalker.StampStalkerData>(0);

        foreach (var stamp in paperComponent.StampedBy)
        {
            var colorStamp = new PaperItemStalker.StampColorStalkerData(stamp.StampedColor.R, stamp.StampedColor.G, stamp.StampedColor.B, stamp.StampedColor.A);
            var newStamp = new PaperItemStalker.StampStalkerData(stamp.StampedName, colorStamp);

            newListStampStalkerData.Add(newStamp);
        }

        paper.ListStampStalkerData = newListStampStalkerData;
        returnList.Add(paper);

        return returnList;
    }

    private List<object> ConverterAmmoItemStalker(EntityUid inputItem)
    {
        var returnList = new List<object>(0);

        if (!TryComp(inputItem, out BallisticAmmoProviderComponent? ammoProvider))
            return returnList;
        if (ammoProvider.Container.ContainedEntities.Count != 0)
        {
            var ent = ammoProvider.Container.ContainedEntities.First();
            var entProto = GetPrototypeName(ent);
            ammoProvider.Proto ??= entProto;
        }
        returnList.Add(new AmmoContainerStalker(GetPrototypeName(inputItem), ammoProvider.Proto, ammoProvider.EntProtos, ammoProvider.Count));
        return returnList;
    }

    private List<object> ConverterBatteryItemStalker(EntityUid item)
    {
        var returnList = new List<object>(0);
        if (!TryComp<BatteryComponent>(item, out var battery))
            return returnList;
        returnList.Add(new BatteryItemStalker(battery.CurrentCharge, GetPrototypeName(item)));
        return returnList;
    }

    private List<object> ConverterSolutionItemStalker(EntityUid item)
    {
        var returnList = new List<object>(capacity: 0);
        if (!TryComp<SolutionContainerManagerComponent>(item, out _) ||
            !TryComp<ContainerManagerComponent>(item, out var containerMan))
            return returnList;
        var dict = new Dictionary<string, List<ReagentQuantity>>();
        var volume = FixedPoint2.Zero;
        foreach (var container in containerMan.Containers)
        {
            var split = container.Key.Split("@");
            if (split[0] != "solution")
                continue;
            foreach (var element in container.Value.ContainedEntities)
            {
                if (!TryComp<SolutionComponent>(element, out var solution))
                    continue;
                dict.Add(split[1], solution.Solution.Contents);
                volume += solution.Solution.Volume;
            }
        }
        returnList.Add(new SolutionItemStalker(dict, GetPrototypeName(item), volume));
        return returnList;
    }

    public List<object> ConverterCartridgeItemStalker(EntityUid item)
    {
        var returnList = new List<object>(capacity: 0);
        if (!TryComp<CartridgeAmmoComponent>(item, out var ammoComp))
            return returnList;
        if (ammoComp.Spent)
        {
            returnList.Add(new AmmoItemStalker(GetPrototypeName(item), true));
            return returnList;
        }
        returnList.Add(new AmmoItemStalker(GetPrototypeName(item), false));
        return returnList;
    }


    #endregion

    #region ItemOperations

    public void LoadStalkerItemsByEntityUid(EntityUid inputEntity)
    {
        var deserializedFromJson = new List<IItemStalkerStorage>();
        if (!TryComp(inputEntity, out StalkerRepositoryComponent? stalkerRepositoryComponent) || stalkerRepositoryComponent.LoadedDbJson == string.Empty)
            return;

        var fromDbPlayerInventory = InventoryFromJson(stalkerRepositoryComponent.LoadedDbJson);

        foreach (var item in fromDbPlayerInventory.AllItems)
        {
            if (item is not IItemStalkerStorage storageItem)
                continue;

            storageItem.PrototypeName = MapPrototype(storageItem.PrototypeName);

            if (item is AmmoContainerStalker ammoContainer)
                ammoContainer.EntProtoIds = MapPrototype(ammoContainer.EntProtoIds);

            deserializedFromJson.Add(storageItem);
        }

        foreach (var itemConverted in deserializedFromJson)
        {
            AddToVendingMachineProtoByName(itemConverted.Identifier(), itemConverted.PrototypeName, stalkerRepositoryComponent, itemConverted);
        }
    }

    private List<EntProtoId> MapPrototype(in List<EntProtoId> protoIds)
    {
        return protoIds.Select(MapPrototype).ToList();
    }

    private EntProtoId MapPrototype(EntProtoId protoId)
    {
        var prototype = protoId;
        if (_mapping.TryGetValue(prototype, out var newPrototype))
            prototype = newPrototype;

        if (!_prototype.HasIndex(prototype))
            Log.Error($"A non-existent prototype entity in the stash {prototype}");

        return prototype;
    }

    public void SpawnedItem(EntityUid inputItemUid, IItemStalkerStorage? nextSpawnOptions)
    {
        if (IsBlackListed(inputItemUid) == false && !HasComp<SolutionContainerManagerComponent>(inputItemUid))
        {
            DeleteChildren(inputItemUid);
            DeleteAmmo(inputItemUid);
        }

        if (TryComp<ItemSlotsComponent>(inputItemUid, out var slots))
        {
            foreach (var slot in slots.Slots)
            {
                QueueDel(slot.Value.Item);
                if (slot.Value.Item == null)
                    continue;
                Logger.Debug($"Deleted {Name(slot.Value.Item.Value)}");
            }
        }
        switch (nextSpawnOptions)
        {
            case null:
                return;
            case StackItemStalker sitOptions:
                {
                    if (TryComp(inputItemUid, out StackComponent? stackComponent))
                    {
                        stackComponent.Count = (int)sitOptions.StackCount;
                        Dirty(inputItemUid, stackComponent);
                    }
                    break;
                }
            case BatteryItemStalker options:
                if (TryComp<BatteryComponent>(inputItemUid, out var batteryComponent))
                {
                    _batterySys.SetCharge(inputItemUid, options.CurrentCharge, batteryComponent);
                }
                break;
            case AmmoContainerStalker options:
                if (TryComp<BallisticAmmoProviderComponent>(inputItemUid, out var ammoProvider))
                {
                    ammoProvider.Proto = options.AmmoPrototypeName;
                    ammoProvider.UnspawnedCount = options.AmmoCount;
                    ammoProvider.EntProtos = options.EntProtoIds;
                    Dirty(inputItemUid, ammoProvider);
                }
                break;
            case SolutionItemStalker options:
                {
                    if (TryComp<ContainerManagerComponent>(inputItemUid, out var containerMan))
                    {
                        foreach (var container in containerMan.Containers)
                        {
                            var split = container.Key.Split("@");
                            if (split[0] != "solution") // If it is not a solution container we don't need to iterate through it
                                continue;
                            foreach (var element in container.Value.ContainedEntities)
                            {
                                if (!TryComp<SolutionComponent>(element, out var solution))
                                    continue;
                                solution.Solution.Contents.Clear();
                                if (!options.Contents.TryGetValue(split[1], out var contents))
                                    continue;
                                foreach (var quan in contents)
                                {
                                    solution.Solution.AddReagent(quan);
                                }
                                solution.Solution.Volume = options.Volume;
                                Dirty(element, solution);
                            }
                        }
                    }
                    break;
                }
            case AmmoItemStalker options:
                {
                    if (TryComp<CartridgeAmmoComponent>(inputItemUid, out var ammoComp))
                    {
                        ammoComp.Spent = options.Exhausted;
                        _appearance.SetData(inputItemUid, AmmoVisuals.Spent, options.Exhausted);
                    }
                    break;
                }
        }

        if (nextSpawnOptions is not PaperItemStalker paperItemStalker)
            return;

        if (!TryComp(inputItemUid, out PaperComponent? paperComponent))
            return;

        paperComponent.Content = paperItemStalker.Content;
        paperComponent.ContentSize = paperItemStalker.ContentSize;


        if (paperItemStalker.ListStampStalkerData.Count > 0)
        {
            var newStampedBy = new List<StampDisplayInfo>(0);
            foreach (var data in paperItemStalker.ListStampStalkerData)
            {
                var newStampedByOne = new StampDisplayInfo
                {
                    StampedName = data.StampedName,
                    StampedColor = new Color(data.PaperColorStalkerData.R, data.PaperColorStalkerData.G, data.PaperColorStalkerData.B, data.PaperColorStalkerData.A)
                };
                newStampedBy.Add(newStampedByOne);
            }

            paperComponent.StampedBy = newStampedBy;
        }

        if (paperItemStalker.StampState != "")
        {
            paperComponent.StampState = paperItemStalker.StampState;
        }
    }

    private void DeleteAmmo(EntityUid inputItemUid)
    {
        if (!TryComp(inputItemUid, out BallisticAmmoProviderComponent? ammoProvider))
            return;
        ammoProvider.UnspawnedCount = 0;
    }

    private void DeleteChildren(EntityUid inputItemUid)
    {
        if (!TryComp(inputItemUid, out TransformComponent? transform))
            return;
        var enumerator = transform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            EntityManager.QueueDeleteEntity(child);
            Logger.Debug($"Deleted {Name(child)}");
        }
    }

    #endregion

    #region ContainerOperations

    //Получить содержимое всех контейнеров(в сумках, частях тела,частях оружия и т.д.)

    private List<EntityUid> GetAllContainersUidItems(EntityUid inputItem)
    {
        var allContainerUids = new List<EntityUid>(0);
        allContainerUids.AddRange(GetContainerUids(inputItem));
        return allContainerUids;
    }

    //Получить контейнеры из ContainerManagerComponent
    private List<EntityUid> GetContainerUids(EntityUid inputItemUid)
    {
        var containerUids = new List<EntityUid>(0);

        if (!TryComp(inputItemUid, out ContainerManagerComponent? managerComponent))
            return containerUids;

        foreach (var container in managerComponent.Containers)
        {
            if (_blackListContainerNames.Contains(container.Key))
                continue;

            foreach (var entity in container.Value.ContainedEntities)
            {
                //Добавляем предмет который внутри
                containerUids.Add(entity);

                //Добавляем еще предметы если они существуют внутри добавляемого обьекта
                containerUids.AddRange(GetAllContainersUidItems(entity));
            }
        }
        return containerUids;
    }

    //Получить контейнеры из ContainerManagerComponent без черного списка
    public List<EntityUid> GetSomeContainerUidsWithoutBlackList(EntityUid inputItemUid)
    {
        var containerUids = new List<EntityUid>(0);

        if (!TryComp(inputItemUid, out ContainerManagerComponent? managerComponent))
            return containerUids;

        foreach (var container in managerComponent.Containers)
        {
            foreach (var entity in container.Value.ContainedEntities)
            {
                //Добавляем предмет который внутри
                containerUids.Add(entity);

                //Добавляем еще предметы если они существуют внутри добавляемого обьекта
                containerUids.AddRange(GetSomeContainerUidsWithoutBlackList(entity));
            }
        }
        return containerUids;
    }

    #endregion

    #region VendingStorageOperations

    private void OnInventoryEjectMessage(EntityUid uid, StalkerStorageComponent component, VendingMachineEjectMessage args)
    {
        if (!TryComp(uid, out VendingMachineComponent? vendingComponent))
            return;

        foreach (var keyValuePair in vendingComponent.Inventory)
        {
            if (keyValuePair.Value.Amount < 1)
            {
                vendingComponent.Inventory.Remove(keyValuePair.Key);
            }
        }
        RefreshUI(uid, vendingComponent);
    }
    private void RefreshUI(EntityUid uid, VendingMachineComponent vendingComponent)
    {
        //var state = new VendingMachineInterfaceState(_vendingMachineSystem.GetAllInventory(uid,vendingComponent)); // ST-TODO
        //_userInterfaceSystem.SetUiState(uid, VendingMachineUiKey.Key, state); // ST-TODO
    }

    private void OnInteractUsing(EntityUid uid, StalkerStorageComponent component, InteractUsingEvent args)
    {
        /*
        if (!TryComp(args.Used, out MetaDataComponent? metaData) ||
            !TryComp(uid, out VendingMachineComponent? vComponent) ||
            metaData.EntityPrototype?.ID == null)
            return;

        //Вставляем предметы(их EntityUid) которые находятся внутри контейнеров в этом предмете
        var itemsToAdd = GetAllContainersUidItems(args.Used);
        itemsToAdd.Add(args.Used);


        var convertedItemsToAdd = new List<object>(0);

        foreach (var item in itemsToAdd)
        {
            convertedItemsToAdd.AddRange(ConvertToIItemStalkerStorage(item));
        }

        foreach (var itemConverted in convertedItemsToAdd)
        {
            if (itemConverted is IItemStalkerStorage itemStalkerStorage)
            {
                AddToVendingMachineProtoByName(itemStalkerStorage.Identifier(),itemStalkerStorage.PrototypeName,vComponent,itemConverted);
            }
        }

        if (TryComp<ActorComponent>(args.User, out _))
        {
            SaveStorage((args.Target, vComponent));
        }

        QueueDel(args.Used);
        RefreshUI(uid,vComponent);
        */
    }

    //Добавить в VendingMachineComponent предмет по ключу и имени его прототипа
    private void AddToVendingMachineProtoByName(string keyIdentifier, string protoName, StalkerRepositoryComponent _stalkerRepositoryComponent, object stalkerItem)
    {
        RepositoryItemInfo NewRepositoryItemInfo = _stalkerRepositorySystem.GenerateItemInfoByPrototype(protoName);
        NewRepositoryItemInfo.SStorageData = stalkerItem;
        NewRepositoryItemInfo.Identifier = keyIdentifier;

        var Count = (int)((IItemStalkerStorage)stalkerItem).CountVendingMachine;
        if (Count < 1)
            return;

        _stalkerRepositorySystem.InsertToRepo((_stalkerRepositoryComponent.Owner, _stalkerRepositoryComponent), NewRepositoryItemInfo, Count);

        /*
        var countVendingMachine = ((IItemStalkerStorage) stalkerItem).CountVendingMachine;
        if (protoName == "" ||
            keyIdentifier == "")
            return;

        if (vendingComponent.Inventory.ContainsKey(keyIdentifier))
        {
            vendingComponent.Inventory[keyIdentifier].Amount += countVendingMachine;
            ((IItemStalkerStorage) vendingComponent.Inventory[keyIdentifier].SStorageData).CountVendingMachine += countVendingMachine;
        }
        else
        {
            vendingComponent.Inventory.Add(keyIdentifier,new VendingMachineInventoryEntry(InventoryType.Regular,protoName,countVendingMachine,stalkerItem));
        }
        */
    }

    #endregion

    //Сохранить инвентарь
    public void SaveStorage(StalkerRepositoryComponent _stalkerRepositoryComponent)
    {
        Console.WriteLine("SaveStorage");
        var inventory = new AllStorageInventory();
        foreach (var item in _stalkerRepositoryComponent.ContainedItems)
        {
            if (item.SStorageData is EmptyItemStalker)
                continue;

            if (item.SStorageData != null)
            {
                inventory.AllItems.Add(item.SStorageData);
            }

        }

        var json = InventoryToJson(inventory);

        _stalkerDbSystem.SetInventoryJson(_stalkerRepositoryComponent.StorageOwner, json);
    }

    public void ClearStorage(EntityUid uid)
    {
        if (!TryComp<StalkerRepositoryComponent>(uid, out var comp))
            return;

        comp.ContainedItems.Clear();
        comp.CurrentWeight = 0f;

        comp.LoadedDbJson = StalkerDbSystem.DefaultStalkerItems;
        LoadStalkerItemsByEntityUid(uid);

        _stalkerDbSystem.ClearInventoryJson(comp.StorageOwner);
    }

    public void ClearStorages(string? login)
    {
        if (login == null)
            return;

        var query = EntityQueryEnumerator<StalkerRepositoryComponent>();

        while (query.MoveNext(out var uid, out var stalkerRepositoryComponent))
        {
            if (stalkerRepositoryComponent.StorageOwner.EndsWith(login))
            {
                // Clear the repository
                stalkerRepositoryComponent.ContainedItems.Clear();

                // just in case, reset the current weight
                stalkerRepositoryComponent.CurrentWeight = 0;

                // Reset the loaded DB JSON to default items (2k roubles)
                stalkerRepositoryComponent.LoadedDbJson = StalkerDbSystem.DefaultStalkerItems;
                LoadStalkerItemsByEntityUid(uid);
            }
        }

        // Clear the repositories in db
        _stalkerDbSystem.ClearAllRepositories(login);
    }

    public static string InventoryToJson(AllStorageInventory inputAllStorageInventory)
    {
        return JsonSerializer.Serialize(inputAllStorageInventory);
    }

    private static AllStorageInventory InventoryFromJson(string jsonText)
    {
        var playerInventory = new AllStorageInventory();
        var parsed = JsonNode.Parse(jsonText);
        var jsonArray = parsed?["AllItems"]?.AsArray();

        if (jsonArray == null)
            return playerInventory;

        foreach (var node in jsonArray)
        {
            if (node == null)
                continue;

            object? newObject;
            switch (node["ClassType"]?.ToString())
            {
                case "SimpleItemStalker":
                    newObject = node.Deserialize<SimpleItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "StackItemStalker":
                    newObject = node.Deserialize<StackItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "BatteryItemStalker":
                    newObject = node.Deserialize<BatteryItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "AmmoItemStalker":
                    newObject = node.Deserialize<AmmoItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "SolutionItemStalker":
                    newObject = node.Deserialize<SolutionItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "AmmoContainerStalker":
                    newObject = node.Deserialize<AmmoContainerStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
                case "PaperItemStalker":
                    newObject = node.Deserialize<PaperItemStalker>();
                    if (newObject != null)
                        playerInventory.AllItems.Add(newObject);
                    break;
            }
        }
        return playerInventory;
    }

    //Получить количество незаспавненых снарядов/патронов из ентити (например из магазина АК47), возвращает количество и название снарядов/патронов
    public (int Count, string Name) GetAmmoUnSpawnedCount(EntityUid inputItemUid)
    {
        var returnProtoName = string.Empty;
        var returnAmmoUnSpawnedCount = 0;

        if (!TryComp(inputItemUid, out BallisticAmmoProviderComponent? ammoProvider))
            return (returnAmmoUnSpawnedCount, returnProtoName);

        returnAmmoUnSpawnedCount = ammoProvider.UnspawnedCount;

        if (ammoProvider.Proto != null)
            returnProtoName = ammoProvider.Proto;

        return (returnAmmoUnSpawnedCount, returnProtoName);
    }
}
