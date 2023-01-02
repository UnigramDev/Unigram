//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsQuickReactionViewModel : TLViewModelBase
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
