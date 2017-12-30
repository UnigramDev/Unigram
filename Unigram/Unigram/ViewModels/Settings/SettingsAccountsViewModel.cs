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
using Template10.Common;
using Unigram.Common;
using Unigram.Views;
using Unigram.Views;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAccountsViewModel : UnigramViewModelBase
    {
        public SettingsAccountsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<int>();

            NewAccountCommand = new RelayCommand(NewAccountExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.Clear();

            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);
            foreach (var folder in folders)
            {
                if (int.TryParse(Path.GetFileName(folder), out int guid))
                {
                    Items.Add(guid);
                }
            }

            SelectedItem = SettingsHelper.SelectedAccount;

            return Task.CompletedTask;
        }

        public ObservableCollection<int> Items { get; private set; }

        private int _selectedItem;
        public int SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                SwitchAccount();
            }
        }

        public RelayCommand NewAccountCommand { get; }
        private void NewAccountExecute()
        {
            var index = 0;
            // TODO: implement
            index++;

            SettingsHelper.SwitchAccount = index;
            SettingsHelper.IsAuthorized = false;
            SettingsHelper.IsTestMode = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            App.Current.Exit();
        }

        private async void SwitchAccount()
        {
            if (_selectedItem != SettingsHelper.SelectedAccount && _selectedItem >= 0)
            {
                //SettingsHelper.SwitchAccount = _selectedItem;
                //await CoreApplication.RequestRestartAsync(string.Empty);



                WindowWrapper.Current().NavigationServices.Remove(NavigationService);
                BootStrapper.Current.NavigationService.Reset();
            }
        }
    }
}
