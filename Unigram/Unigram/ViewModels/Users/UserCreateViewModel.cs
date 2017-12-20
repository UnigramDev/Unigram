using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Models;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Users
{
    public class UserCreateViewModel : UnigramViewModelBase
    {
        public UserCreateViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProtoService.GotUserCountry += GotUserCountry;

            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrEmpty(_firstName) && !string.IsNullOrEmpty(_phoneCode) && !string.IsNullOrEmpty(_phoneNumber));
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (!string.IsNullOrEmpty(ProtoService.Country))
            {
                GotUserCountry(this, new CountryEventArgs { Country = ProtoService.Country });
            }

            IsLoading = false;
            return Task.CompletedTask;
        }

        private void GotUserCountry(object sender, CountryEventArgs e)
        {
            Country country = null;
            foreach (var local in Country.Countries)
            {
                if (string.Equals(local.Code, e.Country, StringComparison.OrdinalIgnoreCase))
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
            get
            {
                return _selectedCountry;
            }
            set
            {
                if (value != null)
                {
                    _phoneCode = value.PhoneCode;
                    RaisePropertyChanged(() => PhoneCode);
                }

                Set(ref _selectedCountry, value);
            }
        }

        private string _firstName;
        public string FirstName
        {
            get
            {
                return _firstName;
            }
            set
            {
                Set(ref _firstName, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _lastName;
        public string LastName
        {
            get
            {
                return _lastName;
            }
            set
            {
                Set(ref _lastName, value);
            }
        }

        private string _phoneCode;
        public string PhoneCode
        {
            get
            {
                return _phoneCode;
            }
            set
            {
                Set(ref _phoneCode, value);

                Country country = null;
                foreach (var c in Country.Countries)
                {
                    if (c.PhoneCode == PhoneCode)
                    {
                        if (c.PhoneCode == "1" && c.Code != "us")
                        {
                            continue;
                        }

                        if (c.PhoneCode == "7" && c.Code != "ru")
                        {
                            continue;
                        }

                        country = c;
                        break;
                    }
                }

                SelectedCountry = country;
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get
            {
                return _phoneNumber;
            }
            set
            {
                Set(ref _phoneNumber, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        public IList<Country> Countries { get; } = Country.Countries.OrderBy(x => x.DisplayName).ToList();

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var contact = new TLInputPhoneContact
            {
                ClientId = GetHashCode(),
                FirstName = FirstName,
                LastName = LastName,
                Phone = "+" + PhoneCode + PhoneNumber
            };

            var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact });
            if (response.IsSucceeded)
            {
                if (response.Result.Users.Count > 0)
                {
                    Aggregator.Publish(new TLUpdateContactLink
                    {
                        UserId = response.Result.Users[0].Id,
                        MyLink = new TLContactLinkContact(),
                        ForeignLink = new TLContactLinkUnknown()
                    });
                }
                else
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.ContactNotRegistered, Strings.Android.AppName, Strings.Android.Invite, Strings.Android.Cancel);
                }
            }
            else
            {
                await TLMessageDialog.ShowAsync(Strings.Android.ContactNotRegistered, Strings.Android.AppName, Strings.Android.Invite, Strings.Android.Cancel);
            }
        }
    }
}
