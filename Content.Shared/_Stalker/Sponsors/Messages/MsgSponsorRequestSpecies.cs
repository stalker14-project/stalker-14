using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Sponsors.Messages;

public sealed class MsgSponsorRequestSpecies : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    
    public List<string> AllowedSpecies = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadVariableInt32();
        using var ms = new MemoryStream();
        
        buffer.ReadAlignedMemory(ms, length);
        serializer.DeserializeDirect(ms, out AllowedSpecies);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        using var ms = new MemoryStream();
        
        serializer.SerializeDirect(ms, AllowedSpecies);
        buffer.WriteVariableInt32((int) ms.Length);
        ms.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}