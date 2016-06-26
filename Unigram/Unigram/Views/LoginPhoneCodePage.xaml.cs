using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPhoneCodePage : Page
    {
        public LoginPhoneCodeViewModel ViewModel => DataContext as LoginPhoneCodeViewModel;

        public LoginPhoneCodePage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Instance.ResolverType<LoginPhoneCodeViewModel>();
            txtMasterCodeInputInput.TextChanged += OnTextChanged;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtMasterCodeInputInput.Text.Length == 5)
            {
                //var vm = (LoginPhoneCodeViewModel)DataContext;
                ViewModel.PhoneCode = txtMasterCodeInputInput.Text;
                ViewModel.SendCommand.Execute(sender);
            }
        }
    }
}
