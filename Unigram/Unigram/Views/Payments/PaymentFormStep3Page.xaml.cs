using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Payments;
using Unigram.Webview;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Payments
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PaymentFormStep3Page : Page
    {
        public PaymentFormStep3ViewModel ViewModel => DataContext as PaymentFormStep3ViewModel;

        public PaymentFormStep3Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentFormStep3ViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Navigate"))
            {
                View.Navigate(new Uri(ViewModel.PaymentForm.Url));
            }
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramWebviewProxy((title, credentials) =>
            {
                ViewModel.NavigateToNextStep(title, credentials);
            }));
        }
    }
}
