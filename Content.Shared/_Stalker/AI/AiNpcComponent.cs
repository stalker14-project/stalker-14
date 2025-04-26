// Content.Shared/_Stalker/AI/AiNpcComponent.cs
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes; // Required for ProtoId
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype; // Required for ProtoId serializer
using System.Collections.Generic; // Required for List
using Robust.Shared.Serialization; // Required for NetSerializable

namespace Content.Shared._Stalker.AI
{
    // Define the unified rarity enum
    [Serializable, NetSerializable]
    public enum ItemRarity
    {
        Common,     // Low value, easily given or used in simple quests
        Uncommon,   // Medium value, might require some interaction or moderate quests
        Rare        // High value, likely requires significant quests or specific conditions
    }

    // Define the unified record for managed items
    [DataDefinition, Serializable, NetSerializable]
    public sealed partial class ManagedItemInfo
    {
        [DataField("protoId", required: true)] // Removed customTypeSerializer, let the system infer it
        public ProtoId<EntityPrototype> ProtoId { get; private set; } = default!;

        [DataField("maxQuantity")]
        public int MaxQuantity { get; private set; } = 1; // Max quantity NPC might handle/give at once

        [DataField("rarity")]
        public ItemRarity Rarity { get; private set; } = ItemRarity.Common;
    }


    [RegisterComponent, NetworkedComponent, Access(typeof(SharedAiNpcSystem))]
    public sealed partial class AiNpcComponent : Component
    {
        /// <summary>
        /// Base personality prompt or instructions for the AI model.
        /// </summary>
        [DataField("prompt"), ViewVariables(VVAccess.ReadWrite)]
        public string BasePrompt { get; private set; } = "You are a helpful NPC in a space station environment.";

        /// <summary>
        /// Maximum number of messages (user + assistant) to keep in history.
        /// </summary>
        [DataField("maxHistory"), ViewVariables(VVAccess.ReadWrite)]
        public int MaxHistory { get; private set; } = 10;

        /// <summary>
        /// A list defining items this NPC can potentially give out (e.g., as rewards, trade).
        /// The AI's TryGiveItem tool will be restricted to items in this list.
        /// </summary>
        [DataField("givableItems")]
        [ViewVariables(VVAccess.ReadWrite)]
        public List<ManagedItemInfo> GivableItems { get; private set; } = new();

        /// <summary>
        /// A list defining items relevant to quests this NPC might offer or be involved in
        /// (e.g., items the player needs to find, items the NPC needs).
        /// </summary>
        [DataField("questItems")]
        [ViewVariables(VVAccess.ReadWrite)]
        public List<ManagedItemInfo> QuestItems { get; private set; } = new();
    }
}
