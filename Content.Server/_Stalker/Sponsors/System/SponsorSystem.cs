using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server._Stalker.Sponsors.SponsorManager;
using Content.Server._Stalker.StalkerRepository;
using Content.Server.Administration;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared._Stalker.Sponsors;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors.System;

public sealed partial class SponsorSystem : EntitySystem
{
    [Dependency] private readonly IConsoleHost _consoleHost = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SponsorsManager _sponsors = default!;
    [Dependency] private readonly StalkerRepositorySystem _repositorySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _sponsorsEnabled = false;

    public override void Initialize()
    {
        _consoleHost.RegisterCommand("st_give_loadout", GiveSponsorLoadout, GetLoadoutCompletion);
        _consoleHost.RegisterCommand("st_give_contrib_loadout", GiveContribLoadout, GetContribCompletion);
        _consoleHost.RegisterCommand("st_is_given", IsGiven);
        _consoleHost.RegisterCommand("st_make_wipe", MakeWipe);

        _cfg.OnValueChanged(CCCCVars.DiscordAuthEnabled, val => _sponsorsEnabled = val, true);

        // debug
        _consoleHost.RegisterCommand("st_list_sponsors", ListSponsors);

        InitializeJobs();
        InitializeSpecies();
    }

    #region GiveLoadout
    [AdminCommand(AdminFlags.Host)]
    private void GiveSponsorLoadout(IConsoleShell shell, string argstr, string[] args)
    {
        // user-friendly
        var ckey = args[1];
        var strMode = args[0];

        if (!_playerManager.TryGetSessionByUsername(ckey, out var session))
            return;

        if (args.Length < 2)
        {
            shell.WriteError("Usage: give_loadout <mode> <ckey> <level>");
            return;
        }

        string? sponsorProtoId = null;
        var useLevelSpecified = args.Length == 3;
        if (useLevelSpecified)
            sponsorProtoId = args[2];

        if (!_sponsors.TryGetInfo(session.UserId, out var sponsorInfo) ||
            sponsorInfo.SponsorProtoId is null)
        {
            shell.WriteError($"User with ckey {ckey} is not sponsor");
            return;
        }

        SponsorPrototype? userSpecifiedSponsorPrototype = null;
        if (useLevelSpecified && sponsorProtoId is not null)
            userSpecifiedSponsorPrototype = _prototype.Index<SponsorPrototype>(sponsorProtoId);

        var sponsorPrototypeIndex = _prototype.Index(sponsorInfo.SponsorProtoId.Value);

        if (userSpecifiedSponsorPrototype is not null &&
            userSpecifiedSponsorPrototype.SponsorPriority > sponsorPrototypeIndex.SponsorPriority)
        {
            shell.WriteError($"User with login {ckey} have {sponsorPrototypeIndex.SponsorPriority} priority but you tried to give {userSpecifiedSponsorPrototype.SponsorPriority}");
            return;
        }

        if (!Enum.TryParse<LoadoutGiveMode>(strMode, out var mode))
        {
            shell.WriteError("Unable to parse first argument");
            return;
        }

        List<EntProtoId> items;
        if (!useLevelSpecified)
        {
            var rawItems = sponsorPrototypeIndex.RepositoryItems.ToList();
            items = rawItems.ConvertAll(a => new EntProtoId(a));
        }
        else if (useLevelSpecified && userSpecifiedSponsorPrototype is not null)
        {
            var rawItems = userSpecifiedSponsorPrototype.RepositoryItems.ToList();
            items = rawItems.ConvertAll(a => new EntProtoId(a));
        }
        else
        {
            shell.WriteError("Unable to find items to give");
            return;
        }

        switch (mode)
        {
            case LoadoutGiveMode.Hands:
            {
                if (GiveHands(ckey, items, out var reason))
                {
                    shell.WriteLine($"Successfully gave loadout to {ckey}");
                    return;
                }
                shell.WriteError(reason);
                return;
            }
            case LoadoutGiveMode.Repository:
            {
                if (GiveRepository(ckey, items, out var reason))
                {
                    shell.WriteLine($"Successfully gave loadout to {ckey}");
                    return;
                }
                shell.WriteError(reason);
                return;
            }
            default:
            {
                shell.WriteError("Invalid mode selected");
                return;
            }
        }
    }

    private CompletionResult GetLoadoutCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
            {
                var values = Enum.GetValues<LoadoutGiveMode>().ToList();
                var options = new List<CompletionOption>();
                foreach (var val in values)
                {
                    options.Add(new CompletionOption(val.ToString()));
                }
                return CompletionResult.FromHintOptions(options, "<mode>");
            }
            case 2:
                return CompletionResult.FromHint("<ckey>");
            case 3:
                return CompletionResult.FromHint("<level>?");
            default:
                return CompletionResult.Empty;
        }
    }
    private bool GiveRepository(string owner, List<EntProtoId> items,
        [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        // find repository
        var repos = EntityQueryEnumerator<StalkerRepositoryComponent>();
        Entity<StalkerRepositoryComponent>? entity = null;
        while (repos.MoveNext(out var uid, out var comp))
        {
            if (comp.StorageOwner != owner)
                continue;
            entity = (uid, comp);
        }

        if (entity == null)
        {
            reason = $"Not found user's repository with ckey {owner}";
            return false;
        }
        _repositorySystem.GiveLoadout(entity.Value, items);
        return true;
    }

    private bool GiveHands(string owner, List<EntProtoId> items,
        [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (!_playerManager.TryGetSessionByUsername(owner, out var session))
        {
            reason = $"Player with ckey {owner} not found";
            return false;
        }
        var entity = session.AttachedEntity;
        if (entity == null || HasComp<GhostComponent>(entity))
        {
            reason = $"Player with ckey {owner} has no attached entity or its a ghost";
            return false;
        }

        foreach (var ent in items)
        {
            var playerCoords = Transform(entity.Value).Coordinates;
            var spawned = Spawn(ent, playerCoords);
            _hands.PickupOrDrop(entity.Value, spawned, animate: false);
        }
        return true;
    }
    #endregion

    #region ContribLoadout
    [AdminCommand(AdminFlags.Host)]
    private void GiveContribLoadout(IConsoleShell shell, string argstr, string[] args)
    {
        var strMode = args[0];
        var ckey = args[1];

        if (_sponsors.ContributorPrototype is null)
        {
            shell.WriteError("There are no contributor prototype defined...");
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(ckey, out var session))
            return;

        if (args.Length < 2)
        {
            shell.WriteError("Usage: give_loadout <mode> <ckey>");
            return;
        }
        if (!_sponsors.TryGetInfo(session.UserId, out var sponsorInfo))
        {
            shell.WriteError($"User with ckey {ckey} is not sponsor");
            return;
        }

        if (!sponsorInfo.Contributor)
        {
            shell.WriteError("User is not contributor");
            return;
        }

        if (!Enum.TryParse<LoadoutGiveMode>(strMode, out var mode))
        {
            shell.WriteError("Unable to parse first argument");
            return;
        }


        var rawItems = _sponsors
            .ContributorPrototype
            .ContributorItems
            .ToList();

        var items = rawItems.ConvertAll(a => new EntProtoId(a));

        switch (mode)
        {
            case LoadoutGiveMode.Hands:
            {
                if (GiveHands(ckey, items, out var reason))
                {
                    shell.WriteLine($"Successfully gave loadout to {ckey}");
                    return;
                }
                shell.WriteError(reason);
                return;
            }
            case LoadoutGiveMode.Repository:
            {
                if (GiveRepository(ckey, items, out var reason))
                {
                    shell.WriteLine($"Successfully gave loadout to {ckey}");
                    return;
                }
                shell.WriteError(reason);
                return;
            }
            default:
            {
                shell.WriteError("Invalid mode selected");
                return;
            }
        }
    }

    private CompletionResult GetContribCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
            {
                var values = Enum.GetValues<LoadoutGiveMode>().ToList();
                var options = new List<CompletionOption>();
                foreach (var val in values)
                {
                    options.Add(new CompletionOption(val.ToString()));
                }
                return CompletionResult.FromHintOptions(options, "<mode>");
            }
            case 2:
            {
                return CompletionResult.FromHint("<ckey>");
            }
            default:
            {
                return CompletionResult.Empty;
            }
        }
    }
    #endregion

    #region Given
    [AdminCommand(AdminFlags.Debug)]
    public void IsGiven(IConsoleShell shell, string args, string[] argv)
    {
        string ckey;
        if (argv.Length < 1)
        {
            if (shell.Player is null)
            {
                shell.WriteLine("Usage: is_given <ckey>");
                return;
            }
            ckey = shell.Player.Name;
        }
        else
        {
            ckey = argv[0];
        }

        if (!_playerManager.TryGetSessionByUsername(ckey, out var session))
        {
            shell.WriteError($"Unable to find session with ckey: {ckey}");
            return;
        }

        if (!_sponsors.TryGetInfo(session.UserId, out var data))
        {
            shell.WriteError($"{ckey} is not a sponsor.");
            return;
        }

        shell.WriteLine($"{ckey} status is: {(data.IsGiven ? "GIVEN" : "NOT GIVEN")}");
    }

    #endregion

    #region Wipe

    [AdminCommand(AdminFlags.Host)]
    public void MakeWipe(IConsoleShell shell, string argstr, string[] argv)
    {
        Task.Run(() => _sponsors.MakeWipe());
        shell.WriteLine("Wipe request sent!");
    }

    #endregion

    #region Debug

    [AdminCommand(AdminFlags.Debug)]
    public void ListSponsors(IConsoleShell shell, string argStr, string[] argv)
    {
        var sponsors = _sponsors.Sponsors;

        var builder = new StringBuilder();

        foreach (var (userId, sponsorData) in sponsors)
        {
            var playerData = _playerManager.GetPlayerData(userId);
            builder.Append(
                $"Name: {playerData.UserName} | UserId: {userId} | Data: {sponsorData.SponsorProtoId ?? "NONE"}, IsContributor: {sponsorData.Contributor}\n");
        }

        shell.WriteLine(builder.ToString());
    }

    #endregion
}

public enum LoadoutGiveMode : byte
{
    Hands,
    Repository
}
