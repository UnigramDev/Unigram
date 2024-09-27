//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase, IChildViewModel, IDelegable<ISettingsDelegate>, IHandle
    {
        private readonly ISettingsSearchService _searchService;
        private readonly IStorageService _storageService;

        public ISettingsDelegate Delegate { get; set; }

        public SettingsViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator, ISettingsSearchService searchService)
            : base(clientService, settingsService, aggregator)
        {
            _searchService = searchService;
            _storageService = storageService;

            NavigateCommand = new RelayCommand<SettingsSearchEntry>(Navigate);

            Results = new MvxObservableCollection<SettingsSearchEntry>();
        }

        public IStorageService StorageService => _storageService;

        public string OwnedStarCount => ClientService.OwnedStarCount > 0
            ? ClientService.OwnedStarCount.ToString("N0")
            : string.Empty;

        public MvxObservableCollection<SettingsSearchEntry> Results { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUser(ClientService.Options.MyId, out User item))
            {
                Delegate?.UpdateUser(null, item, false);

                var cache = ClientService.GetUserFull(item.Id);
                if (cache == null)
                {
                    ClientService.Send(new GetUserFullInfo(item.Id));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(null, item, cache, false, false);
                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle)
                .Subscribe<UpdateUserFullInfo>(Handle)
                .Subscribe<UpdateOwnedStarCount>(Handle)
                .Subscribe<UpdateOption>(Handle);
        }

        public async void Activate()
        {
            await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
        }

        public void Deactivate() { }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == ClientService.Options.MyId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(null, update.User, false));
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == ClientService.Options.MyId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(null, ClientService.GetUser(update.UserId), update.UserFullInfo, false, false));
            }
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.IsPremium || update.Name == OptionsService.R.IsPremiumAvailable)
            {
                BeginOnUIThread(() => RaisePropertyChanged(nameof(IsPremiumAvailable)));
            }
        }



        public async void Ask()
        {
            var text = Regex.Replace(Strings.AskAQuestionInfo, "<!\\[CDATA\\[(.*?)\\]\\]>", "$1");

            var confirm = await ShowPopupAsync(text, Strings.AskAQuestion, Strings.AskButton, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new GetSupportUser());
                if (response is User user)
                {
                    response = await ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                    if (response is Chat chat)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                }
            }
        }

        public async void PremiumGifting()
        {
            var user = await ChooseChatsPopup.PickUserAsync(ClientService, NavigationService, Strings.SelectContact, false);
            if (user == null)
            {
                return;
            }

            var userFull = await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;
            if (userFull == null)
            {
                return;
            }

            await ShowPopupAsync(new GiftPopup(ClientService, NavigationService, user, userFull.PremiumGiftOptions));
        }

        public void Search(string query)
        {
            Results.ReplaceWith(_searchService.Search(query));
        }

        public RelayCommand<SettingsSearchEntry> NavigateCommand { get; }
        public void Navigate(SettingsSearchEntry entry)
        {
            if (entry is SettingsSearchPage page && page.Page != null)
            {
                if (page.Page == typeof(SettingsPasscodePage))
                {
                    NavigationService.NavigateToPasscode();
                }
                else if (page.Page == typeof(InstantPage))
                {
                    NavigationService.NavigateToInstant(Strings.TelegramFaqUrl);
                }
                //else if (page.Page == typeof(WalletPage))
                //{
                //    NavigationService.NavigateToWallet();
                //}
                else
                {
                    NavigationService.Navigate(page.Page, page.Parameter);
                }
            }
            else if (entry is SettingsSearchFaq faq)
            {
                NavigationService.NavigateToInstant(faq.Url);
            }
            else if (entry is SettingsSearchAction action)
            {
                action.Action();
            }
        }
    }
}
