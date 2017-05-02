using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Views;

namespace Unigram.ViewModels
{
    public class InstantViewModel : UnigramViewModelBase
    {
        public InstantViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            _gallery = new InstantGalleryViewModel();
        }

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

        public RelayCommand FeedbackCommand => new RelayCommand(FeedbackExecute);
        private async void FeedbackExecute()
        {
            var user = CacheService.GetUser("previews");
            if (user == null)
            {
                var response = await ProtoService.ResolveUsernameAsync("previews");
                if (response.IsSucceeded)
                {
                    user = response.Result.Users.FirstOrDefault();
                }
            }

            if (user != null)
            {
                NavigationService.Navigate(typeof(DialogPage), user.ToPeer());
            }
        }
    }
}
