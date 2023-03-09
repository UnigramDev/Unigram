//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsAutoDeleteViewModel : TLViewModelBase
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

        public ObservableCollection<SettingsOptionItem<int>> Items { get; } = new()
        {
            new SettingsOptionItem<int>(0,                 Strings.Resources.ShortMessageLifetimeForever),
            new SettingsOptionItem<int>(60 * 60 * 24,      Locale.FormatTtl(60 * 60 * 24)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 7,  Locale.FormatTtl(60 * 60 * 24 * 7)),
            new SettingsOptionItem<int>(60 * 60 * 24 * 31, Locale.FormatTtl(60 * 60 * 24 * 31))
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
                var confirm = await ShowPopupAsync(string.Format(Strings.Resources.AutoDeleteConfirmMessage, Locale.FormatTtl(value)), Strings.Resources.MessageLifetime, Strings.Resources.AutoDeleteConfirm, Strings.Resources.Cancel);
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

                Items.Add(new SettingsOptionItem<int>(value, Locale.FormatTtl(value)) { IsChecked = true });
            }

            ClientService.Send(new SetDefaultMessageAutoDeleteTime(new MessageAutoDeleteTime(value)));
        }
    }
}
