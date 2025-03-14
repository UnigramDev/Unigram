using Microsoft.UI.Xaml.Controls;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;

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

            message.IsOutgoing = false;
            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var playback = TypeResolver.Current.Playback;
            var settings = TypeResolver.Current.Resolve<ISettingsService>(clientService.SessionId);

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, playback, delegato, chat, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            CaptionInfo.Text = string.Format(Strings.BotShareMessageInfo, botUser.FirstName);
        }

        private void Purchase_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
