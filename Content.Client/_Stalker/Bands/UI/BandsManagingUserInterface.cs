using Content.Shared._Stalker.Bands;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using System;

namespace Content.Client._Stalker.Bands.UI
{
    [UsedImplicitly]
    public sealed class BandsManagingUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private BandsManagingWindow? _window;

        public BandsManagingUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = this.CreateWindow<BandsManagingWindow>();
            if (_window != null)
            {
                _window.OnAddMemberButtonPressed += AddMember;
                _window.OnRemoveMemberButtonPressed += RemoveMember;
                _window.OnBuyItemButtonPressed += BuyItem;
            }
        }

        private void AddMember(string memberName)
        {
            if (!string.IsNullOrWhiteSpace(memberName))
                SendMessage(new BandsManagingAddMemberMessage(memberName));
        }

        private void RemoveMember(Guid memberUserId)
        {
            SendMessage(new BandsManagingRemoveMemberMessage(memberUserId));
        }

        private void BuyItem(string itemId)
        {
            SendMessage(new BandsManagingBuyItemMessage(itemId));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            if (state is BandsManagingBoundUserInterfaceState castState)
                _window?.UpdateState(castState);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _window != null)
            {
                _window.OnAddMemberButtonPressed -= AddMember;
                _window.OnRemoveMemberButtonPressed -= RemoveMember;
                _window.OnBuyItemButtonPressed -= BuyItem;
                _window.Dispose();
            }
        }
    }
}
