using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Views.Settings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPhoneViewModel : TLViewModelBase
    {
        public SettingsPhoneViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetCountryCode(), result =>
            {
                if (result is Text text)
                {
                    BeginOnUIThread(() => GotUserCountry(text.TextValue));
                }
            });

            IsLoading = false;
            return Task.CompletedTask;
        }

        private void GotUserCountry(string code)
        {
            Country country = null;
            foreach (var local in Country.Countries)
            {
                if (string.Equals(local.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    country = local;
                    break;
                }
            }

            if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
            {
                BeginOnUIThread(() =>
                {
                    SelectedCountry = country;
                });
            }
        }

        private Country _selectedCountry;
        public Country SelectedCountry
        {
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => Set(ref _phoneNumber, value);
        }

        public IList<Country> Countries { get; } = Country.Countries.OrderBy(x => x.DisplayName).ToList();

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var phoneNumber = _phoneNumber?.Trim('+').Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                RaisePropertyChanged("PHONE_NUMBER_INVALID");
                return;
            }

            IsLoading = true;

            await ProtoService.SendAsync(new SetOption("x_phonenumber", new OptionValueString(phoneNumber)));

            var response = await ProtoService.SendAsync(new ChangePhoneNumber(phoneNumber, new PhoneNumberAuthenticationSettings(false, false, false)));
            if (response is AuthenticationCodeInfo info)
            {
                App.Current.SessionState["x_codeinfo"] = info;
                NavigationService.Navigate(typeof(SettingsPhoneSentCodePage));
            }
            else if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_FLOOD))
                {
                    await MessagePopup.ShowAsync("Sorry, you have deleted and re-created your account too many times recently. Please wait for a few days before signing up again.", "Telegram", "OK");
                }
                else
                {
                    await new MessagePopup(error.Message ?? "Error message", error.Code.ToString()).ShowQueuedAsync();
                }
            }
        }
    }
}