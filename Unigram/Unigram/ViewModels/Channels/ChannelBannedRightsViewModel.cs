using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.Aggregator;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views.Users;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelBannedRightsViewModel : UnigramViewModelBase
    {
        public ChannelBannedRightsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProfileCommand = new RelayCommand(ProfileExecute);
            SendCommand = new RelayCommand(SendExecute);
            DismissCommand = new RelayCommand(DismissExecute);
        }

        private TLChannel _channel;
        public TLChannel Channel
        {
            get
            {
                return _channel;
            }
            set
            {
                Set(ref _channel, value);
            }
        }

        private TLChannelParticipantBanned _item;
        public TLChannelParticipantBanned Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        private TLUserFull _full;
        public TLUserFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<TLPeerChannel, TLChannelParticipantBase>(from);
                if (tuple.Item2 is TLChannelParticipant participant)
                {
                    IsBannedAlready = false;

                    tuple.Item2 = new TLChannelParticipantBanned
                    {
                        UserId = participant.UserId,
                        Date = participant.Date,
                        BannedRights = new TLChannelBannedRights
                        {
                            IsViewMessages = true,
                            IsSendMessages = false,
                            IsSendMedia = false,
                            IsSendStickers = false,
                            IsSendGifs = false,
                            IsSendGames = false,
                            IsSendInline = false,
                            IsEmbedLinks = false
                        }
                    };
                }
                else if (tuple.Item2 is TLChannelParticipantAdmin admin)
                {
                    IsBannedAlready = false;

                    tuple.Item2 = new TLChannelParticipantBanned
                    {
                        UserId = admin.UserId,
                        Date = admin.Date,
                        BannedRights = new TLChannelBannedRights
                        {
                            IsViewMessages = true,
                            IsSendMessages = false,
                            IsSendMedia = false,
                            IsSendStickers = false,
                            IsSendGifs = false,
                            IsSendGames = false,
                            IsSendInline = false,
                            IsEmbedLinks = false
                        }
                    };
                }

                Channel = CacheService.GetChat(tuple.Item1.ChannelId) as TLChannel;
                Item = tuple.Item2 as TLChannelParticipantBanned;

                IsEmbedLinks = _item.BannedRights.IsEmbedLinks;
                IsSendInline = _item.BannedRights.IsSendInline;
                IsSendGames = _item.BannedRights.IsSendGames;
                IsSendGifs = _item.BannedRights.IsSendGifs;
                IsSendStickers = _item.BannedRights.IsSendStickers;
                IsSendMedia = _item.BannedRights.IsSendMedia;
                IsSendMessages = _item.BannedRights.IsSendMessages;
                IsViewMessages = _item.BannedRights.IsViewMessages;

                var user = tuple.Item2.User;
                if (user == null)
                {
                    return;
                }

                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(user.ToInputUser());
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }

                Full = full;
            }
        }

        private bool _isBannedAlready = true;
        public bool IsBannedAlready
        {
            get
            {
                return _isBannedAlready;
            }
            set
            {
                Set(ref _isBannedAlready, value);
            }
        }

        #region Flags

        private bool _isViewMessages;
        public bool IsViewMessages
        {
            get
            {
                return _isViewMessages;
            }
            set
            {
                Set(ref _isViewMessages, value);

                // Don't allow send messages
                if (value && !_isSendMessages)
                {
                    IsSendMessages = true;
                }
            }
        }

        private bool _isSendMessages;
        public bool IsSendMessages
        {
            get
            {
                return _isSendMessages;
            }
            set
            {
                Set(ref _isSendMessages, value);

                // Allow view
                if (!value && _isViewMessages)
                {
                    IsViewMessages = false;
                }

                // Don't allow send media
                if (value && !_isSendMedia)
                {
                    IsSendMedia = true;
                }
            }
        }

        private bool _isSendMedia;
        public bool IsSendMedia
        {
            get
            {
                return _isSendMedia;
            }
            set
            {
                Set(ref _isSendMedia, value);

                // Allow send messages
                if (!value && _isSendMessages)
                {
                    IsSendMessages = false;
                }

                // Don't allow send stickers
                if (value && !_isSendStickers)
                {
                    IsSendStickers = true;
                }

                // Don't allow embed links
                if (value && !_isEmbedLinks)
                {
                    IsEmbedLinks = true;
                }
            }
        }

        private bool _isSendStickers;
        public bool IsSendStickers
        {
            get
            {
                return _isSendStickers;
            }
            set
            {
                Set(ref _isSendStickers, value);

                _isSendGifs = value;
                _isSendGames = value;
                _isSendInline = value;

                // Allow send media
                if (!value && _isSendMedia)
                {
                    IsSendMedia = false;
                }
            }
        }

        private bool _isSendGifs;
        public bool IsSendGifs
        {
            get
            {
                return _isSendGifs;
            }
            set
            {
                Set(ref _isSendGifs, value);
            }
        }

        private bool _isSendGames;
        public bool IsSendGames
        {
            get
            {
                return _isSendGames;
            }
            set
            {
                Set(ref _isSendGames, value);
            }
        }

        private bool _isSendInline;
        public bool IsSendInline
        {
            get
            {
                return _isSendInline;
            }
            set
            {
                Set(ref _isSendInline, value);
            }
        }

        private bool _isEmbedLinks;
        public bool IsEmbedLinks
        {
            get
            {
                return _isEmbedLinks;
            }
            set
            {
                Set(ref _isEmbedLinks, value);

                if (!value && _isSendMedia)
                {
                    IsSendMedia = false;
                }
            }
        }

        #endregion

        public RelayCommand ProfileCommand { get; }
        private void ProfileExecute()
        {
            var user = _item.User;
            if (user == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserDetailsPage), user.ToPeer());
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var rights = new TLChannelBannedRights
            {
                IsViewMessages = _isViewMessages,
                IsSendMessages = _isViewMessages,
                IsSendMedia = _isSendMedia,
                IsSendStickers = _isSendStickers,
                IsSendGifs = _isSendGifs,
                IsSendGames = _isSendGames,
                IsSendInline = _isSendInline,
                IsEmbedLinks = _isEmbedLinks
            };

            var response = await ProtoService.EditBannedAsync(_channel, _item.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
        }

        public RelayCommand DismissCommand { get; }
        private async void DismissExecute()
        {
            var rights = new TLChannelBannedRights();

            var response = await ProtoService.EditBannedAsync(_channel, _item.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
        }
    }
}
