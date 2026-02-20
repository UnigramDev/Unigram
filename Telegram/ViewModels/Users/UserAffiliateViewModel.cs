//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Chats;
using Telegram.Views.Users.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Users
{
    public partial class UserAffiliateViewModel : ViewModelBase
    {
        public UserAffiliateViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private long _userId;

        private AffiliateProgramInfo _cached;

        private AffiliateProgramInfo _info;
        public AffiliateProgramInfo Info
        {
            get => _info;
            set => Set(ref _info, value);
        }

        private int _minimumCommission;
        public int MinimumCommission
        {
            get => _minimumCommission;
            set => Set(ref _minimumCommission, value);
        }

        private int _commission;
        public int Commission
        {
            get => _commission;
            set
            {
                if (Set(ref _commission, value))
                {
                    RaisePropertyChanged(nameof(CanBeSaved));
                }
            }
        }

        private int _minimumDuration;
        public int MinimumDuration
        {
            get => _minimumDuration;
            set => Set(ref _minimumDuration, value);
        }

        private int _duration;
        public int Duration
        {
            get => _duration;
            set
            {
                if (Set(ref _duration, value))
                {
                    RaisePropertyChanged(nameof(CanBeSaved));
                }
            }
        }

        public bool CanBeSaved
        {
            get
            {
                if (_cached == null)
                {
                    return true;
                }
                else if (_cached.EndDate > 0)
                {
                    return _cached.EndDate < DateTime.Now.ToTimestamp();
                }

                return _cached.Parameters.CommissionPerMille != Commission
                    || _cached.Parameters.MonthCount != ConvertDurationBack(Duration);
            }
        }

        public void Reset()
        {
            _cached = null;
            Info = null;
            MinimumCommission = 1;
            MinimumDuration = 0;

            RaisePropertyChanged(nameof(CanBeSaved));
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long userId && ClientService.TryGetUser(userId, out User user) && ClientService.TryGetUserFull(userId, out UserFullInfo fullInfo))
            {
                _userId = userId;
                _cached = fullInfo.BotInfo.AffiliateProgram;
                Info = fullInfo.BotInfo.AffiliateProgram;
                MinimumCommission = Commission = fullInfo.BotInfo?.AffiliateProgram?.Parameters.CommissionPerMille ?? 1;
                MinimumDuration = Duration = ConvertDuration(fullInfo.BotInfo?.AffiliateProgram?.Parameters.MonthCount ?? 1);
            }

            return Task.CompletedTask;
        }

        private int ConvertDuration(int duration)
        {
            return duration switch
            {
                int value when value > 36 => 6,
                int value when value > 24 => 5,
                int value when value > 12 => 4,
                int value when value > 6 => 3,
                int value when value > 3 => 2,
                int value when value > 1 => 1,
                _ => 0
            };
        }

        private int ConvertDurationBack(int duration)
        {
            return duration switch
            {
                0 => 1,
                1 => 3,
                2 => 6,
                3 => 12,
                4 => 24,
                5 => 36,
                _ => 0
            };
        }

        public async void Continue()
        {
            var commission = Commission;
            var monthCount = ConvertDurationBack(Duration);

            var parameters = new AffiliateProgramParameters(commission, monthCount);

            var confirm = await ShowPopupAsync(new UserAffiliatePopup(_cached == null, parameters));
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new SetChatAffiliateProgram(_userId, parameters));
                if (response is Ok)
                {
                    ShowToast(string.Format("**{0}**\n{1}", Strings.AffiliateProgramStartedTitle, Strings.AffiliateProgramStartedText), ToastPopupIcon.Success);

                    NavigationService.GoBack();
                }
                else if (response is Error error)
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
            }
        }

        public async void Stop()
        {
            var message = Strings.AffiliateProgramStopText + "\n\n• " + Strings.AffiliateProgramStopText1 + "\n• " + Strings.AffiliateProgramStopText2 + "\n• " + Strings.AffiliateProgramStopText3;

            var confirm = await ShowPopupAsync(message, Strings.AffiliateProgramAlert, Strings.AffiliateProgramStopButton, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new SetChatAffiliateProgram(_userId, null));
                if (response is Ok)
                {
                    ShowToast(string.Format("**{0}**\n{1}", Strings.AffiliateProgramEndedTitle, Strings.AffiliateProgramEndedText), ToastPopupIcon.Success);

                    NavigationService.GoBack();
                }
                else if (response is Error error)
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
            }
        }

        public void Existing()
        {
            NavigationService.Navigate(typeof(ChatAffiliatePage), new AffiliateTypeBot(_userId));
        }
    }
}
