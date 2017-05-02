using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Payments;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Hosting;
using Unigram.Common;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep1Page : Page
    {
        public PaymentFormStep1ViewModel ViewModel => DataContext as PaymentFormStep1ViewModel;

        public PaymentFormStep1Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentFormStep1ViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "REQ_INFO_NAME_INVALID":
                    VisualUtilities.ShakeView(FieldName);
                    break;
                case "REQ_INFO_PHONE_INVALID":
                    VisualUtilities.ShakeView(FieldPhone);
                    break;
                case "REQ_INFO_EMAIL_INVALID":
                    VisualUtilities.ShakeView(FieldEmail);
                    break;
                case "ADDRESS_COUNTRY_INVALID":
                    VisualUtilities.ShakeView(FieldCountry);
                    break;
                case "ADDRESS_CITY_INVALID":
                    VisualUtilities.ShakeView(FieldCity);
                    break;
                case "ADDRESS_POSTCODE_INVALID":
                    VisualUtilities.ShakeView(FieldPostcode);
                    break;
                case "ADDRESS_STATE_INVALID":
                    VisualUtilities.ShakeView(FieldState);
                    break;
                case "ADDRESS_STREET_LINE1_INVALID":
                    VisualUtilities.ShakeView(FieldStreet1);
                    break;
                case "ADDRESS_STREET_LINE2_INVALID":
                    VisualUtilities.ShakeView(FieldStreet2);
                    break;
            }
        }
    }
}
