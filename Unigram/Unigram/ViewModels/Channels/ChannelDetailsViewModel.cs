using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Views.Channels;
using Unigram.Views.Chats;
using Unigram.Views.Dialogs;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelDetailsViewModel : ChannelParticipantsViewModelBase, IHandle<TLUpdateChannel>, IHandle<TLUpdateNotifySettings>
    {
        public ChannelDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new TLChannelParticipantsRecent(), query => new TLChannelParticipantsSearch { Q = query })
        {
            EditCommand = new RelayCommand(EditExecute);
            InviteCommand = new RelayCommand(InviteExecute);
            MediaCommand = new RelayCommand(MediaExecute);
            AdminsCommand = new RelayCommand(AdminsExecute);
            BannedCommand = new RelayCommand(BannedExecute);
            KickedCommand = new RelayCommand(KickedExecute);
            ParticipantsCommand = new RelayCommand(ParticipantsExecute);
            AdminLogCommand = new RelayCommand(AdminLogExecute);
            ToggleMuteCommand = new RelayCommand(ToggleMuteExecute);
            UsernameCommand = new RelayCommand(UsernameExecute);
            ParticipantPromoteCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantPromoteExecute);
            ParticipantRestrictCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantRestrictExecute);
            ParticipantRemoveCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantRemoveExecute);
        }

        protected TLChannelFull _full;
        public TLChannelFull Full
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
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full != null)
                {
                    Full = full;

                    if (_item.IsMegaGroup)
                    {
                        Participants = new ItemsCollection(ProtoService, channel.ToInputChannel(), null, full.ParticipantsCount);
                    }

                    RaisePropertyChanged(() => IsMuted);
                    RaisePropertyChanged(() => Participants);

                    Aggregator.Subscribe(this);
                }
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public bool IsEditEnabled
        {
            get
            {
                return _item != null && (_item.IsCreator || (_item.HasAdminRights && _item.AdminRights.IsChangeInfo));
            }
        }

        public bool IsAdminLog
        {
            get
            {
                return _item != null && (_item.IsCreator || _item.HasAdminRights);
            }
        }

        public bool IsInviteUsers
        {
            get
            {
                return _item != null && (_item.IsCreator || (_item.HasAdminRights && _item.AdminRights.IsInviteUsers));
            }
        }

        public bool IsMuted
        {
            get
            {
                var notifySettings = _full?.NotifySettings as TLPeerNotifySettings;
                if (notifySettings == null)
                {
                    return false;
                }

                var clientDelta = MTProtoService.Current.ClientTicksDelta;
                var utc0SecsInt = notifySettings.MuteUntil - clientDelta / 4294967296.0;

                var muteUntilDateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);
                return muteUntilDateTime > DateTime.Now;
            }
        }

        public void Handle(TLUpdateChannel update)
        {
            if (_item == null)
            {
                return;
            }

            if (_item.Id == update.ChannelId)
            {
                RaisePropertyChanged(() => Item);
                RaisePropertyChanged(() => Full);
                RaisePropertyChanged(() => IsMuted);

                RaisePropertyChanged(() => IsInviteUsers);
                RaisePropertyChanged(() => IsEditEnabled);
                RaisePropertyChanged(() => IsAdminLog);
            }
        }

        public void Handle(TLUpdateNotifySettings update)
        {
            var notifyPeer = update.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                var peer = notifyPeer.Peer;
                if (peer is TLPeerChannel && peer.Id == Item.Id)
                {
                    BeginOnUIThread(() =>
                    {
                        Full.NotifySettings = update.NotifySettings;
                        Full.RaisePropertyChanged(() => Full.NotifySettings);
                        RaisePropertyChanged(() => IsMuted);

                        //var notifySettings = updateNotifySettings.NotifySettings as TLPeerNotifySettings;
                        //if (notifySettings != null)
                        //{
                        //    _suppressUpdating = true;
                        //    MuteUntil = notifySettings.MuteUntil.Value;
                        //    _suppressUpdating = false;
                        //}
                    });
                }
            }
        }

        public RelayCommand EditCommand { get; }
        private void EditExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelEditPage), _item.ToPeer());
            //NavigationService.Navigate(typeof(ChannelManagePage), _item.ToPeer());
        }

        public RelayCommand InviteCommand { get; }
        private void InviteExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatInvitePage), _item.ToPeer());
        }

        public RelayCommand MediaCommand { get; }
        private void MediaExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(DialogSharedMediaPage), _item.ToInputPeer());
        }

        public RelayCommand AdminsCommand { get; }
        private void AdminsExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelAdminsPage), _item.ToPeer());
        }

        public RelayCommand BannedCommand { get; }
        private void BannedExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelBannedPage), _item.ToPeer());
        }

        public RelayCommand KickedCommand { get; }
        private void KickedExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelKickedPage), _item.ToPeer());
        }

        public RelayCommand ParticipantsCommand { get; }
        private void ParticipantsExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelParticipantsPage), _item.ToPeer());
        }

        public RelayCommand AdminLogCommand { get; }
        private void AdminLogExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelAdminLogPage), _item.ToPeer());
        }

        public RelayCommand ToggleMuteCommand { get; }
        private async void ToggleMuteExecute()
        {
            if (_item == null || _full == null)
            {
                return;
            }

            var notifySettings = _full.NotifySettings as TLPeerNotifySettings;
            if (notifySettings != null)
            {
                var muteUntil = notifySettings.MuteUntil == int.MaxValue ? 0 : int.MaxValue;
                var settings = new TLInputPeerNotifySettings
                {
                    MuteUntil = muteUntil,
                    IsShowPreviews = notifySettings.IsShowPreviews,
                    IsSilent = notifySettings.IsSilent,
                    Sound = notifySettings.Sound
                };

                var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = _item.ToInputPeer() }, settings);
                if (response.IsSucceeded)
                {
                    notifySettings.MuteUntil = muteUntil;
                    RaisePropertyChanged(() => IsMuted);
                    Full.RaisePropertyChanged(() => Full.NotifySettings);

                    var dialog = CacheService.GetDialog(_item.ToPeer());
                    if (dialog != null)
                    {
                        dialog.NotifySettings = _full.NotifySettings;
                        dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                        dialog.RaisePropertyChanged(() => dialog.IsMuted);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                    }

                    CacheService.Commit();
                }
            }
        }

        public RelayCommand UsernameCommand { get; }
        public async void UsernameExecute()
        {
            var item = _item as TLChannel;
            if (item == null)
            {
                return;
            }

            var title = item.Title;
            var link = new Uri(MeUrlPrefixConverter.Convert(item.Username));

            await ShareView.Current.ShowAsync(link, title);
        }

        #region Context menu

        public RelayCommand<TLChannelParticipantBase> ParticipantPromoteCommand { get; }
        private void ParticipantPromoteExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelAdminRightsPage), TLTuple.Create(_item.ToPeer(), participant));
        }

        public RelayCommand<TLChannelParticipantBase> ParticipantRestrictCommand { get; }
        private void ParticipantRestrictExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelBannedRightsPage), TLTuple.Create(_item.ToPeer(), participant));
        }

        public RelayCommand<TLChannelParticipantBase> ParticipantRemoveCommand { get; }
        private async void ParticipantRemoveExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            if (participant.User == null)
            {
                return;
            }

            var rights = new TLChannelBannedRights { IsEmbedLinks = true, IsSendGames = true, IsSendGifs = true, IsSendInline = true, IsSendMedia = true, IsSendMessages = true, IsSendStickers = true, IsViewMessages = true };

            var response = await ProtoService.EditBannedAsync(_item, participant.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                Participants.Remove(participant);
            }
        }

        #endregion
    }

    public class TLChannelParticipantBaseComparer : IComparer<TLChannelParticipantBase>
    {
        private bool _epoch;

        public TLChannelParticipantBaseComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLChannelParticipantBase x, TLChannelParticipantBase y)
        {

            var xUser = x.User;
            var yUser = y.User;
            if (xUser == null || yUser == null)
            {
                return -1;
            }

            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(yUser).CompareTo(LastSeenConverter.GetIndex(xUser));
                if (epoch == 0)
                {
                    var fullName = xUser.FullName.CompareTo(yUser.FullName);
                    if (fullName == 0)
                    {
                        return yUser.Id.CompareTo(xUser.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                var fullName = xUser.FullName.CompareTo(yUser.FullName);
                if (fullName == 0)
                {
                    return yUser.Id.CompareTo(xUser.Id);
                }

                return fullName;
            }
        }
    }
}
