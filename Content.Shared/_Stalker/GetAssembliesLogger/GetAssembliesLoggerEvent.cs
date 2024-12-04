using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.GetAssembliesLogger;
[Serializable, NetSerializable]
public class GetAssembliesLoggerEvent : EntityEventArgs{
    public string Message;

    public GetAssembliesLoggerEvent(string message){
        Message = message;
    }
}
