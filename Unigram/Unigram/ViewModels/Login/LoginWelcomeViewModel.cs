using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Views.Login;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Login
{
    public class LoginWelcomeViewModel : UnigramViewModelBase
    {
        public LoginWelcomeViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<WelcomeTour>();

            // TODO: put them in a separate file?
            // TODO: localization
            Items.Add(new WelcomeTour { Title = "Unigram", Text = "Unigram is a Telegram Universal app built by the Windows Community, for the Windows Community" });
            Items.Add(new WelcomeTour { Title = "Fast", Text = "**Telegram** delivers messages faster\nthan any other application." });
            Items.Add(new WelcomeTour { Title = "Free", Text = "**Telegram** is free forever. No ads.\nNo subscription fees." });
            Items.Add(new WelcomeTour { Title = "Powerful", Text = "**Telegram** has no limits on\nthe size of your media and chats." });
            Items.Add(new WelcomeTour { Title = "Secure", Text = "**Telegram** keeps your messages\nsafe from hacker attacks." });
            Items.Add(new WelcomeTour { Title = "Cloud-Based", Text = "**Telegram** lets you access your\nmessages from multiple devices." });
            SelectedItem = Items[0];
        }

        private RelayCommand _continueCommand;
        public RelayCommand ContinueCommand => _continueCommand = (_continueCommand ?? new RelayCommand(ContinueExecute, () => SelectedItem == Items.Last()));
        private void ContinueExecute()
        {
            NavigationService.Navigate(typeof(LoginSignInPage));
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
