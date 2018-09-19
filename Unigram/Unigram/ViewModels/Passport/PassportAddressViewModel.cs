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

namespace Unigram.ViewModels.Passport
{
    public class PassportAddressViewModel : TLViewModelBase
    {
        public PassportAddressViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var address = parameter as PassportElementAddress;
            if (address == null)
            {
                CanDelete = false;
                Address = new Address();
            }
            else
            {
                CanDelete = true;
                Address = address.Address;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool _canDelete;
        public bool CanDelete
        {
            get
            {
                return _canDelete;
            }
            set
            {
                Set(ref _canDelete, value);
            }
        }

        private Address _address;
        public Address Address
        {
            get
            {
                return _address;
            }
            set
            {
                Set(ref _address, value);
            }
        }

        public IList<Country> Countries { get; } = Country.Countries.OrderBy(x => x.DisplayName).ToList();

        private Country _selectedCountry = Country.Countries[0];
        public Country SelectedCountry
        {
            get
            {
                return _selectedCountry;
            }
            set
            {
                Set(ref _selectedCountry, value);
            }
        }



        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var address = _address;
            if (address == null)
            {
                return;
            }

            bool hasStreet = address.StreetLine1.Length > 0;
            bool hasCity = address.City.Length >= 2;
            bool hasCountry = address.CountryCode.Length > 0;
            bool hasStateIfNeeded = !address.CountryCode.Equals("US") || address.State.Length >= 2;
            bool hasPostcode = address.PostalCode.Length > 0 && address.PostalCode.Length <= 12;
            bool hasAddress = (hasStreet && hasCity && hasCountry && hasStateIfNeeded && hasPostcode); //|| _documentOnly;

            if (!hasStreet)
            {
                RaisePropertyChanged("ADDRESS_STREET_LINE1_INVALID");
                return;
            }

            if (!hasStateIfNeeded)
            {
                RaisePropertyChanged("ADDRESS_STATE_INVALID");
                return;
            }

            if (!hasPostcode)
            {
                RaisePropertyChanged("ADDRESS_POSTCODE_INVALID");
                return;
            }

            if (!hasCity)
            {
                RaisePropertyChanged("ADDRESS_CITY_INVALID");
                return;
            }

            if (!hasCountry)
            {
                RaisePropertyChanged("ADDRESS_COUNTRY_INVALID");
                return;
            }

            var element = new InputPassportElementAddress(_address);
            var response = await ProtoService.SendAsync(new SetPassportElement(element, string.Empty));
            if (response is PassportElement)
            {
                NavigationService.GoBack();
            }
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.PassportDeleteDocumentAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new DeletePassportElement(new PassportElementTypeAddress()));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
        }
    }
}
