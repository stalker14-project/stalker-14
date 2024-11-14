using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Stalker.CCCCVars;
using Robust.Server.Console;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Color = Robust.Shared.Maths.Color;

namespace Content.Server._Stalker.ServerAdministration;

public sealed class ServerApi : IPostInjectInit
{
    [Dependency] private readonly IStatusHost _statusHost = default!; // requests handler
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatMgr = default!; // Dispatching server messages
    [Dependency] private readonly ITaskManager _taskManager = default!; // Running on main thread, cause of wizdens' shitcode lol
    [Dependency] private readonly IServerDbManager _db = default!; // database operations, playtime, wl
    [Dependency] private readonly IPlayerLocator _loc = default!; // locating players for playtime
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!; // Playtime adding/removing
    [Dependency] private readonly IAdminManager _admin = default!; // To get all admins
    [Dependency] private readonly IServerConsoleHost _consoleHost = default!; // To execute commands on server

    private ISawmill _sawmill = default!;
    private string _token = default!;

    void IPostInjectInit.PostInject()
    {
        _sawmill = Logger.GetSawmill("serverApi");

        _statusHost.AddHandler(ActionWhitelistAdd);
        _statusHost.AddHandler(ActionPlayTimeRole);
        _statusHost.AddHandler(ActionGetAdmins);
        _statusHost.AddHandler(ActionSendInGameAnnouncement);
        _statusHost.AddHandler(ActionCommand);
    }

    public void Initialize()
    {
        _config.OnValueChanged(CCCCVars.ServerAPIToken, UpdateToken, true);
    }

    public void Shutdown()
    {
        _config.UnsubValueChanged(CCCCVars.ServerAPIToken, UpdateToken);
    }

    private void UpdateToken(string token)
    {
        _token = token;
    }

    #region WhitelistActions
    private async Task<bool> ActionWhitelistAdd(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/admin/whitelist/")
            return false;

        if (!CheckAccess(context))
        {
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        if (!context.RequestHeaders.TryGetValue("Action", out var action))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        if (!context.RequestHeaders.TryGetValue("Ckey", out var ckey))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        if (ckey.Count > 1)
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        var data = await _loc.LookupIdByNameAsync(ckey.ToString());
        if (data == null)
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }
        var guid = data.UserId;

        var isWhitelistedTask = await RunOnMainThread(() => _db.GetWhitelistStatusAsync(guid));
        var isWhitelisted = await isWhitelistedTask;
        switch (action)
        {
            case "add":
                if (isWhitelisted)
                {
                    await context.RespondErrorAsync(HttpStatusCode.Conflict);
                    return true;
                }
                await _db.AddToWhitelistAsync(guid);
                break;
            case "remove":
                if (!isWhitelisted)
                {
                    await context.RespondErrorAsync(HttpStatusCode.BadRequest);
                    return true;
                }
                await _db.RemoveFromWhitelistAsync(guid);
                break;
        }

        await context.RespondAsync("Success", HttpStatusCode.OK);
        return true;
    }
    #endregion

    #region PlayTimeActions
    private async Task<bool> ActionPlayTimeRole(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/admin/playtime/")
            return false;

        if (!CheckAccess(context))
        {
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        if (!context.RequestHeaders.TryGetValue("Action", out var action))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        var ckeySupplied = context.RequestHeaders.TryGetValue("Ckey", out var ckey);
        var jobIdSupplied = context.RequestHeaders.TryGetValue("JobId", out var jobId);
        var valueSupplied = context.RequestHeaders.TryGetValue("Value", out var value);
        if (!ckeySupplied || !jobIdSupplied || !valueSupplied)
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        var session = await RunOnMainThread(() =>
        {
            _playerManager.TryGetSessionByUsername(ckey.ToString(), out var session);
            return session;
        });

        if (session == null)
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        if (!int.TryParse(value, out var parsedVal))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        switch (action)
        {
            case "add":
                _taskManager.RunOnMainThread(() => _playTimeTracking.AddTimeToTracker(session, jobId.ToString(), TimeSpan.FromSeconds(parsedVal)));
                _taskManager.RunOnMainThread(() => _playTimeTracking.SaveSession(session));
                break;
            case "remove":
                _taskManager.RunOnMainThread(() => _playTimeTracking.AddTimeToTracker(session, jobId.ToString(), -TimeSpan.FromSeconds(parsedVal)));
                _taskManager.RunOnMainThread(() => _playTimeTracking.SaveSession(session));
                break;
        }

        await context.RespondAsync("Success", HttpStatusCode.OK);
        return true;
    }
    #endregion

    #region InfoActions

    private async Task<bool> ActionGetAdmins(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Get || context.Url.AbsolutePath != "/admin/info")
            return false;

        // I just don't want server to be DDoSed through this uri
        if (!CheckAccess(context))
        {
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        var content = await GetAdmins();
        await context.RespondAsync(content, 200, "text/plain; charset=utf-8");
        return true;
    }
    #endregion

    #region ControlActions

    private async Task<bool> ActionSendInGameAnnouncement(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/admin/send/")
            return false;
        if (!CheckAccess(context))
        {
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        var msgObj = await context.RequestBodyJsonAsync<MessageBody>();
        if (msgObj == null)
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        #region GettingColor

        Color converted;
        if (msgObj.Color == null)
        {
            msgObj.Color = "#34eb55";
            if (!Color.TryParse(msgObj.Color, out converted))
            {
                await context.RespondErrorAsync(HttpStatusCode.BadRequest);
                return true;
            }
        }
        else
        {
            if (!Color.TryParse(msgObj.Color, out converted))
            {
                await context.RespondErrorAsync(HttpStatusCode.BadRequest);
                return true;
            }
        }

        #endregion

        _taskManager.RunOnMainThread(() => _chatMgr.DispatchServerAnnouncement($"{msgObj.Sender}: {msgObj.Message}", converted));
        await context.RespondAsync("Success", HttpStatusCode.OK);
        return true;
    }

    #endregion

    #region CommandActions

    private async Task<bool> ActionCommand(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/admin/commands/")
            return false;

        if (!CheckAccess(context))
        {
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        if (!context.RequestHeaders.TryGetValue("Command", out var command))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        _taskManager.RunOnMainThread(() => _consoleHost.ExecuteCommand(command.ToString()));
        await context.RespondAsync("Success", 200);
        return true;
    }
    #endregion

    #region HelperMethods
    private bool CheckAccess(IStatusHandlerContext context)
    {
        var auth = context.RequestHeaders.TryGetValue("Authorization", out var authToken);

        if (!auth)
        {
            _sawmill.Info(@"Unauthorized access attempt to admin API. No auth header");
            return false;
        } // No auth header, no access

        if (authToken == _token)
            return true;

        // Invalid auth header, no access
        _sawmill.Info(@"Unauthorized access attempt to admin API. ""{0}""", authToken.ToString());
        return false;
    }

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();

        _taskManager.RunOnMainThread(() =>
        {
            taskCompletionSource.TrySetResult(func());
        });

        var result = await taskCompletionSource.Task;
        return result;
    }
    private async Task<string> GetAdmins()
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var admin in _admin.ActiveAdmins)
        {
            if (!first)
                sb.Append('\n');
            first = false;

            var adminData = _admin.GetAdminData(admin)!;

            sb.Append(admin.Name);
            if (adminData.Title is { } title)
                sb.Append($": [{title}]");
        }

        return sb.ToString();
    }
    #endregion
}

[Serializable]
public sealed class MessageBody
{
    public MessageBody(string sender, string message, string? color)
    {
        Sender = sender;
        Message = message;
        Color = color;
    }
    public string Sender { get; set; }
    public string Message { get; set; }
    public string? Color { get; set; }
}
