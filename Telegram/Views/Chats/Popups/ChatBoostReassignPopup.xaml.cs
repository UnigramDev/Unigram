//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Chats.Popups
{
    public sealed partial class ChatBoostReassignPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly Chat _chat;

        private DispatcherTimer _cooldownTimer;

        public ChatBoostReassignPopup(IClientService clientService, Chat chat, ChatBoostSlots slots)
        {
            InitializeComponent();

            _clientService = clientService;
            _chat = chat;

            var link = string.Format("[{0}](tg://premium_offer)", Strings.BoostingReassignBoostTextLink.Replace("**", string.Empty));
            var message = Locale.Declension(Strings.R.BoostingReassignBoostTextPluralWithLink, clientService.Options.PremiumGiftBoostCount, chat.Title, link);

            TextBlockHelper.SetMarkdown(MessageLabel, message);

            ScrollingHost.ItemsSource = slots.Slots
                .Where(x => x.CurrentlyBoostedChatId != chat.Id)
                .OrderBy(x => x.CooldownUntilDate != 0)
                .ToList();

            PurchaseCommand.IsEnabled = false;
            PurchaseText.Text = Strings.BoostingReassignBoost;

            Title = Strings.BoostingReassignBoosts;

            foreach (var slot in slots.Slots)
            {
                if (slot.CooldownUntilDate != 0)
                {
                    _cooldownTimer = new DispatcherTimer();
                    _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
                    _cooldownTimer.Tick += Cooldown_Tick;
                    _cooldownTimer.Start();

                    break;
                }
            }
        }

        private void Cooldown_Tick(object sender, object e)
        {
            var panel = ScrollingHost.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            var stop = true;

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ScrollingHost.ContainerFromIndex(i) as SelectorItem;

                var content = container.ContentTemplateRoot as ProfileCell;
                var slot = ScrollingHost.Items[i] as ChatBoostSlot;

                var diff = slot.CooldownUntilDate - DateTime.Now.ToTimestamp();
                if (diff > 0)
                {
                    content.Subtitle = string.Format(Strings.BoostingAvailableIn, diff.GetDuration());
                    stop = false;
                }
                else
                {
                    content.Subtitle = string.Format(Strings.BoostExpireOn, Formatter.Date(slot.ExpirationDate));
                    slot.CooldownUntilDate = 0;
                }

                container.IsEnabled = slot.CooldownUntilDate == 0;
            }

            if (stop)
            {
                _cooldownTimer.Stop();
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PurchaseCommand.IsEnabled = ScrollingHost.SelectedItems.Count > 0;
            PurchaseText.Text = ScrollingHost.SelectedItems.Count == 1
                ? Strings.BoostingReassignBoost
                : Strings.BoostingReassignBoosts;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateBoostSlot(_clientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private async void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var selected = ScrollingHost.SelectedItems
                .OfType<ChatBoostSlot>()
                .Select(x => x.SlotId)
                .ToList();

            var response = await _clientService.SendAsync(new BoostChat(_chat.Id, selected));
            if (response is not Error)
            {
                var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);
                aggregator.Publish(new UpdateConfetti());

                Hide();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _cooldownTimer?.Stop();
        }
    }
}
