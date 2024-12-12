using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Stalker.Storage;

public interface IItemStalkerStorage
{
    string ClassType { get; set; }
    string PrototypeName { get; set; }
    string Identifier();
    uint CountVendingMachine { get; set; }
}

[Serializable, NetSerializable]
public class SpecialLoadClass : IItemStalkerStorage
{
    public string ClassType { get; set; } = "SpecialLoadClass";
    public string PrototypeName { get; set; } = "SpecialLoadClassPrototypeName";

    public uint CountVendingMachine { get; set; } = 0u;
    public string Identifier()
    {
        return ClassType + "_" + "_" + PrototypeName + "_" + "SpecialLoadClassIdentifier";
    }

}

[Serializable, NetSerializable]
public class AllStorageInventory
{
    //public string Login = "Empty";
    public List<object> AllItems { get; set; } = new List<object>(0);

}


[Serializable, NetSerializable]
public class EmptyItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "EmptyItemStalker";
    public string PrototypeName { get; set; } = "EmptyItemStalker";

    public uint CountVendingMachine { get; set; } = 0u;

    public string Identifier()
    {
        return "EmptyItemStalker";
    }
}

[Serializable, NetSerializable]
public class SimpleItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "SimpleItemStalker";
    public string PrototypeName { get; set; } = "";

    public uint CountVendingMachine { get; set; }

    public SimpleItemStalker(string prototypeName = "", uint CountVendingMachine = 1)
    {
        PrototypeName = prototypeName;
        this.CountVendingMachine = CountVendingMachine;
    }

    public string Identifier()
    {
        return "S_" + PrototypeName;
    }

}


[Serializable, NetSerializable]
public class PaperItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "PaperItemStalker";
    public string PrototypeName { get; set; } = "";
    public uint CountVendingMachine { get; set; }
    public string Content { get; set; } = "";
    public int ContentSize { get; set; } = 0;
    public List<StampStalkerData> ListStampStalkerData { get; set; } = new(0);
    public string StampState { get; set; } = "";

    private string SavedIdentifier = "";

    public PaperItemStalker(string prototypeName, uint countVendingMachine, string content, int contentSize)
    {
        PrototypeName = prototypeName;
        CountVendingMachine = countVendingMachine;
        Content = content;
        ContentSize = contentSize;
    }

    string Hash(string input)
    {
        if (input == null)
            return "";
        return "" + input.GetHashCode();
    }

    public string Identifier()
    {
        if (SavedIdentifier != "")
        {
            return SavedIdentifier;
        }

        string StampsDataString = "";

        foreach (var OneStamp in ListStampStalkerData)
        {
            StampsDataString += "SN=" + OneStamp.StampedName + "_SC=" + OneStamp.PaperColorStalkerData.R + "_" + OneStamp.PaperColorStalkerData.G + "_" + OneStamp.PaperColorStalkerData.B + "_" + OneStamp.PaperColorStalkerData.A + "#";
        }

        string Return = "P_" + PrototypeName + "_HASHTEXT=" + Hash(Content) + "_CS=" + ContentSize + "_SS=" + StampState + "_STAMPS=" + StampsDataString;

        SavedIdentifier = Return;

        return SavedIdentifier;
    }

    [Serializable, NetSerializable]
    public class StampStalkerData
    {
        public string StampedName { get; set; } = "";
        public StampColorStalkerData PaperColorStalkerData { get; set; } = new StampColorStalkerData(0f, 0f, 0f, 0f);

        public StampStalkerData(string stampedName, StampColorStalkerData paperColorStalkerData)
        {
            StampedName = stampedName;
            PaperColorStalkerData = paperColorStalkerData;
        }
    }
    [Serializable, NetSerializable]
    public class StampColorStalkerData
    {
        public float R { get; set; } = 0f;
        public float G { get; set; } = 0f;
        public float B { get; set; } = 0f;
        public float A { get; set; } = 0f;

        public StampColorStalkerData(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }


}

[Serializable, NetSerializable]
public sealed class BatteryItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "BatteryItemStalker";
    public string PrototypeName { get; set; } = "";
    public uint CountVendingMachine { get; set; }
    public float CurrentCharge { get; set; }

    public string Identifier()
    {
        return PrototypeName + "_" + CurrentCharge;
    }

    public BatteryItemStalker(float CurrentCharge = 100, string PrototypeName = "", uint CountVendingMachine = 1)
    {
        this.CurrentCharge = CurrentCharge;
        this.PrototypeName = PrototypeName;
        this.CountVendingMachine = CountVendingMachine;
    }
}
[Serializable, NetSerializable]
public sealed class SolutionItemStalker : IItemStalkerStorage
{
    public SolutionItemStalker(Dictionary<string, List<ReagentQuantity>> Contents, string PrototypeName, FixedPoint2 Volume, uint CountVendingMachine = 1)
    {
        this.Contents = Contents;
        this.PrototypeName = PrototypeName;
        this.CountVendingMachine = CountVendingMachine;
        this.Volume = Volume;
    }

    public string ClassType { get; set; } = "SolutionItemStalker";
    public string PrototypeName { get; set; } = "";
    public uint CountVendingMachine { get; set; }
    public Dictionary<string, List<ReagentQuantity>> Contents { get; set; } = new();
    public FixedPoint2 Volume { get; set; } // Needed for solution correct consuming

    public string Identifier()
    {
        var contentsString = string.Join(", ", Contents.Select(kv => $"{kv.Key}: [{string.Join(", ", kv.Value)}]"));
        return $"{PrototypeName}_{contentsString}_{Volume}";
    }

}

[Serializable, NetSerializable]
public class StackItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "StackItemStalker";
    public string PrototypeName { get; set; } = "";
    public uint StackCount { get; set; } = 0;
    public uint CountVendingMachine { get; set; } = 0u;

    public StackItemStalker(string prototypeName = "", uint CountVendingMachine = 1, uint stackCount = 1)
    {
        PrototypeName = prototypeName;
        this.CountVendingMachine = CountVendingMachine;
        StackCount = stackCount;
    }

    public string Identifier()
    {
        return PrototypeName + "_" + StackCount;
    }
}
[Serializable, NetSerializable]
public sealed class AmmoContainerStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "AmmoContainerStalker";
    public string PrototypeName { get; set; }
    public string? AmmoPrototypeName { get; set; }
    public int AmmoCount { get; set; }
    public uint CountVendingMachine { get; set; }
    public List<EntProtoId> EntProtoIds { get; set; }
    public AmmoContainerStalker(string prototypeName, string? ammoPrototypeName, List<EntProtoId> entProtoIds, int ammoCount = 1, uint countVendingMachine = 1)
    {
        PrototypeName = prototypeName;
        AmmoPrototypeName = ammoPrototypeName;
        AmmoCount = ammoCount;
        CountVendingMachine = countVendingMachine;
        EntProtoIds = entProtoIds;
    }

    public string Identifier()
    {
        return PrototypeName + "_" + AmmoPrototypeName + "_" + AmmoCount;
    }
}
[Serializable, NetSerializable]
public sealed class AmmoItemStalker : IItemStalkerStorage
{
    public string ClassType { get; set; } = "AmmoItemStalker";
    public string PrototypeName { get; set; } = "";
    public bool Exhausted { get; set; }
    public uint CountVendingMachine { get; set; }

    public AmmoItemStalker(string prototypeName, bool exhausted, uint countVendingMachine = 1)
    {
        PrototypeName = prototypeName;
        Exhausted = exhausted;
        CountVendingMachine = countVendingMachine;
    }

    public string Identifier()
    {
        return $"{PrototypeName}_{Exhausted}";
    }
}
