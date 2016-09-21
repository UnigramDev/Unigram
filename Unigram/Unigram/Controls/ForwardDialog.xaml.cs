using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class ForwardDialog : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ForwardDialog()
        {
            this.InitializeComponent();
        }


        private async void ForwardCancel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await PlayCancelAnimation();
            ExitDialog();
        }

        private void ExitDialog()
        {
            FContactsList.SelectedItem = null;

            ViewModel.CancelForward();

            ForwardHeader.Visibility = Visibility.Visible;
            ForwardSearchBox.Visibility = Visibility.Collapsed;
            ForwardMessage.Text = "";

            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Showing -= ForwardDialog_InputShowing;
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Hiding -= ForwardDialog_InputHiding;
        }


        private async Task PlaySendAnimation()
        {
            ForwardMenuHideStoryboard2.Begin();

            await Task.Delay(200);
        }

        private async Task PlayCancelAnimation()
        {
            ForwardMenuHideStoryboard.Begin();

            await Task.Delay(200);
        }

        private void ForwardSearchButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ForwardHeader.Visibility = Visibility.Collapsed;
            ForwardSearchBox.Visibility = Visibility.Visible;
            ForwardSearchBox.Focus(FocusState.Pointer);
        }

        private void ForwardSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ForwardSearchBox.Text != "")
            {
                if (FContactsList.ItemsSource != ViewModel.FSearchDialogs)
                {
                    FContactsList.ItemsSource = ViewModel.FSearchDialogs;
                }
                ViewModel.GetSearchDialogs(ForwardSearchBox.Text);
            }
            else
            {
                FContactsList.ItemsSource = ViewModel.FDialogs;
            }
        }

        private void FContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ForwardButton.IsEnabled = (FContactsList.SelectedItems.Count != 0);
        }

        private async void ForwardButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await ViewModel.Forward((FContactsList.SelectedItem as TLDialog).Peer, ForwardMessage.Text.Trim());

            await PlaySendAnimation();
            ExitDialog();
        }

        private void ForwardMessage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ForwardButton_Tapped(this, new TappedRoutedEventArgs());
                e.Handled = true; // Fix a bug causing this event to fire twice.
            }
        }

        private void fDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Showing += ForwardDialog_InputShowing;
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Hiding += ForwardDialog_InputHiding;
        }

        VerticalAlignment origVa = VerticalAlignment.Center;
        double origHeight = -1;
        private void ForwardDialog_InputHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs args)
        {
            if (origHeight == -1)
                return;
            ForwardMenuBox.VerticalAlignment = origVa;
            ForwardMenuBox.Height = origHeight;
            origHeight = -1;
        }

        private void ForwardDialog_InputShowing(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs args)
        {
            if (origHeight != -1)
                return;
            origVa = ForwardMenuBox.VerticalAlignment;
            ForwardMenuBox.VerticalAlignment = VerticalAlignment.Top;
            origHeight = ForwardMenuBox.Height;

            double maxHeight = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds.Height - args.OccludedRect.Height;
            if (ForwardMenuBox.Height > maxHeight)
            {
                ForwardMenuBox.Height = maxHeight;
            }
        }

        private void ForwardSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ForwardSearchBox.Text.Length == 0)
            {
                ForwardSearchBox.Visibility = Visibility.Collapsed;
                ForwardHeader.Visibility = Visibility.Visible;
            }
        }
    }
}
