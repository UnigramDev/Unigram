//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Windows.System;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class InstantViewModel : MultiViewModelBase
    {
        private readonly ITranslateService _translateService;
        private readonly IMessageFactory _messageFactory;

        private readonly IMessageDelegate _messageDelegate;

        public InstantViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _translateService = translateService;
            _messageFactory = messageFactory;
            _gallery = new InstantGalleryViewModel(clientService, storageService, aggregator);

            _messageDelegate = new InstantMessageDelegate(this);

            Children.Add(_gallery);
        }

        public ITranslateService TranslateService => _translateService;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetWebPagePreview(new FormattedText((string)parameter, Array.Empty<TextEntity>()), null));
            if (response is WebPage webPage)
            {
                Title = webPage.SiteName;
            }
        }

        public Uri ShareLink { get; set; }
        public string ShareTitle { get; set; }

        public MessageViewModel CreateMessage(Message message)
        {
            return _messageFactory.Create(_messageDelegate, null, message);
        }

        private InstantGalleryViewModel _gallery;
        public InstantGalleryViewModel Gallery
        {
            get => _gallery;
            set => Set(ref _gallery, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public async void Share()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostLink(new HttpUrl(link.ToString())));
        }

        public async void Feedback()
        {
            var response = await ClientService.SendAsync(new SearchPublicChat("previews"));
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public async void Browser()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            await Launcher.LaunchUriAsync(link);
        }

        public void Copy()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            MessageHelper.CopyLink(link.ToString());
        }
    }
}
