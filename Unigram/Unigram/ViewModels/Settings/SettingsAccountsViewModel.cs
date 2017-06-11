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
using Unigram.Common;
using Unigram.Views;
using Unigram.Views;
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
            Items = new ObservableCollection<string>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.Clear();

            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);
            foreach (var folder in folders)
            {
                if (Guid.TryParse(Path.GetFileName(folder), out Guid guid))
                {
                    Items.Add(guid.ToString());
                }
            }

            SelectedItem = SettingsHelper.SessionGuid;

            return Task.CompletedTask;
        }

        public ObservableCollection<string> Items { get; private set; }

        private string _selectedItem;
        public string SelectedItem
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

        public RelayCommand NewAccountCommand => new RelayCommand(NewAccountExecute);
        private void NewAccountExecute()
        {
            SettingsHelper.SwitchGuid = Guid.NewGuid().ToString();
            SettingsHelper.IsAuthorized = false;
            SettingsHelper.IsTestMode = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            App.Current.Exit();
        }

        private void SwitchAccount()
        {
            if (_selectedItem != SettingsHelper.SessionGuid && _selectedItem != null)
            {
                SettingsHelper.SwitchGuid = _selectedItem;
                App.Current.Exit();
            }
        }
    }
}
