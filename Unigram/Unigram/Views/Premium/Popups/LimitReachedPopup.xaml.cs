using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Premium.Popups
{
    public sealed partial class LimitReachedPopup : ContentPopup
    {
        private readonly INavigationService _navigationService;
        private readonly IProtoService _protoService;

        public LimitReachedPopup(INavigationService navigationService, IProtoService protoService, PremiumLimitType type)
        {
            _navigationService = navigationService;
            _protoService = protoService;

            InitializeComponent();
            InitializeLimit(protoService, type);

            Title = Strings.Resources.LimitReached;
        }

        private async void InitializeLimit(IProtoService protoService, PremiumLimitType type)
        {
            var limit = await GetPremiumLimitAsync(protoService, type) as PremiumLimit;
            if (limit != null)
            {
                var iconValue = string.Empty;
                var freeValue = string.Empty;
                var lockedValue = string.Empty;
                var premiumValue = string.Empty;

                var animatedValue = new Uri("ms-appx:///Assets/Animations/Double.json");

                switch (type)
                {
                    case PremiumLimitTypeChatFilterChosenChatCount:
                        iconValue = Icons.ChatFilled;
                        freeValue = Strings.Resources.LimitReachedChatInFolders;
                        lockedValue = Strings.Resources.LimitReachedChatInFoldersLocked;
                        premiumValue = Strings.Resources.LimitReachedChatInFoldersPremium;
                        break;
                    case PremiumLimitTypeChatFilterCount:
                        iconValue = Icons.FolderFilled;
                        freeValue = Strings.Resources.LimitReachedFolders;
                        lockedValue = Strings.Resources.LimitReachedFoldersLocked;
                        premiumValue = Strings.Resources.LimitReachedFoldersPremium;
                        break;
                    case PremiumLimitTypeCreatedPublicChatCount:
                        iconValue = Icons.LinkFilled;
                        freeValue = Strings.Resources.LimitReachedPublicLinks;
                        lockedValue = Strings.Resources.LimitReachedPublicLinksLocked;
                        premiumValue = Strings.Resources.LimitReachedPublicLinksPremium;
                        break;
                    case PremiumLimitTypePinnedArchivedChatCount:
                    case PremiumLimitTypePinnedChatCount:
                        iconValue = Icons.PinFilled;
                        freeValue = Strings.Resources.LimitReachedPinDialogs;
                        lockedValue = Strings.Resources.LimitReachedPinDialogsLocked;
                        premiumValue = Strings.Resources.LimitReachedPinDialogsPremium;
                        break;
                    case PremiumLimitTypeSupergroupCount:
                        iconValue = Icons.PeopleFilled;
                        freeValue = Strings.Resources.LimitReachedCommunities;
                        lockedValue = Strings.Resources.LimitReachedCommunitiesLocked;
                        premiumValue = Strings.Resources.LimitReachedCommunitiesPremium;
                        break;
                    case PremiumLimitTypeConnectedAccounts:
                        iconValue = Icons.PersonFilled;
                        freeValue = Strings.Resources.LimitReachedAccounts;
                        lockedValue = Strings.Resources.LimitReachedAccountsPremium;
                        premiumValue = Strings.Resources.LimitReachedAccountsPremium;

                        animatedValue = new Uri("ms-appx:///Assets/Animations/AddOne.json");
                        break;
                }

                if (protoService.IsPremium)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(premiumValue, limit.PremiumValue));

                    Icon.Text = iconValue;
                    Limit.Text = limit.PremiumValue.ToString();
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 0, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Right;

                    PrevArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = limit.DefaultValue.ToString();
                    NextLimit.Text = string.Empty;

                    PurchaseIcon.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.Resources.IncreaseLimit;
                }
                else if (protoService.IsPremiumAvailable)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(freeValue, limit.DefaultValue, limit.PremiumValue));

                    Icon.Text = iconValue;
                    Limit.Text = limit.DefaultValue.ToString();
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 14, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Center;

                    NextArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = string.Empty;
                    NextLimit.Text = limit.PremiumValue.ToString();

                    PurchaseIcon.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.Resources.IncreaseLimit;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(lockedValue, limit.DefaultValue));

                    LimitHeader.Visibility = Visibility.Collapsed;
                    LimitPanel.Visibility = Visibility.Collapsed;

                    PurchaseCommand.Style = App.Current.Resources["AccentButtonStyle"] as Style;
                    PurchaseIcon.Visibility = Visibility.Collapsed;
                    PurchaseLabel.Text = Strings.Resources.OK;
                }
            }
        }

        private Task<BaseObject> GetPremiumLimitAsync(IProtoService protoService, PremiumLimitType type)
        {
            if (type is PremiumLimitTypeConnectedAccounts)
            {
                return Task.FromResult<BaseObject>(new PremiumLimit(type, 3, 4));
            }

            if (protoService.IsPremiumAvailable)
            {
                return protoService.SendAsync(new GetPremiumLimit(type));
            }

            static Task<BaseObject> CreateLimit(PremiumLimitType type, long value)
            {
                return Task.FromResult<BaseObject>(new PremiumLimit(type, (int)value, (int)value));
            }

            switch (type)
            {
                case PremiumLimitTypeChatFilterChosenChatCount:
                    return CreateLimit(type, protoService.Options.ChatFilterChosenChatCountMax);
                case PremiumLimitTypeChatFilterCount:
                    return CreateLimit(type, protoService.Options.ChatFilterCountMax);
                case PremiumLimitTypeCreatedPublicChatCount:
                    return CreateLimit(type, 500);
                case PremiumLimitTypePinnedArchivedChatCount:
                    return CreateLimit(type, protoService.Options.PinnedArchivedChatCountMax);
                case PremiumLimitTypePinnedChatCount:
                    return CreateLimit(type, protoService.Options.PinnedChatCountMax);
                case PremiumLimitTypeSupergroupCount:
                    return CreateLimit(type, 10);
                case PremiumLimitTypeConnectedAccounts:
                    return CreateLimit(type, 4);
            }

            return null;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide();

            if (_protoService.IsPremiumAvailable && !_protoService.IsPremium)
            {
                _navigationService.ShowPromo(new PremiumSourceLimitExceeded());
            }
        }
    }
}
