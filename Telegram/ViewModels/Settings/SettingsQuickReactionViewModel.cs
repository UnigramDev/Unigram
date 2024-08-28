//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Navigation;
using Telegram.Services;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsQuickReactionViewModel : ViewModelBase
    {
        public SettingsQuickReactionViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public bool IsQuickReplySelected
        {
            get => Settings.Appearance.IsQuickReplySelected;
            set
            {
                Settings.Appearance.IsQuickReplySelected = value;
                RaisePropertyChanged();
            }
        }
    }
}
