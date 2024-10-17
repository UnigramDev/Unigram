//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.ViewModels.Create
{
    public partial class NewGroupViewModel : ViewModelBase
    {
        private readonly IProfilePhotoService _profilePhotoService;

        public NewGroupViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
            : base(clientService, settingsService, aggregator)
        {
            _profilePhotoService = profilePhotoService;
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (Set(ref _title, value))
                {
                    RaisePropertyChanged(nameof(CanCreate));
                }
            }
        }

        private InputChatPhoto _inputPhoto;

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        private int _ttl;
        public int Ttl
        {
            get => Array.IndexOf(_ttlIndexer, _ttl);
            set
            {
                if (value >= 0 && value < _ttlIndexer.Length && _ttl != _ttlIndexer[value])
                {
                    _ttl = _ttlIndexer[value];
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _ttlIndexer = new[]
        {
            0,
            30,
            60 * 60 * 24 * 1,
            60 * 60 * 24 * 2,
            60 * 60 * 24 * 3,
            60 * 60 * 24 * 4,
            60 * 60 * 24 * 5,
            60 * 60 * 24 * 6,
            60 * 60 * 24 * 7,
            60 * 60 * 24 * 7 * 2,
            60 * 60 * 24 * 7 * 3,
            60 * 60 * 24 * 31,
            60 * 60 * 24 * 31 * 2,
            60 * 60 * 24 * 31 * 3,
            60 * 60 * 24 * 31 * 4,
            60 * 60 * 24 * 31 * 5,
            60 * 60 * 24 * 31 * 6,
            60 * 60 * 24 * 365,
        };

        public List<SettingsOptionItem<int>> TtlOptions { get; } = new()
        {
            new SettingsOptionItem<int>(0, Strings.ShortMessageLifetimeForever),
            new SettingsOptionItem<int>(60 * 60 * 24 * 1, Locale.FormatTtl(60 * 60 * 24)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 2, Locale.FormatTtl(60 * 60 * 24 * 2)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 3, Locale.FormatTtl(60 * 60 * 24 * 3)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 4, Locale.FormatTtl(60 * 60 * 24 * 4)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 5, Locale.FormatTtl(60 * 60 * 24 * 5)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 6, Locale.FormatTtl(60 * 60 * 24 * 6)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 7 * 1, Locale.FormatTtl(60 * 60 * 24 * 7)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 7 * 2, Locale.FormatTtl(60 * 60 * 24 * 7 * 2)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 7 * 3, Locale.FormatTtl(60 * 60 * 24 * 7 * 3)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30, Locale.FormatTtl(60 * 60 * 24 * 31)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30 * 2, Locale.FormatTtl(60 * 60 * 24 * 31 * 2)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30 * 3, Locale.FormatTtl(60 * 60 * 24 * 31 * 3)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30 * 4, Locale.FormatTtl(60 * 60 * 24 * 31 * 4)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30 * 5, Locale.FormatTtl(60 * 60 * 24 * 31 * 5)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 30 * 6, Locale.FormatTtl(60 * 60 * 24 * 31 * 6)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 365, Locale.FormatTtl(60 * 60 * 24 * 365)),
        };

        public bool CanCreate => !string.IsNullOrWhiteSpace(Title);

        public async void Create(IEnumerable<User> users)
        {
            var maxSize = ClientService.Options.BasicGroupSizeMax;

            var peers = users.Select(x => x.Id).ToArray();
            if (peers.Length <= maxSize)
            {
                // Classic chat
                var response = await ClientService.SendAsync(new CreateNewBasicGroupChat(peers, _title, _ttl));
                if (response is CreatedBasicGroupChat chat)
                {
                    if (_inputPhoto != null)
                    {
                        ClientService.Send(new SetChatPhoto(chat.ChatId, _inputPhoto));
                    }

                    NavigationService.NavigateToChat(chat.ChatId);
                    NavigationService.GoBackAt(0, false);

                    if (chat.FailedToAddMembers?.FailedToAddMembersValue.Count > 0)
                    {
                        var popup = new ChatInviteFallbackPopup(ClientService, chat.ChatId, chat.FailedToAddMembers.FailedToAddMembersValue);
                        await ShowPopupAsync(popup);
                    }
                }
                else if (response is Error error)
                {
                    AlertsService.ShowAddUserAlert(XamlRoot, error.Message, false);
                }
            }
            else
            {

            }
        }

        public async void Create(TaskCompletionSource<Chat> completion)
        {
            var response = await ClientService.SendAsync(new CreateNewSupergroupChat(_title, false, false, string.Empty, null, _ttl, false));
            if (response is Chat chat)
            {
                if (_inputPhoto != null)
                {
                    ClientService.Send(new SetChatPhoto(chat.Id, _inputPhoto));
                }

                completion.TrySetResult(chat);
            }
            else
            {
                completion.TrySetResult(null);
            }
        }

        public async void ChoosePhoto()
        {
            _inputPhoto = await _profilePhotoService.PreviewSetPhotoAsync(NavigationService);
        }
    }
}
