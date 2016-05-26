namespace Unigram.Client.Views
{
    using Core.Dependency;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Unigram.ViewModel;
    using Windows.Foundation;
    using Windows.Foundation.Collections;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Navigation;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPhoneNumber : Page
    {
        private LoginPhoneNumberViewModel viewModel;

        public LoginPhoneNumber()
        {
            this.InitializeComponent();

            viewModel = UnigramContainer.Instance.ResolverType<LoginPhoneNumberViewModel>();
        }

        private void btnMasterPhoneInputConfirm_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(Views.LoginPhoneCode));
        }
    }
}
