using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Views;
using Windows.UI.Xaml;

namespace Unigram.ViewModels
{
    public class LoginWelcomeViewModel : UnigramViewModelBase
    {
        public LoginWelcomeViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<WelcomeTour>();

            // TODO: put them in a separate file?
            // TODO: localization
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Welcome to Unigram", Text = "Unigram is an UWP Telegram-app built by the Windows Community, for the Windows Community" });
            SelectedItem = Items[0];
        }

        private RelayCommand _continueCommand;
        public RelayCommand ContinueCommand => _continueCommand = (_continueCommand ?? new RelayCommand(ContinueExecute, () => SelectedItem == Items.Last()));
        private void ContinueExecute()
        {
            NavigationService.Navigate(typeof(LoginPhoneNumberPage));
        }

        private WelcomeTour _selectedItem;
        public WelcomeTour SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                ContinueCommand.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<WelcomeTour> Items { get; private set; }

        public class WelcomeTour
        {
            public string Title { get; set; }

            public string Text { get; set; }
        }
    }
}
