using Content.Shared.Database;
using Content.Shared._Stalker.GetAssembliesLogger;
using Content.Server.Administration.Logs;

namespace Content.Server._Stalker.GetAssembliesLogger;

public abstract class GetAssembliesLogger : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<GetAssembliesLoggerEvent>(OnMessageRecieved);

    }

    private void OnMessageRecieved(GetAssembliesLoggerEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } ConnectedClient)
            return;
        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme,$"{args.SenderSession.Name} test text");
    }
}


