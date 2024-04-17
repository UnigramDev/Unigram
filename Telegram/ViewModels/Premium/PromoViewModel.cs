//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Premium.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Premium
{
    public class PromoViewModel : ViewModelBase
    {
        public PromoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Limits = new MvxObservableCollection<PremiumLimit>();
            Features = new MvxObservableCollection<PremiumFeature>();
            BusinessFeatures = new MvxObservableCollection<BusinessFeature>();
        }

        public MvxObservableCollection<PremiumLimit> Limits { get; private set; }

        public MvxObservableCollection<PremiumFeature> Features { get; private set; }

        public MvxObservableCollection<BusinessFeature> BusinessFeatures { get; private set; }

        private Dictionary<Type, Animation> _animations;

        private Stickers _stickers;

        private PremiumState _state;
        public PremiumState State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        private PremiumStatePaymentOption _option;
        public PremiumStatePaymentOption Option
        {
            get => _option;
            set => Set(ref _option, value);
        }

        private bool _canPurchase;
        public bool CanPurchase
        {
            get => _canPurchase;
            set => Set(ref _canPurchase, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState _)
        {
            PremiumSource premiumSource = parameter is PremiumSource source ? source : new PremiumSourceSettings();

            var features = await ClientService.SendAsync(new GetPremiumFeatures(premiumSource)) as PremiumFeatures;
            if (features == null)
            {
                return;
            }

            var appIcons = features.Features.FirstOrDefault(x => x is PremiumFeatureAppIcons);
            if (appIcons != null)
            {
                features.Features.Remove(appIcons);
            }

            var archivedChats = features.Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedArchivedChatCount);
            if (archivedChats != null)
            {
                features.Limits.Remove(archivedChats);
            }

            features.Limits.Add(new PremiumLimit(new PremiumLimitTypeConnectedAccounts(), 3, 4));

            Limits.ReplaceWith(features.Limits);
            Features.ReplaceWith(features.Features);

            var state = await ClientService.SendAsync(new GetPremiumState()) as PremiumState;
            if (state == null)
            {
                return;
            }

            State = state;
            Option = state.PaymentOptions.LastOrDefault();

            CanPurchase = Option != null
                && ClientService.IsPremiumAvailable;

            _animations = state.Animations
                .DistinctBy(x => x.Feature.GetType())
                .ToDictionary(x => x.Feature.GetType(), y => y.Animation);

            _stickers = await ClientService.SendAsync(new GetPremiumStickerExamples()) as Stickers;

            var businessFeatures = await ClientService.SendAsync(new GetBusinessFeatures()) as BusinessFeatures;
            if (businessFeatures == null)
            {
                return;
            }

            BusinessFeatures.ReplaceWith(businessFeatures.Features);
        }

        public string PremiumPreviewLimitsDescription
        {
            get
            {
                var channels = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeSupergroupCount)?.PremiumValue ?? 0;
                var folders = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeChatFolderCount)?.PremiumValue ?? 0;
                var pinned = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedChatCount)?.PremiumValue ?? 0;
                var links = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeCreatedPublicChatCount)?.PremiumValue ?? 0;
                var accounts = 4;

                return string.Format(Strings.PremiumPreviewLimitsDescription, channels, folders, pinned, links, accounts);
            }
        }

        public async Task<bool> OpenAsync(PremiumFeature feature)
        {
            var popup = new FeaturesPopup(ClientService, Option?.PaymentOption, Features, BusinessFeatures, Limits, _animations, _stickers, feature);
            await ShowPopupAsync(popup);

            if (popup.ShouldPurchase && !ClientService.IsPremium)
            {
                Purchase();
                return false;
            }

            return true;
        }

        public void Purchase()
        {
            if (Option != null && !ClientService.IsPremium)
            {
                ClientService.Send(new ClickPremiumSubscriptionButton());
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, Option.PaymentOption.PaymentLink);
            }
        }
    }
}
