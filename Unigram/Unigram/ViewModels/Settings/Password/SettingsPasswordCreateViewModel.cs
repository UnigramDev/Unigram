using System.Collections.Generic;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Settings.Password;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordCreateViewModel : TLViewModelBase
    {
        public SettingsPasswordCreateViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _field1;
        public string Field1
        {
            get => _field1;
            set => Set(ref _field1, value);
        }

        private string _field2;
        public string Field2
        {
            get => _field2;
            set => Set(ref _field2, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var field1 = _field1;
            var field2 = _field2;

            if (string.IsNullOrWhiteSpace(field1))
            {
                // Error
                return;
            }

            if (!string.Equals(field1, field2))
            {
                // Error
                await MessagePopup.ShowAsync(Strings.Resources.PasswordDoNotMatch, Strings.Resources.AppName, Strings.Resources.OK);
                return;
            }

            var state = new Dictionary<string, object>
            {
                { "password", field1 }
            };

            NavigationService.Navigate(typeof(SettingsPasswordHintPage), state: state);
        }
    }
}
