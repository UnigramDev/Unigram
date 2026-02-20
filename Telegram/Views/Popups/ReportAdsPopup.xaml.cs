//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public record ReportAdsSelection(ReportOption Option, object Result);

    public sealed partial class ReportAdsPopup : ContentPopup
    {
        private readonly DialogViewModel _viewModel;
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly long _chatId;
        private readonly long _messageId;

        private readonly Stack<ReportAdsSelection> _history = new();
        private ReportAdsSelection _selection;

        public ReportAdsPopup(DialogViewModel viewModel, long chatId, long messageId, ReportOption option)
        {
            InitializeComponent();
            XamlRoot = viewModel.XamlRoot;

            _viewModel = viewModel;
            _clientService = viewModel.ClientService;
            _navigationService = viewModel.NavigationService;
            _chatId = chatId;
            _messageId = messageId;

            option ??= new ReportOption(Array.Empty<byte>(), Strings.ReportAd);

            Title.Text = option.Text;

            Continue(option);
        }

        private async void Continue(ReportOption option)
        {
            IsEnabled = false;

            if (_selection != null)
            {
                _history.Push(_selection);
            }

            var response = await _clientService.SendAsync(new ReportChatSponsoredMessage(_chatId, _messageId, option.Id));

            UpdateSelection(new ReportAdsSelection(option, response));

            IsEnabled = true;
        }

        private void UpdateSelection(ReportAdsSelection selection)
        {
            _selection = selection;
            Title.Text = selection.Option.Text;

            if (selection.Result is ReportSponsoredResultOptionRequired optionRequired)
            {
                OptionRoot.Visibility = Visibility.Visible;

                Subtitle.Text = optionRequired.Title;
                ScrollingHost.ItemsSource = optionRequired.Options;
            }
            else if (selection.Result is ReportSponsoredResultPremiumRequired textRequired)
            {
                Hide();
                _navigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureDisabledAds()));
            }
            else if (selection.Result is ReportSponsoredResultAdsHidden)
            {
                Hide();
                _viewModel.SponsoredMessage = null;

                ToastPopup.Show(XamlRoot, Strings.AdHidden, ToastPopupIcon.AntiSpam);
            }
            else if (selection.Result is ReportSponsoredResultOk)
            {
                Hide();
                _viewModel.SponsoredMessage = null;

                ToastPopup.Show(XamlRoot, Strings.AdReported, ToastPopupIcon.AntiSpam);
            }

            ShowHideBackButton(_history.Count > 0);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReportOption option && _selection != null)
            {
                Continue(option);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is ReportOption option)
            {
                var textBlock = content.Children[0] as TextBlock;
                textBlock.Text = option.Text;
            }

            args.Handled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_history.TryPop(out ReportAdsSelection selection))
            {
                UpdateSelection(selection);
            }
        }

        private bool _backButtonCollapsed = true;

        private void ShowHideBackButton(bool show)
        {
            if (_backButtonCollapsed != show)
            {
                return;
            }

            _backButtonCollapsed = !show;
            BackButton.Visibility = Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(BackButton);
            var visual2 = ElementComposition.GetElementVisual(Title);

            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual2.Properties.InsertVector3("Translation", Vector3.Zero);
                BackButton.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            var offset = visual1.Compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(0, show ? -28 : 0);
            offset.InsertKeyFrame(1, show ? 0 : -28);
            offset.Duration = Constants.FastAnimation;

            var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            var opacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);

            visual1.CenterPoint = new Vector3(24);

            visual2.StartAnimation("Translation.X", offset);
            visual1.StartAnimation("Scale", scale);
            visual1.StartAnimation("Opacity", opacity);
            batch.End();
        }
    }
}
