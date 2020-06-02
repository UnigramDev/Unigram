using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyPhoneViewModel : TLMultipleViewModelBase
    {
        private readonly SettingsPrivacyShowPhoneViewModel _showPhone;
        private readonly SettingsPrivacyAllowFindingByPhoneNumberViewModel _allowFindingByPhoneNumber;

        public SettingsPrivacyPhoneViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, SettingsPrivacyShowPhoneViewModel showPhone, SettingsPrivacyAllowFindingByPhoneNumberViewModel allowFindingByPhoneNumber)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _showPhone = showPhone;
            _allowFindingByPhoneNumber = allowFindingByPhoneNumber;

            Children.Add(showPhone);
            Children.Add(allowFindingByPhoneNumber);

            SendCommand = new RelayCommand(SendExecute);
        }

        public SettingsPrivacyShowPhoneViewModel ShowPhone => _showPhone;
        public SettingsPrivacyAllowFindingByPhoneNumberViewModel AllowFindingByPhoneNumber => _allowFindingByPhoneNumber;

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response1 = await ShowPhone.SendAsync();
            var response2 = await AllowFindingByPhoneNumber.SendAsync();
            if (response1 is Ok && response2 is Ok)
            {
                NavigationService.GoBack();
            }
        }
    }
}
