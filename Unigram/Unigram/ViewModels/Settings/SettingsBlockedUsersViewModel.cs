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

                var blocked = response.Result as TLContactsBlocked;
                if (blocked != null)
                {
                    foreach (var contact in blocked.Blocked)
                    {
                        var user = CacheService.GetUser(contact.UserId) as TLUser;
                        if (user != null)
                        {
                            Items.Add(user);
                        }
                    }
                }
            }
        }

        public async void Handle(TLUpdateUserBlocked message)
        {
            var user = CacheService.GetUser(message.UserId) as TLUser;
            if (user != null)
            {
                Execute.BeginOnUIThread(() =>
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
                    Execute.BeginOnUIThread(() =>
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

        public RelayCommand BlockCommand => new RelayCommand(BlockExecute);
        private void BlockExecute()
        {
            NavigationService.Navigate(typeof(SettingsBlockUserPage), new TLVector<TLUserBase>(Items));
        }

        public RelayCommand<TLUser> UnblockCommand => new RelayCommand<TLUser>(UnblockExecute);
        private async void UnblockExecute(TLUser user)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = "Unblock";
            dialog.Message = "You sure?";
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.UnblockAsync(user.ToInputUser());
                if (response.IsSucceeded)
                {
                    Items.Remove(user);
                }
            }
        }
    }
}
