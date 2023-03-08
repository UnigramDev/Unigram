//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Premium.Popups;

namespace Unigram.ViewModels.Premium
{
    public class PromoViewModel : TLViewModelBase
    {
        public PromoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Limits = new MvxObservableCollection<PremiumLimit>();
            Features = new MvxObservableCollection<PremiumFeature>();
        }

        private InternalLinkType _paymentLink;
        public InternalLinkType PaymentLink
        {
            get => _paymentLink;
            set => Set(ref _paymentLink, value);
        }

        public MvxObservableCollection<PremiumLimit> Limits { get; private set; }

        public MvxObservableCollection<PremiumFeature> Features { get; private set; }

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
            var state = await ClientService.SendAsync(new GetPremiumState()) as PremiumState;

            if (features == null || state == null)
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

            PaymentLink = features.PaymentLink;
            Limits.ReplaceWith(features.Limits);
            Features.ReplaceWith(features.Features);

            State = state;
            Option = state.PaymentOptions.FirstOrDefault();

            CanPurchase = features.PaymentLink != null
                && ClientService.IsPremiumAvailable;

            _animations = state.Animations.ToDictionary(x => x.Feature.GetType(), y => y.Animation);

            _stickers = await ClientService.SendAsync(new GetPremiumStickers()) as Stickers;
        }

        public string PremiumPreviewLimitsDescription
        {
            get
            {
                var channels = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeSupergroupCount)?.PremiumValue ?? 0;
                var folders = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeChatFilterCount)?.PremiumValue ?? 0;
                var pinned = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedChatCount)?.PremiumValue ?? 0;
                var links = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeCreatedPublicChatCount)?.PremiumValue ?? 0;
                var accounts = 4;

                return string.Format(Strings.Resources.PremiumPreviewLimitsDescription, channels, folders, pinned, links, accounts);
            }
        }

        public async Task<bool> OpenAsync(PremiumFeature feature)
        {
            ClientService.Send(new ViewPremiumFeature(feature));

            if (feature is PremiumFeatureIncreasedLimits)
            {
                var dialog = new LimitsPopup(ClientService, Option.PaymentOption, Limits);
                await dialog.ShowQueuedAsync(XamlRoot);

                if (dialog.ShouldPurchase && !ClientService.IsPremium)
                {
                    Purchase();
                    return false;
                }

                return true;
            }
            else
            {
                var dialog = new FeaturesPopup(ClientService, Option.PaymentOption, Features, _animations, _stickers, feature);
                await dialog.ShowQueuedAsync(XamlRoot);

                if (dialog.ShouldPurchase && !ClientService.IsPremium)
                {
                    Purchase();
                    return false;
                }

                return true;
            }
        }

        public void Purchase()
        {
            if (PaymentLink != null && !ClientService.IsPremium)
            {
                ClientService.Send(new ClickPremiumSubscriptionButton());
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, PaymentLink);
            }
        }
    }
}
