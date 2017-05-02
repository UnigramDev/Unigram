using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
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

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep4Page : Page
    {
        public PaymentFormStep4ViewModel ViewModel => DataContext as PaymentFormStep4ViewModel;

        public PaymentFormStep4Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentFormStep4ViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "PASSWORD_HASH_INVALID":
                    VisualUtilities.ShakeView(FieldPassword);
                    break;
            }
        }
    }
}
