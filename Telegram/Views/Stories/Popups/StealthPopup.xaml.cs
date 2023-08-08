//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Stories.Popups
{
    public sealed partial class StealthPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly DispatcherTimer _cooldownTimer;

        enum StealthModeFeature
        {
            HideRecentViews,
            HideNextViews
        }

        public StealthPopup(IClientService clientService, PremiumPaymentOption option)
        {
            InitializeComponent();

            _clientService = clientService;

            _cooldownTimer = new DispatcherTimer();
            _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
            _cooldownTimer.Tick += CooldownTimer_Tick;

            if (clientService.StealthMode.CooldownUntilDate > 0)
            {
                _cooldownTimer.Start();
            }

            RequestedTheme = ElementTheme.Dark;

            ScrollingHost.ItemsSource = new StealthModeFeature[]
            {
                StealthModeFeature.HideRecentViews,
                StealthModeFeature.HideNextViews,
            };

            if (clientService.IsPremium)
            {
                Subtitle.Text = Strings.StealthModeHint;
                UpdateCooldownTimer();
            }
            else
            {
                Subtitle.Text = Strings.StealthModePremiumHint;
                PurchaseCommand.Content = Strings.UnlockStealthMode;
            }

            Closing += OnClosing;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _cooldownTimer.Stop();
        }

        private void CooldownTimer_Tick(object sender, object e)
        {
            UpdateCooldownTimer();
        }

        private void UpdateCooldownTimer()
        {
            if (_clientService.StealthMode.CooldownUntilDate == 0)
            {
                _cooldownTimer.Stop();
                PurchaseCommand.Content = Strings.EnableStealthMode;
            }
            else
            {
                var untilDate = Converters.Formatter.ToLocalTime(_clientService.StealthMode.CooldownUntilDate);
                var timeLeft = untilDate - DateTime.Now;

                PurchaseCommand.Content = string.Format(Strings.AvailableIn, timeLeft.ToString("h\\:mm\\:ss"));
            }
        }

        public bool ShouldPurchase { get; private set; }
        public bool Activated { get; private set; }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case StealthModeFeature.HideRecentViews:
                    iconValue = Icons.Rewind524;
                    titleValue = Strings.HideRecentViews;
                    subtitleValue = Strings.HideRecentViewsDescription;
                    break;
                case StealthModeFeature.HideNextViews:
                    iconValue = Icons.Rewind2524;
                    titleValue = Strings.HideNextViews;
                    subtitleValue = Strings.HideNextViewsDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;

            title.Text = titleValue;
            TextBlockHelper.SetMarkdown(subtitle, subtitleValue);
            icon.Text = iconValue;
        }

        private void PurchaseCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_clientService.StealthMode.CooldownUntilDate > 0)
            {
                return;
            }

            if (_clientService.IsPremium)
            {
                _clientService.Send(new ActivateStoryStealthMode());
                Activated = true;
            }
            else if (_clientService.IsPremiumAvailable)
            {
                ShouldPurchase = true;
            }

            Hide();
        }
    }
}
