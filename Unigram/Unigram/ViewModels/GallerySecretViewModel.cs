using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
using Windows.Storage;
using Windows.System;

namespace Unigram.ViewModels
{
    public class GallerySecretViewModel : UnigramViewModelBase
    {
        public GallerySecretViewModel(TLInputPeerBase peer, TLMessage message, IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SelectedItem = new GallerySecretMessageItem(message);
        }

        protected GallerySecretMessageItem _firstItem;
        public GallerySecretMessageItem SelectedItem
        {
            get
            {
                return _firstItem;
            }
            set
            {
                Set(ref _firstItem, value);
            }
        }
    }

    public class GallerySecretMessageItem : GalleryMessageItem
    {
        public GallerySecretMessageItem(TLMessage message)
            : base(message)
        {
        }
    }
}
