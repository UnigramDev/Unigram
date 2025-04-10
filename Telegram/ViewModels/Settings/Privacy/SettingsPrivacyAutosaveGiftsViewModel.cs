//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Settings.Privacy
{
    public partial class SettingsPrivacyAutosaveGiftsViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAutosaveGiftsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAutosaveGifts())
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo fullInfo))
            {
                AllowLimited = fullInfo.GiftSettings.AcceptedGiftTypes.LimitedGifts;
                AllowUnlimited = fullInfo.GiftSettings.AcceptedGiftTypes.UnlimitedGifts;
                AllowUnique = fullInfo.GiftSettings.AcceptedGiftTypes.UpgradedGifts;
                AllowPremium = fullInfo.GiftSettings.AcceptedGiftTypes.PremiumSubscription;
                ShowIcon = fullInfo.GiftSettings.ShowGiftButton;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool _showIcon;
        public bool ShowIcon
        {
            get => _showIcon;
            set => Set(ref _showIcon, value);
        }

        private bool _allowLimited;
        public bool AllowLimited
        {
            get => _allowLimited || !IsPremium;
            set => Set(ref _allowLimited, value);
        }

        public void ChangeAllowLimited()
        {
            if (ClientService.IsPremium)
            {
                AllowLimited = !AllowLimited;
            }
            else
            {
                ShowFeaturePromo();
            }
        }

        private bool _allowUnlimited;
        public bool AllowUnlimited
        {
            get => _allowUnlimited || !IsPremium;
            set => Set(ref _allowUnlimited, value);
        }

        public void ChangeAllowUnlimited()
        {
            if (ClientService.IsPremium)
            {
                AllowUnlimited = !AllowUnlimited;
            }
            else
            {
                ShowFeaturePromo();
            }
        }

        private bool _allowUnique;
        public bool AllowUnique
        {
            get => _allowUnique || !IsPremium;
            set => Set(ref _allowUnique, value);
        }

        public void ChangeAllowUnique()
        {
            if (ClientService.IsPremium)
            {
                AllowUnique = !AllowUnique;
            }
            else
            {
                ShowFeaturePromo();
            }
        }

        private bool _allowPremium;
        public bool AllowPremium
        {
            get => _allowPremium || !IsPremium;
            set => Set(ref _allowPremium, value);
        }

        public void ChangeAllowPremium()
        {
            if (ClientService.IsPremium)
            {
                AllowPremium = !AllowPremium;
            }
            else
            {
                ShowFeaturePromo();
            }
        }

        private void ShowFeaturePromo()
        {
            ToastPopup.ShowFeaturePromo(NavigationService, null);
        }

        public override async void Save()
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo fullInfo))
            {
                var settings = new GiftSettings
                {
                    AcceptedGiftTypes = new AcceptedGiftTypes
                    {
                        LimitedGifts = AllowLimited,
                        UnlimitedGifts = AllowUnlimited,
                        UpgradedGifts = AllowUnique,
                        PremiumSubscription = AllowPremium,
                    },
                    ShowGiftButton = ShowIcon
                };

                if (!fullInfo.GiftSettings.AreTheSame(settings))
                {
                    var response = await ClientService.SendAsync(new SetGiftSettings(settings));
                    if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                        return;
                    }
                }
            }

            base.Save();
        }
    }
}
