//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsAutoDeleteViewModel : ViewModelBase
    {
        public SettingsAutoDeleteViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items[0].IsChecked = true;
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetDefaultMessageAutoDeleteTime());
            if (response is MessageAutoDeleteTime messageTtl)
            {
                UpdateSelection(messageTtl.Time, false);
            }
        }

        public ObservableCollection<SettingsAutoDeleteItem> Items { get; } = new()
        {
            new SettingsAutoDeleteItem(0,                 Strings.ShortMessageLifetimeForever),
            new SettingsAutoDeleteItem(60 * 60 * 24,      Locale.FormatTtl(60 * 60 * 24)),
            new SettingsAutoDeleteItem(60 * 60 * 24 * 7,  Locale.FormatTtl(60 * 60 * 24 * 7)),
            new SettingsAutoDeleteItem(60 * 60 * 24 * 31, Locale.FormatTtl(60 * 60 * 24 * 31))
        };

        public async void SetCustomTime()
        {
            var popup = new ChatTtlPopup(ChatTtlType.Auto);
            var confirm = await ShowPopupAsync(popup);

            if (confirm == ContentDialogResult.Primary)
            {
                UpdateSelection(popup.Value, true);
            }
        }

        public async void UpdateSelection(int value, bool sync)
        {
            var selected = Items.FirstOrDefault(x => x.IsChecked);
            if (selected?.Value == value)
            {
                return;
            }

            if (sync && value != 0 && selected?.Value == 0)
            {
                var confirm = await ShowPopupAsync(string.Format(Strings.AutoDeleteConfirmMessage, Locale.FormatTtl(value)), Strings.MessageLifetime, Strings.AutoDeleteConfirm, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            if (selected != null)
            {
                selected.IsChecked = false;
            }

            var already = Items.FirstOrDefault(x => x.Value == value);
            if (already != null)
            {
                already.IsChecked = true;
            }
            else
            {
                if (Items.Count >= 5)
                {
                    Items.RemoveAt(4);
                }

                Items.Add(new SettingsAutoDeleteItem(value, Locale.FormatTtl(value))
                {
                    IsChecked = true
                });
            }

            ClientService.Send(new SetDefaultMessageAutoDeleteTime(new MessageAutoDeleteTime(value)));
        }
    }

    public class SettingsAutoDeleteItem : BindableBase
    {
        public SettingsAutoDeleteItem(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public string Text { get; set; }

        public int Value { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
    }
}
