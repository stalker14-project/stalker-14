using System.Collections.Concurrent;
using System.Threading.Tasks;
using Content.Server._Stalker.Teleports.DuplicateTeleport;
using Content.Server.Database;
using Content.Shared.GameTicking;
using Content.Shared._Stalker.Teleport;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.StalkerDB;

// TODO: Idk if it should be cached, probably remove caching to avoid 635 DB operations on start???
public sealed class StalkerDbSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    public const string DefaultStalkerItems =
"""
{
  "AllItems": [
    {
      "ClassType": "StackItemStalker",
      "PrototypeName": "Roubles",
      "StackCount": 3000,
      "CountVendingMachine": 1
    },
    {
      "ClassType": "SimpleItemStalker",
      "PrototypeName": "StalkerHunterCrate",
      "CountVendingMachine": 1
    },
    {
      "ClassType": "SimpleItemStalker",
      "PrototypeName": "StalkerArtefactHunterCrate",
      "CountVendingMachine": 1
    },
    {
      "ClassType": "SimpleItemStalker",
      "PrototypeName": "StalkerBasedCrate",
      "CountVendingMachine": 1
    }
  ]
}
""";

    // login - json
    public ConcurrentDictionary<string, string> Stalkers = new();
    private List<string> _symbols = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializeGroupRecords();
        LoadPrototypes();
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(BeforeSpawn);
    }

    #region InventoryOperations

    private void LoadPrototypes()
    {
        var prototypes = _prototype.EnumeratePrototypes<DuplicateSymbolsPrototype>();
        foreach (var proto in prototypes)
        {
            _symbols.AddRange(proto.Symbols);
        }
    }
    public void InitializeGroupRecords()
    {
        var prototypes = _prototype.EnumeratePrototypes<StalkerBandPrototype>();
        foreach (var prototype in prototypes)
        {
            foreach (var item in prototype.BandTeleports)
            {
                _ = LoadPlayer(item, false);
            }
        }
    }
    public string GetInventoryJson(string login)
    {
        return !Stalkers.TryGetValue(login, out var value) ? "" : value;
    }

    public void SetInventoryJson(string login, string inputInventoryJson)
    {
        Stalkers[login] = inputInventoryJson;
        _dbManager.SetLoginItems(login, inputInventoryJson);
    }

    public void ClearAllRepositories(string login)
    {
        _dbManager.SetAllLoginItems(login, DefaultStalkerItems);
    }

    public void ClearInventoryJson(string login)
    {
       _dbManager.SetLoginItems(login, DefaultStalkerItems);
    }

    private async Task LoadPlayer(string login, bool loadSymbols = true)
    {
        if (Stalkers.ContainsKey(login))
            return;

        var record = await _dbManager.GetLoginItems(login) ?? DefaultStalkerItems;
        Stalkers.TryAdd(login, record);
        if (loadSymbols)
            await LoadSymbolsPlayers(login);
        var ev = new NewRecordAddedEvent(login);
        if (await _dbManager.EnsureRecordCreated(login, DefaultStalkerItems))
            RaiseLocalEvent(ref ev);
    }

    private async Task LoadSymbolsPlayers(string login)
    {
        foreach (var item in _symbols)
        {
            var concat = item + login;
            if (Stalkers.ContainsKey(concat))
                continue;
            var ev = new NewRecordAddedEvent(concat);
            if (await _dbManager.EnsureRecordCreated(concat, DefaultStalkerItems))
                RaiseLocalEvent(ref ev);

            var items = await _dbManager.GetLoginItems(concat) ?? DefaultStalkerItems;

            Stalkers.TryAdd(concat, items);
        }
    }

    #endregion

    private async void BeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        await LoadPlayer(ev.Player.Name);
    }
}

[ByRefEvent]
public record struct NewRecordAddedEvent(string Login);
