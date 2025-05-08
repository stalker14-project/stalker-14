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
        /// Whether the NPC can use the TryChat tool.
        /// </summary>
        [DataField("enabled"), ViewVariables(VVAccess.ReadWrite)]
        public bool Enabled { get; private set; } = false;
        /// <summary>
        /// If true, this NPC will only process interactions from players with sponsor status (priority join).
        /// </summary>
        [DataField("sponsorOnly"), ViewVariables(VVAccess.ReadWrite)]
        public bool SponsorOnly { get; private set; } = true;

        /// <summary>
        /// Whether the NPC can use the TryChat tool.
        /// </summary>
        [DataField("canChat"), ViewVariables(VVAccess.ReadWrite)]
        public bool CanChat { get; private set; } = true;

        /// <summary>
        /// Maximum number of messages (user + assistant + tool) to keep in history *per player*.
        /// </summary>
        [DataField("maxHistoryPerPlayer"), ViewVariables(VVAccess.ReadWrite)]
        public int MaxHistoryPerPlayer { get; private set; } = 20; // Increased default slightly

        /// <summary>
        /// Range on which NPC will "hear" and communicate with the player
        /// </summary>
        [DataField("interactionRange"), ViewVariables(VVAccess.ReadWrite)]
        public float InteractionRange { get; private set; } = 2;

        /// <summary>
        /// Whether the NPC can use the TryGiveItem tool. Requires GivableItems to be populated.
        /// </summary>
        [DataField("canGiveItems"), ViewVariables(VVAccess.ReadWrite)]
        public bool CanGiveItems { get; private set; } = false;

        /// <summary>
        /// A list defining items this NPC can potentially give out (e.g., as rewards, trade).
        /// The AI's TryGiveItem tool will be restricted to items in this list.
        /// </summary>
        [DataField("givableItems")]
        [ViewVariables(VVAccess.ReadWrite)]
        public List<ManagedItemInfo> GivableItems { get; private set; } = new();

        /// <summary>
        /// Whether the NPC can use the TryOfferQuest tool. Requires QuestItems to be populated.
        /// </summary>
        [DataField("canOfferQuests"), ViewVariables(VVAccess.ReadWrite)]
        public bool CanOfferQuests { get; private set; } = false;

        /// <summary>
        /// A list defining items relevant to quests this NPC might offer or be involved in
        /// (e.g., items the player needs to find, items the NPC needs).
        /// </summary>
        [DataField("questItems")]
        [ViewVariables(VVAccess.ReadWrite)]
        public List<ManagedItemInfo> QuestItems { get; private set; } = new();

        /// <summary>
        /// Whether the NPC can use the TryTakeItem tool.
        /// </summary>
        [DataField("canTakeItems"), ViewVariables(VVAccess.ReadWrite)]
        public bool CanTakeItems { get; private set; } = false;

        /// <summary>
        /// Damage specifier applied when the TryPunishPlayer tool is used.
        /// If null, no damage is applied.
        /// </summary>
        [DataField("punishmentDamage")]
        [ViewVariables(VVAccess.ReadWrite)]
        public Shared.Damage.DamageSpecifier? PunishmentDamage { get; private set; }

        /// <summary>
        /// Whether the NPC can use the TryPunishPlayer tool. Requires PunishmentDamage or PunishmentSound to be set.
        /// </summary>
        [DataField("canPunish"), ViewVariables(VVAccess.ReadWrite)]
        public bool CanPunish { get; private set; } = false;

        /// <summary>
        /// Sound played when the TryPunishPlayer tool is used.
        /// </summary>
        [DataField("punishmentSound")]
        [ViewVariables(VVAccess.ReadWrite)]
        public Robust.Shared.Audio.SoundSpecifier? PunishmentSound { get; private set; }

        /// <summary>
        /// Entities matching this whitelist will not be affected by the TryPunishPlayer tool.
        /// </summary>
        [DataField("punishmentWhitelist")]
        [ViewVariables(VVAccess.ReadWrite)]
        public Shared.Whitelist.EntityWhitelist? PunishmentWhitelist { get; private set; }
    }
}
