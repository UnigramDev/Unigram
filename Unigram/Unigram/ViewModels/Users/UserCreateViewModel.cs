using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Users
{
    public class UserCreateViewModel : UnigramViewModelBase
    {
        public UserCreateViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrEmpty(_firstName) && !string.IsNullOrEmpty(_phoneCode) && !string.IsNullOrEmpty(_phoneNumber));
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (!string.IsNullOrEmpty(LegacyService.Country))
            //{
            //    GotUserCountry(this, new CountryEventArgs { Country = LegacyService.Country });
            //}

            IsLoading = false;
            return Task.CompletedTask;
        }

        //private void GotUserCountry(object sender, CountryEventArgs e)
        //{
        //    Country country = null;
        //    foreach (var local in Country.Countries)
        //    {
        //        if (string.Equals(local.Code, e.Country, StringComparison.OrdinalIgnoreCase))
        //        {
        //            country = local;
        //            break;
        //        }
        //    }

        //    if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
        //    {
        //        BeginOnUIThread(() =>
        //        {
        //            SelectedCountry = country;
        //        });
        //    }
        //}

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
            var response = await ProtoService.SendAsync(new ImportContacts(new[] { new Contact() }));
            if (response is ImportedContacts imported)
            {
                if (imported.UserIds.Count > 0)
                {
                    var create = await ProtoService.SendAsync(new CreatePrivateChat(imported.UserIds[0], false));
                    if (create is Chat chat)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                }
                else
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.ContactNotRegistered, Strings.Resources.AppName, Strings.Resources.Invite, Strings.Resources.Cancel);
                }
            }
            else
            {
                await TLMessageDialog.ShowAsync(Strings.Resources.ContactNotRegistered, Strings.Resources.AppName, Strings.Resources.Invite, Strings.Resources.Cancel);
            }
        }
    }
}
