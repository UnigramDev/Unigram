using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Business.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public enum BusinessAwayScheduleType
    {
        AlwaysSend,
        OutsideBusinessHours,
        Custom
    }

    public partial class BusinessAwayViewModel : BusinessRecipientsViewModelBase, IHandle
    {
        public BusinessAwayViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (Invalidate(ref _isEnabled, value) && value)
                {
                    Replies ??= ClientService.GetQuickReplyShortcut("away");
                }
            }
        }

        private QuickReplyShortcut _replies;
        public QuickReplyShortcut Replies
        {
            get => _replies;
            set => Invalidate(ref _replies, value);
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var settings = cached?.BusinessInfo?.AwayMessageSettings;
            if (settings != null)
            {
                _cached = settings;

                IsEnabled = true;
                Replies = ClientService.GetQuickReplyShortcut(settings.ShortcutId);

                OfflineOnly = settings.OfflineOnly;

                if (settings.Schedule is BusinessAwayMessageScheduleAlways)
                {
                    SetScheduleType(BusinessAwayScheduleType.AlwaysSend, false);
                }
                else if (settings.Schedule is BusinessAwayMessageScheduleOutsideOfOpeningHours)
                {
                    SetScheduleType(BusinessAwayScheduleType.OutsideBusinessHours, false);
                }
                else if (settings.Schedule is BusinessAwayMessageScheduleCustom scheduleCustom)
                {
                    SetScheduleType(BusinessAwayScheduleType.Custom, false);

                    CustomStart = Formatter.ToLocalTime(scheduleCustom.StartDate);
                    CustomEnd = Formatter.ToLocalTime(scheduleCustom.EndDate);
                }

                UpdateRecipients(settings.Recipients);
            }
            else if (mode == NavigationMode.Back)
            {
                IsEnabled = true;
                Replies = ClientService.GetQuickReplyShortcut("away");
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateQuickReplyShortcut>(this, Handle);
        }

        private void Handle(UpdateQuickReplyShortcut update)
        {
            if (update.Shortcut.Name == "away")
            {
                BeginOnUIThread(() => Replies = update.Shortcut);
            }
        }

        public void Create()
        {
            _completed = true;
            NavigationService.Navigate(typeof(ChatBusinessRepliesPage), new ChatBusinessRepliesIdNavigationArgs("away"));
        }

        public bool IsAlwaysSend
        {
            get => _scheduleType == BusinessAwayScheduleType.AlwaysSend;
            set
            {
                if (value)
                {
                    SetScheduleType(BusinessAwayScheduleType.AlwaysSend);
                }
            }
        }

        public bool IsOutsideBusinessHours
        {
            get => _scheduleType == BusinessAwayScheduleType.OutsideBusinessHours;
            set
            {
                if (value)
                {
                    SetScheduleType(BusinessAwayScheduleType.OutsideBusinessHours);
                }
            }
        }

        public bool IsCustom
        {
            get => _scheduleType == BusinessAwayScheduleType.Custom;
            set
            {
                if (value)
                {
                    SetScheduleType(BusinessAwayScheduleType.Custom);
                }
            }
        }

        private BusinessAwayScheduleType _scheduleType;
        public BusinessAwayScheduleType ScheduleType
        {
            get => _scheduleType;
            set => SetScheduleType(value);
        }

        private void SetScheduleType(BusinessAwayScheduleType value, bool update = true)
        {
            if (Invalidate(ref _scheduleType, value, nameof(ScheduleType)))
            {
                RaisePropertyChanged(nameof(IsAlwaysSend));
                RaisePropertyChanged(nameof(IsOutsideBusinessHours));
                RaisePropertyChanged(nameof(IsCustom));

                if (IsCustom && update)
                {
                    CustomStart ??= DateTime.Now;
                    CustomEnd ??= DateTime.Now.AddDays(1);
                }
            }
        }

        private DateTime? _customStart;
        public DateTime? CustomStart
        {
            get => _customStart;
            set => Invalidate(ref _customStart, value);
        }

        private DateTime? _customEnd;
        public DateTime? CustomEnd
        {
            get => _customEnd;
            set => Invalidate(ref _customEnd, value);
        }

        public async void ChangeCustomStart()
        {
            var popup = new ChooseAwayPopup(Strings.BusinessAwayScheduleCustomStartTitle, DateTime.Now, CustomStart ?? DateTime.Now);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                CustomStart = popup.Value;
            }
        }

        public async void ChangeCustomEnd()
        {
            var popup = new ChooseAwayPopup(Strings.BusinessAwayScheduleCustomEndTitle, CustomStart ?? DateTime.Now, CustomEnd ?? (CustomStart ?? DateTime.Now).AddDays(1));

            var confirm = await ShowPopupAsync(popup);
            if (confirm == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                CustomEnd = popup.Value;
            }
        }

        private bool _offlineOnly;
        public bool OfflineOnly
        {
            get => _offlineOnly;
            set => Invalidate(ref _offlineOnly, value);
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        public override async void Continue()
        {
            if (IsEnabled && Replies == null)
            {
                RaisePropertyChanged("REPLIES_MISSING");
                return;
            }

            _completed = true;

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessAwayMessageSettings(settings));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private BusinessAwayMessageSettings _cached;
        private BusinessAwayMessageSettings GetSettings()
        {
            if (IsEnabled)
            {
                return new BusinessAwayMessageSettings
                {
                    ShortcutId = Replies?.Id ?? 0,
                    Schedule = ScheduleType switch
                    {
                        BusinessAwayScheduleType.AlwaysSend => new BusinessAwayMessageScheduleAlways(),
                        BusinessAwayScheduleType.OutsideBusinessHours => new BusinessAwayMessageScheduleOutsideOfOpeningHours(),
                        BusinessAwayScheduleType.Custom => new BusinessAwayMessageScheduleCustom
                        {
                            StartDate = (CustomStart ?? DateTime.Now).ToTimestamp(),
                            EndDate = (CustomEnd ?? DateTime.Now.AddDays(1)).ToTimestamp(),
                        },
                        _ => null
                    },
                    OfflineOnly = OfflineOnly,
                    Recipients = GetRecipients()
                };
            }

            return null;
        }
    }
}
