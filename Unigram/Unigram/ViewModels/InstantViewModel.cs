//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Unigram.ViewModels
{
    public class InstantViewModel : TLViewModelBase
    {
        private readonly ITranslateService _translateService;
        private readonly IMessageFactory _messageFactory;

        public InstantViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _translateService = translateService;
            _messageFactory = messageFactory;
            _gallery = new InstantGalleryViewModel(clientService, storageService, aggregator);

            ShareCommand = new RelayCommand(ShareExecute);
            FeedbackCommand = new RelayCommand(FeedbackExecute);
            BrowserCommand = new RelayCommand(BrowserExecute);
            CopyCommand = new RelayCommand(CopyExecute);
        }

        public ITranslateService TranslateService => _translateService;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetWebPagePreview(new FormattedText((string)parameter, new TextEntity[0])));
            if (response is WebPage webPage)
            {
                Title = webPage.SiteName;
            }
        }

        public Uri ShareLink { get; set; }
        public string ShareTitle { get; set; }

        public MessageViewModel CreateMessage(IMessageDelegate delegato, Message message)
        {
            return _messageFactory.Create(delegato, message);
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

        public RelayCommand ShareCommand { get; }
        private async void ShareExecute()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            await SharePopup.GetForCurrentView().ShowAsync(link, ShareTitle);
        }

        public RelayCommand FeedbackCommand { get; }
        private async void FeedbackExecute()
        {
            var response = await ClientService.SendAsync(new SearchPublicChat("previews"));
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public RelayCommand BrowserCommand { get; }
        private async void BrowserExecute()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            await Launcher.LaunchUriAsync(link);
        }

        public RelayCommand CopyCommand { get; }
        private async void CopyExecute()
        {
            var link = ShareLink;
            if (link == null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(link.AbsoluteUri);
            ClipboardEx.TrySetContent(dataPackage);

            await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.LinkCopied, Strings.Resources.AppName, Strings.Resources.OK);
        }
    }
}
