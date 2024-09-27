//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using GiftGroup = Telegram.Collections.KeyedList<Telegram.Views.Premium.Popups.GiftPopup.GiftGroupType, Telegram.Td.Api.Gift>;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class GiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly long _userId;

        private readonly DiffObservableCollection<Gift> _gifts = new(Constants.DiffOptions);

        public GiftPopup(IClientService clientService, INavigationService navigationService, User user, IList<PremiumPaymentOption> options)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _userId = user.Id;

            Photo.SetUser(clientService, user, 96);

            TextBlockHelper.SetMarkdown(PremiumInfo, string.Format(Strings.Gift2PremiumInfo, user.FirstName));
            TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsInfo, user.FirstName));

            ScrollingHost.ItemsSource = _gifts;

            if (options.Count > 0)
            {
                PremiumOptions.ItemsSource = options
                    .OrderBy(x => x.MonthCount)
                    .ToList();

                StarsRoot.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                PremiumTitle.Visibility = Visibility.Collapsed;
                PremiumInfo.Visibility = Visibility.Collapsed;
            }

            InitializeGifts(clientService);
        }

        public enum GiftGroupType
        {
            All,
            Limited,
            StarCount
        }

        private async void InitializeGifts(IClientService clientService)
        {
            var response = await clientService.SendAsync(new GetAvailableGifts());
            if (response is Gifts gifts)
            {
                //ScrollingHost.ItemsSource = gifts.GiftsValue;

                var ciccio = new List<GiftGroup>();
                ciccio.Add(new GiftGroup(GiftGroupType.All, gifts.GiftsValue));

                //Navigation.Items.Add(new TopNavViewItem { Content = Strings.Gift2TabAll });

                if (gifts.GiftsValue.Any(x => x.TotalCount > 0))
                {
                    ciccio.Add(new GiftGroup(GiftGroupType.Limited, gifts.GiftsValue.Where(x => x.TotalCount > 0)));

                    //Navigation.Items.Add(new TopNavViewItem { Content = Strings.Gift2TabLimited });
                }

                var groups = gifts.GiftsValue
                    .GroupBy(x => x.StarCount)
                    .OrderBy(x => x.Key);

                foreach (var group in groups)
                {
                    ciccio.Add(new GiftGroup(GiftGroupType.StarCount, group));
                }

                Navigation.ItemsSource = ciccio;
                Navigation.SelectedIndex = 0;
            }
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Gift gift)
            {
                Hide();
                await _clientService.SendAsync(new CreatePrivateChat(_clientService.Options.MyId, true));
                await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, gift, _userId));
            }
            else if (e.ClickedItem is PremiumPaymentOption option)
            {
                Hide();
                MessageHelper.OpenTelegramUrl(_clientService, _navigationService, option.PaymentLink);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserGiftCell userGiftCell && args.Item is Gift gift)
            {
                userGiftCell.UpdateGift(_clientService, gift);
            }
            else if (args.ItemContainer.ContentTemplateRoot is PremiumGiftCell premiumGiftCell && args.Item is PremiumPaymentOption option)
            {
                premiumGiftCell.UpdatePremiumGift(_clientService, option);
            }

            args.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is GiftGroup group)
            {
                _gifts.ReplaceDiff(group);
            }
        }
    }
}
