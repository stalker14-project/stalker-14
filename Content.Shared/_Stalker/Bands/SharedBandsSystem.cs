using System;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Network;
using Content.Shared._Stalker.Bands;

namespace Content.Shared._Stalker.Bands
{
    // UI key for bands managing UI
    [Serializable, NetSerializable]
    public enum BandsUiKey
    {
        Key = 0
    }
    [Virtual]
    public class SharedBandsSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly IPrototypeManager _protoManager = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string? BandName { get; }
        public int MaxMembers { get; }
        public List<BandMemberInfo> Members { get; }
        public bool CanManage { get; }
        public List<WarZoneInfo> WarZones { get; }
        public List<BandPointsInfo> BandPoints { get; }
        public List<BandShopItem> ShopItems { get; } // Added shop items

        public BandsManagingBoundUserInterfaceState(
            string? bandName,
            int maxMembers,
            List<BandMemberInfo> members,
            bool canManage,
            List<WarZoneInfo>? warZones,
            List<BandPointsInfo>? bandPoints,
            List<BandShopItem>? shopItems) // Added shop items
        {
            BandName = bandName;
            MaxMembers = maxMembers;
            Members = members; // Assuming members is never null based on existing code
            CanManage = canManage;
            WarZones = warZones ?? new List<WarZoneInfo>();
            BandPoints = bandPoints ?? new List<BandPointsInfo>();
            ShopItems = shopItems ?? new List<BandShopItem>(); // Added shop items
        }
    }

    // --- New Data Structures for War Zone Tab ---

    [Serializable, NetSerializable]
    public sealed class WarZoneInfo
    {
        public string ZoneId { get; }
        public string Owner { get; }
        public float Cooldown { get; } // In seconds
        public string Attacker { get; }
        public string Defender { get; }
        public float Progress { get; } // 0.0 to 1.0. TODO: Not implemented yet

        public WarZoneInfo(string zoneId, string owner, float cooldown, string attacker, string defender, float progress)
        {
            ZoneId = zoneId;
            Owner = owner;
            Cooldown = cooldown;
            Attacker = attacker;
            Defender = defender;
            Progress = progress;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandPointsInfo
    {
        public string BandProtoId { get; }
        public string BandName { get; }
        public float Points { get; }

        public BandPointsInfo(string bandProtoId, string bandName, float points)
        {
            BandProtoId = bandProtoId;
            BandName = bandName;
            Points = points;
        }
    }

    // --- Existing Data Structures ---

    [Serializable, NetSerializable]
    public sealed class BandMemberInfo
    {
        public NetUserId UserId { get; }
        public string PlayerName { get; } // Keep this for now (ckey)
        public string CharacterName { get; } // Add this field
        public string RoleId { get; }

        public BandMemberInfo(NetUserId userId, string playerName, string characterName, string roleId)
        {
            UserId = userId;
            PlayerName = playerName;
            CharacterName = characterName;
            RoleId = roleId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingAddMemberMessage : BoundUserInterfaceMessage
    {
        public string PlayerName { get; }

        public BandsManagingAddMemberMessage(string playerName)
        {
            PlayerName = playerName;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingRemoveMemberMessage : BoundUserInterfaceMessage
    {
        public Guid PlayerUserId { get; }

        public BandsManagingRemoveMemberMessage(Guid playerUserId)
        {
            PlayerUserId = playerUserId;
        }
    }

    // --- New Message for Buying Items ---
    [Serializable, NetSerializable]
    public sealed class BandsManagingBuyItemMessage : BoundUserInterfaceMessage
    {
        public string ItemId { get; } // The ProductEntity ID of the item to buy

        public BandsManagingBuyItemMessage(string itemId)
        {
            ItemId = itemId;
        }
    }
}

