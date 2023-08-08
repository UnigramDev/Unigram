//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class StoriesPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private PremiumPaymentOption _option;

        public StoriesPopup(IClientService clientService, INavigationService navigationService, PremiumPaymentOption option = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            RequestedTheme = option == null
                ? Windows.UI.Xaml.ElementTheme.Dark
                : Windows.UI.Xaml.ElementTheme.Default;

            ScrollingHost.ItemsSource = new object[]
            {
                new PremiumStoryFeaturePriorityOrder(),
                new PremiumStoryFeatureStealthMode(),
                new PremiumStoryFeaturePermanentViewsHistory(),
                new PremiumStoryFeatureCustomExpirationDuration(),
                new PremiumStoryFeatureSaveStories(),
                new PremiumLimitTypeStoryCaptionLength(),
                new PremiumStoryFeatureLinksAndFormatting()
            };

            if (option != null)
            {
                _option = option;
                PurchaseCommand.Content = PromoPopup.GetPaymentString(clientService.IsPremium, option);
            }
            else
            {
                InitializePaymentOptions(clientService);
            }

            if (clientService.TryGetChat(clientService.Options.MyId, out Chat chat) &&
                clientService.TryGetUser(chat, out User user))
            {
                Segments.UpdateSegments(96, 8, 8, 3);
                Photo.SetUser(clientService, user, 96);
            }

            clientService.Send(new ViewPremiumFeature(new PremiumFeatureUpgradedStories()));
        }

        private async void InitializePaymentOptions(IClientService clientService)
        {
            var state = await clientService.SendAsync(new GetPremiumState()) as PremiumState;
            var payment = state?.PaymentOptions.LastOrDefault();

            var option = payment?.PaymentOption;
            if (option != null)
            {
                _option = option;
                PurchaseCommand.Content = PromoPopup.GetPaymentString(clientService.IsPremium, option);
            }
        }

        public bool ShouldPurchase { get; private set; }

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0x00, 0x7A, 0xFF),
            Color.FromArgb(0xFF, 0x79, 0x8A, 0xFF),
            Color.FromArgb(0xFF, 0xAC, 0x64, 0xF3),
            Color.FromArgb(0xFF, 0xC4, 0x56, 0xAE),
            Color.FromArgb(0xFF, 0xE9, 0x5D, 0x44),
            Color.FromArgb(0xFF, 0xF2, 0x82, 0x2A),
            Color.FromArgb(0xFF, 0xE7, 0xAD, 0x19)
        };

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
                case PremiumStoryFeaturePriorityOrder:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesPriority;
                    subtitleValue = Strings.PremiumStoriesPriorityDescription;
                    break;
                case PremiumStoryFeatureStealthMode:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesStealth;
                    subtitleValue = Strings.PremiumStoriesStealthDescription;
                    break;
                case PremiumStoryFeaturePermanentViewsHistory:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesViews;
                    subtitleValue = Strings.PremiumStoriesViewsDescription;
                    break;
                case PremiumStoryFeatureCustomExpirationDuration:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesExpiration;
                    subtitleValue = Strings.PremiumStoriesExpirationDescription;
                    break;
                case PremiumStoryFeatureSaveStories:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesSaveToGallery;
                    subtitleValue = Strings.PremiumStoriesSaveToGalleryDescription;
                    break;
                case PremiumLimitTypeStoryCaptionLength:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesCaption;
                    subtitleValue = Strings.PremiumStoriesCaptionDescription;
                    break;
                case PremiumStoryFeatureLinksAndFormatting:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesFormatting;
                    subtitleValue = Strings.PremiumStoriesFormattingDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;

            var index = Math.Min(args.ItemIndex, _gradient.Length - 1);

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            icon.Foreground = new SolidColorBrush(_gradient[index]);
        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ShouldPurchase = true;
            Hide();
            Purchase();
        }

        private void Purchase()
        {
            if (_option != null && !_clientService.IsPremium)
            {
                _clientService.Send(new ClickPremiumSubscriptionButton());
                MessageHelper.OpenTelegramUrl(_clientService, _navigationService, _option.PaymentLink);
            }
        }
    }
}
