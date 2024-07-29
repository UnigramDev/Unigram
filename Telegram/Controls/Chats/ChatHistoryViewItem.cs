//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Td.Api;
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
        ServicePhoto,
        ServiceBackground,
        ServiceGift,
        ServiceGiftCode
    }

    public class ChatHistoryViewItem : ListViewItemEx
    {
        private readonly ChatHistoryView _owner;
        private ChatHistoryViewItemType _typeName;

        public ChatHistoryViewItem(ChatHistoryView owner, ChatHistoryViewItemType typeName)
        {
            _owner = owner;
            _typeName = typeName;
        }

        public ChatHistoryViewItemType TypeName
        {
            get => _typeName;
            set => _typeName = value;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }
    }

    public class AccessibleChatListViewItem : ListViewItem
    {
        private readonly IClientService _clientService;

        public AccessibleChatListViewItem()
        {

        }

        public AccessibleChatListViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this, _clientService);
        }
    }

    public class TableAccessibleChatListViewItem : TableListViewItem
    {
        private readonly IClientService _clientService;

        public TableAccessibleChatListViewItem()
        {

        }

        public TableAccessibleChatListViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this, _clientService);
        }
    }

    public class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewItem _owner;
        private readonly IClientService _clientService;

        public ChatListViewAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatListViewAutomationPeer(ListViewItem owner, IClientService clientService)
            : base(owner)
        {
            _owner = owner;
            _clientService = clientService;
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

            return base.GetNameCore();
        }
    }

    public class ChatGridViewItem : GridViewItem
    {
        private readonly IClientService _clientService;

        public ChatGridViewItem()
        {

        }

        public ChatGridViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatGridViewAutomationPeer(this, _clientService);
        }
    }

    public class ChatGridViewAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly ChatGridViewItem _owner;
        private readonly IClientService _clientService;

        public ChatGridViewAutomationPeer(ChatGridViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatGridViewAutomationPeer(ChatGridViewItem owner, IClientService clientService)
            : base(owner)
        {
            _owner = owner;
            _clientService = clientService;
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
            else if (_owner.Content is Message message && _clientService != null)
            {
                return Automation.GetDescription(_clientService, message);
            }

            return base.GetNameCore();
        }
    }
}
