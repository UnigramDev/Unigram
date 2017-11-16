using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Strings;
using Unigram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBlockedUsersViewModel : UnigramViewModelBase, IHandle<TLUpdateUserBlocked>, IHandle
    {
        public SettingsBlockedUsersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLUser>();

            BlockCommand = new RelayCommand(BlockExecute);
            UnblockCommand = new RelayCommand<TLUser>(UnblockExecute);

            Aggregator.Subscribe(this);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateAsync();
        }

        public ObservableCollection<TLUser> Items { get; private set; }

        private async Task UpdateAsync()
        {
            Items.Clear();

            var response = await ProtoService.GetBlockedAsync(0, int.MaxValue);
            if (response.IsSucceeded)
            {
                Items.Clear();

                foreach (var contact in response.Result.Blocked)
                {
                    var user = CacheService.GetUser(contact.UserId) as TLUser;
                    if (user != null)
                    {
                        Items.Add(user);
                    }
                }
            }
        }

        public async void Handle(TLUpdateUserBlocked message)
        {
            var user = CacheService.GetUser(message.UserId) as TLUser;
            if (user != null)
            {
                BeginOnUIThread(() =>
                {
                    if (message.Blocked)
                    {
                        Items.Insert(0, user);
                    }
                    else
                    {
                        Items.Remove(user);
                    }
                });
            }
            else
            {
                var response = await ProtoService.GetFullUserAsync(new TLInputUser { UserId = message.UserId, AccessHash = 0 });
                if (response.IsSucceeded)
                {
                    BeginOnUIThread(() =>
                    {
                        if (message.Blocked)
                        {
                            Items.Insert(0, response.Result.User as TLUser);
                        }
                        else
                        {
                            Items.Remove(response.Result.User as TLUser);
                        }
                    });
                }
            }
        }

        public RelayCommand BlockCommand { get; }
        private void BlockExecute()
        {
            NavigationService.Navigate(typeof(SettingsBlockUserPage), new TLVector<TLUserBase>(Items));
        }

        public RelayCommand<TLUser> UnblockCommand { get; }
        private async void UnblockExecute(TLUser user)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = Strings.Android.AppName;
            dialog.Message = Strings.Android.AreYouSureUnblockContact;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.UnblockAsync(user.ToInputUser());
                if (response.IsSucceeded)
                {
                    Items.Remove(user);
                }
            }
        }

        public class ItemsCollection : IncrementalCollection<TLUser>
        {
            private readonly IMTProtoService _protoService;
            private readonly ICacheService _cacheService;

            public ItemsCollection(IMTProtoService protoService, ICacheService cacheService)
            {
                _protoService = protoService;
                _cacheService = cacheService;
            }

            public override async Task<IList<TLUser>> LoadDataAsync()
            {
                var response = await _protoService.GetBlockedAsync(Count, 1);
                if (response.IsSucceeded)
                {
                    var result = new List<TLUser>();

                    foreach (var contact in response.Result.Blocked)
                    {
                        var user = _cacheService.GetUser(contact.UserId) as TLUser;
                        if (user != null)
                        {
                            result.Add(user);
                        }
                    }

                    return result;
                }

                return new TLUser[0];
            }
        }
    }
}
