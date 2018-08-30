using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Windows.System;

namespace Unigram.ViewModels
{
    public class InstantViewModel : TLViewModelBase
    {
        public InstantViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _gallery = new InstantGalleryViewModel(aggregator);

            ShareCommand = new RelayCommand(ShareExecute);
            FeedbackCommand = new RelayCommand(FeedbackExecute);
            BrowserCommand = new RelayCommand(BrowserExecute);
        }

        public Uri ShareLink { get; set; }
        public string ShareTitle { get; set; }

        private InstantGalleryViewModel _gallery;
        public InstantGalleryViewModel Gallery
        {
            get
            {
                return _gallery;
            }
            set
            {
                Set(ref _gallery, value);
            }
        }

        public RelayCommand ShareCommand { get; }
        private async void ShareExecute()
        {
            if (ShareLink != null)
            {
                await ShareView.GetForCurrentView().ShowAsync(ShareLink, ShareTitle);
            }
        }

        public RelayCommand FeedbackCommand { get; }
        private async void FeedbackExecute()
        {
            var response = await ProtoService.SendAsync(new SearchPublicChat("previews"));
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public RelayCommand BrowserCommand { get; }
        private async void BrowserExecute()
        {
            await Launcher.LaunchUriAsync(ShareLink);
        }
    }
}
