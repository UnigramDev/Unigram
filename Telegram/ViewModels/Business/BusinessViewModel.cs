using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Business;
using Telegram.Views.Business.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessViewModel : ViewModelBase
    {
        private IList<BusinessFeature> _features;
        private Dictionary<Type, Animation> _animations;

        public BusinessViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ObservableCollection<BusinessFeature>();
            PaymentOptions = new ObservableCollection<PremiumStatePaymentOption>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState _)
        {
            ClientService.Send(new GetUserFullInfo(ClientService.Options.MyId));

            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo fullInfo))
            {
                Set(ref _hasSponsoredMessagesEnabled, fullInfo.HasSponsoredMessagesEnabled, nameof(HasSponsoredMessagesEnabled));
            }

            if (Items.Empty())
            {
                var response = await ClientService.SendAsync(new GetBusinessFeatures());
                if (response is BusinessFeatures features)
                {
                    _features = features.Features.ToList();

                    foreach (var feature in features.Features)
                    {
                        if (feature is BusinessFeatureEmojiStatus or BusinessFeatureChatFolderTags or BusinessFeatureUpgradedStories)
                        {
                            continue;
                        }

                        Items.Add(feature);
                    }
                }
            }

            var state = await ClientService.SendAsync(new GetPremiumState()) as PremiumState;
            if (state == null)
            {
                return;
            }

            MonthlyOption = state.PaymentOptions.FirstOrDefault(x => x.PaymentOption.MonthCount == 1);
            AnnualOption = state.PaymentOptions.FirstOrDefault(x => x.PaymentOption.MonthCount == 12);
            CanPurchase = state.PaymentOptions.Count > 0 && !IsPremium;

            RaisePropertyChanged(nameof(SelectedOption));

            _animations = state.BusinessAnimations
                .DistinctBy(x => x.Feature.GetType())
                .ToDictionary(x => x.Feature.GetType(), y => y.Animation);
        }

        public ObservableCollection<BusinessFeature> Items { get; private set; }

        public ObservableCollection<PremiumStatePaymentOption> PaymentOptions { get; private set; }

        private PremiumStatePaymentOption _monthlyOption;
        public PremiumStatePaymentOption MonthlyOption
        {
            get => _monthlyOption;
            set => Set(ref _monthlyOption, value);
        }

        private PremiumStatePaymentOption _annualOption;
        public PremiumStatePaymentOption AnnualOption
        {
            get => _annualOption;
            set => Set(ref _annualOption, value);
        }

        private bool _isAnnualOptionSelected = true;
        public bool IsAnnualOptionSelected
        {
            get => _isAnnualOptionSelected;
            set
            {
                if (value)
                {
                    Set(ref _isAnnualOptionSelected, value);
                    RaisePropertyChanged(nameof(IsMonthlyOptionSelected));
                    RaisePropertyChanged(nameof(SelectedOption));
                }
            }
        }

        public bool IsMonthlyOptionSelected
        {
            get => !_isAnnualOptionSelected;
            set
            {
                if (value)
                {
                    Set(ref _isAnnualOptionSelected, !value);
                    RaisePropertyChanged(nameof(IsAnnualOptionSelected));
                    RaisePropertyChanged(nameof(SelectedOption));
                }
            }
        }

        public PremiumStatePaymentOption SelectedOption => IsAnnualOptionSelected
            ? AnnualOption
            : MonthlyOption;

        private bool _canPurchase;
        public bool CanPurchase
        {
            get => _canPurchase;
            set => Set(ref _canPurchase, value);
        }

        public void Purchase()
        {
            if (SelectedOption != null && CanPurchase)
            {
                ClientService.Send(new ClickPremiumSubscriptionButton());
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, SelectedOption.PaymentOption.PaymentLink);
            }
        }

        public async void OpenFeature(BusinessFeature feature)
        {
            if (IsPremium is false)
            {
                var popup = new BusinessFeaturesPopup(ClientService, null, _features, _animations, feature);
                await ShowPopupAsync(popup);

                if (popup.ShouldPurchase && !ClientService.IsPremium)
                {
                    //Purchase();
                }

                return;
            }

            switch (feature)
            {
                case BusinessFeatureGreetingMessage:
                    NavigationService.Navigate(typeof(BusinessGreetPage));
                    break;
                case BusinessFeatureAwayMessage:
                    NavigationService.Navigate(typeof(BusinessAwayPage));
                    break;
                case BusinessFeatureQuickReplies:
                    NavigationService.Navigate(typeof(BusinessRepliesPage));
                    break;
                case BusinessFeatureOpeningHours:
                    NavigationService.Navigate(typeof(BusinessHoursPage));
                    break;
                case BusinessFeatureLocation:
                    NavigationService.Navigate(typeof(BusinessLocationPage));
                    break;
                case BusinessFeatureBots:
                    NavigationService.Navigate(typeof(BusinessBotsPage));
                    break;
                case BusinessFeatureStartPage:
                    NavigationService.Navigate(typeof(BusinessIntroPage));
                    break;
                case BusinessFeatureAccountLinks:
                    NavigationService.Navigate(typeof(BusinessChatLinksPage));
                    break;
            }
        }

        private bool _hasSponsoredMessagesEnabled;
        public bool HasSponsoredMessagesEnabled
        {
            get => _hasSponsoredMessagesEnabled;
            set => SetHasSponsoredMessagesEnabled(value);
        }

        private void SetHasSponsoredMessagesEnabled(bool value)
        {
            if (Set(ref _hasSponsoredMessagesEnabled, value, nameof(HasSponsoredMessagesEnabled)))
            {
                ClientService.Send(new ToggleHasSponsoredMessagesEnabled(value));
            }
        }
    }
}
