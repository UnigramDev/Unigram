//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public enum ChatHistoryViewItemType
    {
        Outgoing,
        Incoming,
        Service,
        ServiceUnread,
        ServiceForumTopic,
        ServicePhoto,
        ServiceBirthdate,
        ServiceBackground,
        ServiceGift,
        ServiceGiftCode,
        ServiceUpgradedGift,
        ServiceUpgradedGiftPurchaseOffer,
        ServiceAccountInfo,
        ServiceNewThread,
    }

    public partial class ChatHistoryViewItem : ListViewItem
    {
        private readonly ChatHistoryView _owner;
        private ChatHistoryViewItemType _typeName;

        public ChatHistoryViewItem(ChatHistoryView owner, ChatHistoryViewItemType typeName)
        {
            _owner = owner;
            _typeName = typeName;
        }

        public ChatHistoryView Owner => _owner;

        public ChatHistoryViewItemType TypeName
        {
            get => _typeName;
            set => _typeName = value;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }

        private double _paddingTop;
        private double _paddingBottom;

        public void UpdatePadding(double top, double bottom)
        {
            var newTop = top >= 0 ? top : _paddingTop;
            var newBottom = bottom >= 0 ? bottom : _paddingBottom;

            if (_paddingTop != newTop || _paddingBottom != newBottom)
            {
                _paddingTop = newTop;
                _paddingBottom = newBottom;

                Padding = new Thickness(0, newTop, 0, newBottom);
            }
        }
    }

    public partial class TableAccessibleChatListViewItem : TableListViewItem
    {
        private readonly ListViewBase _parent;

        public TableAccessibleChatListViewItem(ListViewBase parent)
        {
            _parent = parent;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(_parent, this);
        }
    }

    public partial class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewBase _parent;
        private readonly ListViewItem _owner;

        public ChatListViewAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatListViewAutomationPeer(ListViewBase parent, ListViewItem owner)
            : base(owner)
        {
            _parent = parent;
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is MessageSelector selector)
            {
                var bubble = selector.Content as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.ContentTemplateRoot is MessageService service)
            {
                return AutomationProperties.GetName(service);
            }
            else if (_owner.ContentTemplateRoot is StackPanel panel && panel.Children.Count > 0)
            {
                if (panel.Children[0] is MessageService sservice)
                {
                    return AutomationProperties.GetName(sservice);
                }
            }

            var content = _parent?.ItemFromContainer(_owner);
            if (content is MessageWithOwner messageWithOwner)
            {
                return Automation.GetSummaryWithName(messageWithOwner, true);
            }

            return base.GetNameCore();
        }
    }

    public partial class ChatGridViewItem : GridViewItem
    {
        private readonly ListViewBase _parent;

        public ChatGridViewItem(ListViewBase parent)
        {
            _parent = parent;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatGridViewAutomationPeer(_parent, this);
        }
    }

    public partial class ChatGridViewAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly ListViewBase _parent;
        private readonly ChatGridViewItem _owner;

        public ChatGridViewAutomationPeer(ListViewBase parent, ChatGridViewItem owner)
            : base(owner)
        {
            _parent = parent;
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is MessageSelector selector)
            {
                var bubble = selector.Content as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }

            var content = _parent.ItemFromContainer(_owner);
            if (content is MessageWithOwner messageWithOwner)
            {
                return Automation.GetSummaryWithName(messageWithOwner, true);
            }

            return base.GetNameCore();
        }
    }
}
