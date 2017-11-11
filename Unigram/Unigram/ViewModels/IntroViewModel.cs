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
using Unigram.Views.SignIn;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Resources;
using Unigram.Strings;

namespace Unigram.ViewModels
{
    public class IntroViewModel : UnigramViewModelBase
    {
        
        public IntroViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            ContinueCommand = new RelayCommand(ContinueExecute /*, () => SelectedItem == Items.Last()*/);

            Items = new ObservableCollection<IntroPage>();
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage1_Title, Text = Strings.Resources.IntroWizardPage1_Text });
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage2_Title, Text = Strings.Resources.IntroWizardPage2_Text });
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage3_Title, Text = Strings.Resources.IntroWizardPage3_Text });
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage4_Title, Text = Strings.Resources.IntroWizardPage4_Text });
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage5_Title, Text = Strings.Resources.IntroWizardPage5_Text });
            //Items.Add(new IntroPage { Title = Strings.Resources.IntroWizardPage6_Title, Text = Strings.Resources.IntroWizardPage6_Text });
            //SelectedItem = Items[0];
        }

        public RelayCommand ContinueCommand { get; }
        private void ContinueExecute()
        {
            NavigationService.Navigate(typeof(SignInPage));
        }

        private IntroPage _selectedItem;
        public IntroPage SelectedItem
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

        public ObservableCollection<IntroPage> Items { get; private set; }

        public class IntroPage
        {
            public string Title { get; set; }

            public string Text { get; set; }
        }
    }
}
