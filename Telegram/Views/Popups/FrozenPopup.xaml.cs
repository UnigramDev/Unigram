//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Views.Popups
{
    public sealed partial class FrozenPopup : ContentPopup
    {
        private readonly UpdateFreezeState _state;

        public FrozenPopup(UpdateFreezeState state)
        {
            InitializeComponent();

            _state = state;

            var text = string.Format(Strings.AccountFrozen3Text, Formatter.Date(state.DeletionDate, "DATE_LONGDATE"));
            var formatted = ClientEx.ParseMarkdown(text);

            if (formatted.Entities.Count == 1)
            {
                formatted.Entities[0].Type = new TextEntityTypeMention();
            }

            TextBlockHelper.SetFormattedText(Line3, formatted);

            PrimaryButtonText = Strings.AccountFrozenButtonAppeal;
            CloseButtonText = Strings.AccountFrozenButtonUnderstood;

            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var navigationService = WindowContext.GetNavigationService(sender.XamlRoot);
            if (navigationService != null)
            {
                var clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);

                Hide();
                MessageHelper.OpenUrl(clientService, navigationService, _state.AppealLink);
            }
        }
    }
}
