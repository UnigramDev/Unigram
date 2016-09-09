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

    public sealed partial class DialogPage : Page
    {
        public DialogPageViewModel ViewModel => DataContext as DialogPageViewModel;
        public DialogPage()
        {
            DataContext = UnigramContainer.Instance.ResolverType<DialogPageViewModel>();
            
            this.InitializeComponent();
        }

        private void txtMessage_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            if (txtMessage.Text == "" || txtMessage.Text == null)
            {
                btnSendMessage.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnVoiceMessage.Visibility = Visibility.Collapsed;
                btnSendMessage.Visibility = Visibility.Visible;
            }

            // TODO Save text to draft if not being send

        }

        private void btnVoiceMessage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SendTextHolder = txtMessage.Text;
        }

        private void btnDialogInfo_Click(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.user;
            ViewModel.NavigationService.Navigate(typeof(UserInfoPage), user);
        }
    }
}
