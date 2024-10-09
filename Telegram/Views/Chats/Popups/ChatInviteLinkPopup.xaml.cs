using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats.Popups
{
    public partial record SelectionValue(int Value, string Text, bool IsCustom = false);

    public sealed partial class ChatInviteLinkPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;
        private readonly string _inviteLink;
        private readonly bool _subscription;

        private readonly ObservableCollection<SelectionValue> _limitByPeriod;

        private readonly ObservableCollection<SelectionValue> _limitNumberOfUses = new()
        {
            new SelectionValue(0, Strings.NoLimit),
            new SelectionValue(1, "1"),
            new SelectionValue(10, "10"),
            new SelectionValue(100, "100"),
            new SelectionValue(int.MaxValue, Strings.LimitNumberOfUsesCustom)
        };

        public ChatInviteLinkPopup(IClientService clientService, long chatId, bool channel, bool canCreateJoinRequests, ChatInviteLink inviteLink)
        {
            InitializeComponent();

            _clientService = clientService;
            _chatId = chatId;
            _inviteLink = inviteLink?.InviteLink;
            _subscription = inviteLink?.SubscriptionPricing != null;

            var time = DateTime.Now.ToTimestamp();

            _limitByPeriod = new()
            {
                new SelectionValue(0, Strings.NoLimit),
                new SelectionValue(time + 60 * 60, Locale.Declension(Strings.R.Hours, 1)),
                new SelectionValue(time + 60 * 60 * 24, Locale.Declension(Strings.R.Days, 1)),
                new SelectionValue(time + 60 * 60 * 24 * 7, Locale.Declension(Strings.R.Weeks, 1)),
                new SelectionValue(int.MaxValue, Strings.LimitByPeriodCustom)
            };

            RequireMonthlyFeeRoot.Visibility = channel && clientService.IsPremiumAvailable
                ? Visibility.Visible
                : Visibility.Collapsed;

            CreatesJoinRequestRoot.Visibility = canCreateJoinRequests
                ? Visibility.Visible
                : Visibility.Collapsed;

            LimitByPeriod.ItemsSource = _limitByPeriod;
            LimitNumberOfUses.ItemsSource = _limitNumberOfUses;

            if (inviteLink != null)
            {
                Title = Strings.EditLink;

                LinkName.Text = inviteLink.Name;
                CreatesJoinRequest.IsChecked = inviteLink.CreatesJoinRequest && canCreateJoinRequests;

                InsertLimitByPeriod(inviteLink.ExpirationDate);
                InsertLimitNumberOfUses(inviteLink.MemberLimit);

                if (inviteLink.SubscriptionPricing != null)
                {
                    RequireMonthlyFee.IsChecked = true;
                    RequireMonthlyFee.IsEnabled = false;
                    RequireMonthlyFeeNumber.Text = inviteLink.SubscriptionPricing.StarCount.ToString();
                    RequireMonthlyFeeNumber.IsReadOnly = true;

                    CreatesJoinRequest.IsEnabled = false;
                    CreatesJoinRequestInfo.Text = Strings.ApproveNewMembersDescriptionFrozen;

                    RequireMonthlyFeeInfo.Text = Strings.RequireMonthlyFeeInfoFrozen;

                    LimitByPeriod.IsEnabled = false;
                    LimitNumberOfUses.IsEnabled = false;
                }

                PrimaryButtonText = Strings.Edit;
            }
            else
            {
                Title = Strings.NewLink;

                InsertLimitByPeriod(0);
                InsertLimitNumberOfUses(0);

                PrimaryButtonText = Strings.Create;
            }

            SecondaryButtonText = Strings.Cancel;
        }

        public Function Request { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var name = LinkName.Text;
            var expirationDate = 0;
            var memberLimit = 0;
            var createsJoinRequest = false;

            if (CreatesJoinRequest.IsChecked == true)
            {
                createsJoinRequest = true;
            }
            else if (LimitNumberOfUses.SelectedItem is SelectionValue limitNumberOfUses)
            {
                memberLimit = limitNumberOfUses.Value;
            }

            if (LimitByPeriod.SelectedItem is SelectionValue limitByPeriod)
            {
                expirationDate = limitByPeriod.Value;
            }

            if (_inviteLink != null)
            {
                if (_subscription)
                {
                    Request = new EditChatSubscriptionInviteLink(_chatId, _inviteLink, name);
                }
                else
                {
                    Request = new EditChatInviteLink(_chatId, _inviteLink, name, expirationDate, memberLimit, createsJoinRequest);
                }
            }
            else
            {
                if (RequireMonthlyFee.IsChecked == true)
                {
                    IsRequireMonthlyFeeValid(RequireMonthlyFeeNumber.Text, out int value);
                    Request = new CreateChatSubscriptionInviteLink(_chatId, name, new StarSubscriptionPricing(2592000, value));
                }
                else
                {
                    Request = new CreateChatInviteLink(_chatId, name, expirationDate, memberLimit, createsJoinRequest);
                }
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void RequireMonthlyFee_Checked(object sender, RoutedEventArgs e)
        {
            RequireMonthlyFeeValue.Visibility = RequireMonthlyFee.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            CreatesJoinRequest.IsEnabled = RequireMonthlyFee.IsChecked != true;
            CreatesJoinRequestInfo.Text = RequireMonthlyFee.IsChecked == true
                ? Strings.ApproveNewMembersDescriptionFrozen
                : Strings.ApproveNewMembersDescription;

            UpdateLimitsVisibility();
        }

        private void CreatesJoinRequest_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLimitsVisibility();
        }

        private void UpdateLimitsVisibility()
        {
            var subscription = RequireMonthlyFee.IsChecked == true;
            var createsJoinRequest = CreatesJoinRequest.IsChecked == true;

            if (subscription)
            {
                LimitByPeriodRoot.Visibility = Visibility.Collapsed;
                LimitNumberOfUsesRoot.Visibility = Visibility.Collapsed;
            }
            else if (createsJoinRequest)
            {
                LimitByPeriodRoot.Visibility = Visibility.Visible;
                LimitNumberOfUsesRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                LimitByPeriodRoot.Visibility = Visibility.Visible;
                LimitNumberOfUsesRoot.Visibility = Visibility.Visible;
            }
        }

        private void RequireMonthlyFeeNumber_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = !IsRequireMonthlyFeeValid(args.NewText, out _);
        }

        private void RequireMonthlyFeeNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsRequireMonthlyFeeValid(RequireMonthlyFeeNumber.Text, out int value))
            {
                if (value > 0)
                {
                    var xtr = value / 1000d;
                    var usd = xtr * _clientService.Options.ThousandStarToUsdRate;

                    var format = Formatter.FormatAmount((long)usd, "USD");

                    RequireMonthlyFeeConversion.Text = string.Format(Strings.RequireMonthlyFeePrice, format);

                    var index = RequireMonthlyFeeNumber.SelectionStart;
                    RequireMonthlyFeeNumber.Text = value.ToString();
                    RequireMonthlyFeeNumber.SelectionStart = index;
                }
                else
                {
                    RequireMonthlyFeeConversion.Text = string.Empty;
                }
            }
            else
            {
                RequireMonthlyFeeConversion.Text = string.Empty;
            }
        }

        private bool IsRequireMonthlyFeeValid(string text, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                //if (value >= 1 && value <= _clientService.Options.SubscriptionStarCountMax)
                //{
                //    return true;
                //}

                value = Math.Clamp(value, 1, (int)_clientService.Options.SubscriptionStarCountMax);
                return true;
            }

            return string.IsNullOrEmpty(text);
        }

        private async void LimitByPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LimitByPeriod.SelectedItem is SelectionValue value)
            {
                if (value.Value == int.MaxValue)
                {
                    var popup = new ChooseDateTimeToast
                    {
                        Title = Strings.ExpireAfter,
                        //Header = Strings.PaidContentPriceTitle,
                        ActionButtonContent = Strings.OK,
                        ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                        CloseButtonContent = Strings.Cancel,
                        PreferredPlacement = TeachingTipPlacementMode.Center,
                        IsLightDismissEnabled = true,
                        ShouldConstrainToRootBounds = true,
                    };

                    var confirm = await popup.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        InsertLimitByPeriod((int)popup.Value.ToTimestamp());
                    }
                    else if (e.RemovedItems?.Count > 0)
                    {
                        LimitByPeriod.SelectedItem = e.RemovedItems[0];
                    }
                }
                else if (value.IsCustom is false)
                {
                    for (int i = 0; i < _limitByPeriod.Count; i++)
                    {
                        if (_limitByPeriod[i].IsCustom)
                        {
                            _limitByPeriod.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void InsertLimitByPeriod(int value)
        {
            for (int i = 0; i < _limitByPeriod.Count; i++)
            {
                if (_limitByPeriod[i].Value == value)
                {
                    LimitByPeriod.SelectedIndex = i;
                    break;
                }
                else if (_limitByPeriod[i].Value > value)
                {
                    _limitByPeriod.Insert(i, new SelectionValue(value, Formatter.DateAt(value), true));
                    LimitByPeriod.SelectedIndex = i;
                    break;
                }
            }
        }

        private async void LimitNumberOfUses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LimitNumberOfUses.SelectedItem is SelectionValue value)
            {
                if (value.Value == int.MaxValue)
                {
                    var popup = new InputTeachingTip(InputPopupType.Value)
                    {
                        Value = 1,
                        Minimum = 1,
                        Maximum = 99999,
                        Title = Strings.UsesLimitHint,
                        //Header = Strings.PaidContentPriceTitle,
                        ActionButtonContent = Strings.OK,
                        ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                        CloseButtonContent = Strings.Cancel,
                        PreferredPlacement = TeachingTipPlacementMode.Center,
                        IsLightDismissEnabled = true,
                        ShouldConstrainToRootBounds = true,
                    };

                    var confirm = await popup.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        InsertLimitNumberOfUses((int)popup.Value);
                    }
                    else if (e.RemovedItems?.Count > 0)
                    {
                        LimitNumberOfUses.SelectedItem = e.RemovedItems[0];
                    }
                }
                else if (value.IsCustom is false)
                {
                    for (int i = 0; i < _limitNumberOfUses.Count; i++)
                    {
                        if (_limitNumberOfUses[i].IsCustom)
                        {
                            _limitNumberOfUses.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void InsertLimitNumberOfUses(int value)
        {
            for (int i = 0; i < _limitNumberOfUses.Count; i++)
            {
                if (_limitNumberOfUses[i].Value == value)
                {
                    LimitNumberOfUses.SelectedIndex = i;
                    break;
                }
                else if (_limitNumberOfUses[i].Value > value)
                {
                    _limitNumberOfUses.Insert(i, new SelectionValue(value, value.ToString(), true));
                    LimitNumberOfUses.SelectedIndex = i;
                    break;
                }
            }
        }
    }
}
