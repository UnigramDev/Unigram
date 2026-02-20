//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class SendPreparedMessagePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        public SendPreparedMessagePopup(IClientService clientService, INavigationService navigationService, Message message, User botUser)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            Title = Strings.BotShareMessage;

            PrimaryButtonText = Strings.BotShareMessageShare;
            ButtonsLayout = ContentPopupButtonsLayout.Vertical;

            message.IsOutgoing = false;
            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var settings = clientService.Session.Resolve<ISettingsService>();

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, delegato, chat, null, null, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            CaptionInfo.Text = string.Format(Strings.BotShareMessageInfo, botUser.FirstName);
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
