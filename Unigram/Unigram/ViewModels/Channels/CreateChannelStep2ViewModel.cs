using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class CreateChannelStep2ViewModel : UnigramViewModelBase
    {
        private TLChannel _channel;
        private TLExportedChatInviteBase _exportedInvite;

        public CreateChannelStep2ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            AdminedPublicChannels = new ObservableCollection<TLChannel>();
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(IsPublic)))
            {
                if (_exportedInvite == null && !_isPublic)
                {
                    ExportInvite();
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            _channel = (TLChannel)parameter;

            var response = await ProtoService.CheckUsernameAsync(_channel.ToInputChannel(), "username");
            if (response.IsSucceeded)
            {
                HasTooMuchUsernames = false;
            }
            else
            {
                if (response.Error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                {
                    HasTooMuchUsernames = true;
                    LoadAdminedPublicChannels();
                }
            }
        }

        public ObservableCollection<TLChannel> AdminedPublicChannels { get; private set; }

        private bool _isPublic = true;
        public bool IsPublic
        {
            get
            {
                return _isPublic;
            }
            set
            {
                Set(ref _isPublic, value);
            }
        }

        private bool _hasTooMuchUsernames;
        public bool HasTooMuchUsernames
        {
            get
            {
                return _hasTooMuchUsernames;
            }
            set
            {
                Set(ref _hasTooMuchUsernames, value);
            }
        }

        private string _inviteLink;
        public string InviteLink
        {
            get
            {
                return _inviteLink;
            }
            set
            {
                Set(ref _inviteLink, value);
            }
        }

        public RelayCommand<TLChannel> RevokeLinkCommand => new RelayCommand<TLChannel>(RevokeLinkExecute);
        private async void RevokeLinkExecute(TLChannel channel)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = "Revoke link";
            dialog.Message = string.Format("Are you sure you want to revoke the link t.me/{0}?\r\n\r\nThe channel \"{1}\" will become private.", channel.Username, channel.DisplayName);
            dialog.PrimaryButtonText = "Revoke";
            dialog.SecondaryButtonText = "Cancel";

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.UpdateUsernameAsync(channel.ToInputChannel(), string.Empty);
                if (response.IsSucceeded)
                {
                    channel.HasUsername = false;
                    channel.Username = null;
                    channel.RaisePropertyChanged(() => channel.HasUsername);
                    channel.RaisePropertyChanged(() => channel.Username);

                    HasTooMuchUsernames = false;
                    AdminedPublicChannels.Clear();


                }
            }
        }

        private async void LoadAdminedPublicChannels()
        {
            if (AdminedPublicChannels.Count > 0)
            {
                return;
            }

            var response = await ProtoService.GetAdminedPublicChannelsAsync();
            if (response.IsSucceeded)
            {
                AdminedPublicChannels.AddRange(response.Result.Chats.OfType<TLChannel>(), true);
            }
            else
            {
                Execute.ShowDebugMessage("channels.getAdminedPublicChannels error " + response.Error);
            }
        }

        private async void ExportInvite()
        {
            var response = await ProtoService.ExportInviteAsync(_channel.ToInputChannel());
            if (response.IsSucceeded)
            {
                _exportedInvite = response.Result;

                var invite = response.Result as TLChatInviteExported;
                if (invite != null && !string.IsNullOrEmpty(invite.Link))
                {
                    InviteLink = invite.Link;
                }
            }
            else
            {
                Execute.ShowDebugMessage("channels.exportInvite error " + response.Error);
            }
        }
    }
}
