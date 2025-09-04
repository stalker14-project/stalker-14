using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.StalkerRepository;

[Serializable, NetSerializable]
public enum StalkerRepositoryUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class RepositoryUpdateState : BoundUserInterfaceState
{
    public List<RepositoryItemInfo> Items;
    public List<RepositoryItemInfo> UserItems;
    public float MaxWeight;
    public RepositoryUpdateState(List<RepositoryItemInfo> items, List<RepositoryItemInfo> userItems, float maxWeight)
    {
        Items = items;
        UserItems = userItems;
        MaxWeight = maxWeight;
    }
}

[Serializable, NetSerializable]
public sealed class InjectRequestedState : BoundUserInterfaceState
{
    public RepositoryItemInfo Item;

    public InjectRequestedState(RepositoryItemInfo item)
    {
        Item = item;
    }
}

[Serializable, NetSerializable]
public sealed class RequestUpdateRepositoryMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class RepositoryEjectMessage : BoundUserInterfaceMessage
{
    public RepositoryItemInfo Item;
    public int Count;

    public RepositoryEjectMessage(RepositoryItemInfo item, int count)
    {
        Item = item;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class RepositoryInjectFromUserMessage : BoundUserInterfaceMessage
{
    public RepositoryItemInfo Item;
    public int Count;

    public RepositoryInjectFromUserMessage(RepositoryItemInfo item, int count)
    {
        Item = item;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class RepositoryInjectAllMessage : BoundUserInterfaceMessage
{

}
