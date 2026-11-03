//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TextStylePreviewPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly TextCompositionStyle _style;

        private readonly List<TextCompositionStyleExample> _examples = new();
        private int _exampleNumber;

        public TextStylePreviewPopup(IClientService clientService, INavigationService navigationService, TextCompositionStyle style, TextCompositionStyleExample example)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _style = style;

            _examples.Add(example);

            Icon.Source = new CustomEmojiFileSource(clientService, style.CustomEmojiId);
            Title.Text = style.Title;

            Before.SetText(clientService, example.SourceText);
            After.SetText(clientService, example.ResultText);

            if (clientService.TryGetUser(style.CreatorUserId, out User user))
            {
                if (user.HasActiveUsername(out string username))
                {
                    Used.Text = string.Format(Strings.AIEditorCreatedBy, "@" + username);
                }
                else
                {
                    Used.Text = string.Format(Strings.AIEditorCreatedBy, user.FullName());
                }
            }
            else if (style.InstallCount > 0)
            {
                Used.Text = Locale.Declension(Strings.R.AIEditorUsedBy, style.InstallCount);
            }
            else
            {
                ContentRoot.Margin = new Thickness(12, 12, 12, 24);
                Used.Visibility = Visibility.Collapsed;
            }

            PrimaryButtonText = clientService.IsTextCompositionStyleInstalled(style.Name)
                ? Strings.Done
                : Strings.AIEditorAddStyle;
        }

        private async void Another_Click(object sender, RoutedEventArgs e)
        {
            if (_examples.Count < _clientService.Options.TextCompositionStyleExampleCount)
            {
                Before.ShowHideSkeleton(true);
                Before.InvalidateArrange();

                After.ShowHideSkeleton(true);
                After.InvalidateArrange();

                var response = await _clientService.SendAsync(new GetTextCompositionStyleExample(_style.Name, ++_exampleNumber));
                if (response is TextCompositionStyleExample example)
                {
                    _examples.Add(example);

                    Before.ShowHideSkeleton(false);
                    Before.SetText(_clientService, example.SourceText);

                    After.ShowHideSkeleton(false);
                    After.SetText(_clientService, example.ResultText);
                }
            }
            else
            {
                _exampleNumber++;

                Before.SetText(_clientService, _examples[_exampleNumber % _examples.Count].SourceText);
                After.SetText(_clientService, _examples[_exampleNumber % _examples.Count].ResultText);
            }
        }

        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            IsPrimaryButtonPending = true;

            var deferral = args.GetDeferral();

            var response = await _clientService.SendAsync(new AddTextCompositionStyle(_style.Name));
            if (response is Error error)
            {
                IsPrimaryButtonPending = false;

                ToastPopup.ShowError(XamlRoot, error);
                args.Cancel = true;
            }

            deferral.Complete();
        }

        private void Used_Click(object sender, TextUrlClickEventArgs e)
        {
            Hide();
            _navigationService.NavigateToUser(_style.CreatorUserId);
        }
    }
}
