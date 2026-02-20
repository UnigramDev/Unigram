//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Monetization.Popups
{
    public sealed partial class AboutAdsPopup : ContentPopup
    {
        private readonly string _value;
        private readonly string _url;

        private readonly DialogViewModel _viewModel;
        private readonly SponsoredMessage _message;

        public AboutAdsPopup(DialogViewModel viewModel, SponsoredMessage message)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _message = message;

            //Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/CollectibleUsername.tgs");

            TextBlockHelper.SetMarkdown(LongInfo, string.Format(Strings.RevenueSharingAdsInfo4Subtitle2, string.Empty));//string.Format("[{0}]({1})", Strings.RevenueSharingAdsInfo4SubtitleLearnMore.Replace("**", string.Empty), Strings.PromoteUrl)));
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {

        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
            MessageHelper.OpenUrl(null, null, _url);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
            MessageHelper.CopyText(XamlRoot, _value);
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(SponsorInfo, Strings.SponsoredMessageSponsorReportable, Icons.Megaphone);
            flyout.CreateFlyoutSeparator();

            if (_message.CanBeReported)
            {
                flyout.CreateFlyoutItem(ReportAd, Strings.ReportAd, Icons.HandRight);
            }

            flyout.CreateFlyoutItem(HideAd, Strings.HideAd, Icons.DismissCircle);

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void SponsorInfo()
        {

        }

        private void ReportAd()
        {
            Hide();
            _viewModel.ShowPopup(new ReportAdsPopup(_viewModel, _viewModel.Chat.Id, _message.MessageId, null));
        }

        private void HideAd()
        {
            Hide();
            _viewModel.HideSponsoredMessage();
        }
    }
}
