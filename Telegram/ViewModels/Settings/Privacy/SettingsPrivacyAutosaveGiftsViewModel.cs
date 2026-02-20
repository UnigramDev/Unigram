//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

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
                _cached = fullInfo.GiftSettings;
                AllowLimited = fullInfo.GiftSettings.AcceptedGiftTypes.LimitedGifts;
                AllowUnlimited = fullInfo.GiftSettings.AcceptedGiftTypes.UnlimitedGifts;
                AllowUnique = fullInfo.GiftSettings.AcceptedGiftTypes.UpgradedGifts;
                AllowFromChannels = fullInfo.GiftSettings.AcceptedGiftTypes.GiftsFromChannels;
                AllowPremium = fullInfo.GiftSettings.AcceptedGiftTypes.PremiumSubscription;
                ShowIcon = fullInfo.GiftSettings.ShowGiftButton;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool _showIcon;
        public bool ShowIcon
        {
            get => _showIcon;
            set => Invalidate(ref _showIcon, value);
        }

        private bool _allowLimited;
        public bool AllowLimited
        {
            get => _allowLimited || !IsPremium;
            set => Invalidate(ref _allowLimited, value);
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
            set => Invalidate(ref _allowUnlimited, value);
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
            set => Invalidate(ref _allowUnique, value);
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

        private bool _allowFromChannels;
        public bool AllowFromChannels
        {
            get => _allowFromChannels || !IsPremium;
            set => Invalidate(ref _allowFromChannels, value);
        }

        public void ChangeAllowFromChannels()
        {
            if (ClientService.IsPremium)
            {
                AllowFromChannels = !AllowFromChannels;
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
            set => Invalidate(ref _allowPremium, value);
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

        private GiftSettings _cached;

        public override bool HasChanged => _cached != null && (base.HasChanged || !_cached.AreTheSame(GetSettings()));

        protected override async void ContinueImpl(NavigatingEventArgs args)
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo fullInfo))
            {
                var settings = GetSettings();

                if (!fullInfo.GiftSettings.AreTheSame(settings))
                {
                    var response = await ClientService.SendAsync(new SetGiftSettings(settings));
                    if (response is Error error)
                    {
                        ShowToast(error);
                        return;
                    }
                }
            }

            base.ContinueImpl(args);
        }

        private GiftSettings GetSettings()
        {
            return new GiftSettings
            {
                AcceptedGiftTypes = new AcceptedGiftTypes
                {
                    LimitedGifts = AllowLimited,
                    UnlimitedGifts = AllowUnlimited,
                    UpgradedGifts = AllowUnique,
                    GiftsFromChannels = AllowFromChannels,
                    PremiumSubscription = AllowPremium,
                },
                ShowGiftButton = ShowIcon
            };
        }
    }
}
