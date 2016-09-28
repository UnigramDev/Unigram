using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Notifications;
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

        public delegate void GoToDialogEventHandler(TLDialog dialog);
        public event GoToDialogEventHandler GoToDialogTapped;

        TLDialog currentDialog = null;
        bool cancelToast;
        bool pointerOnTopOfToast = false;

        private void OnGoToDialogTapped(TLDialog dialog)
        {
            if (GoToDialogTapped != null)
                GoToDialogTapped(dialog);
        }


        public ForwardDialog()
        {
            this.InitializeComponent();

            FContactsList.ItemsSource = null;
        }


        private async void ForwardCancel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            cancelToast = false;

            await PlayCancelAnimation();

            if (FContactsList.Items.Count > 0)
                FContactsList.ScrollIntoView(FContactsList.Items[0]);

            ExitDialog();
        }

        private void ExitDialog()
        {
            if (cancelToast)
                return;

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
            double defaultHeight = ForwardMenuBox.Height;
            cancelToast = false;

            VerticalAlignment defaultVa = ForwardMenuBox.VerticalAlignment;
            ForwardMenuBox.VerticalAlignment = VerticalAlignment.Top;

            ForwardMenuBox.Tag = new Tuple<double, VerticalAlignment>(defaultHeight, defaultVa);

            ForwardMenuBoxTransform.Y = (Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds.Height - ForwardMenuBox.Height) * (defaultVa == VerticalAlignment.Bottom ? 1 : 0.5);
             
            Notif.Visibility = Visibility.Visible;
            ForwardMenuSentStoryboard.Begin();

            await Task.Delay(400);

            ForwardMenuOverlay.Visibility = Visibility.Collapsed;

            if (FContactsList.Items.Count > 0)
                FContactsList.ScrollIntoView(FContactsList.Items[0]);

            pointerOnTopOfToast = false;

            //Avoid closing the NEXT toast here. (close this function before second comes)
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(400);
                if (pointerOnTopOfToast)
                    i = 5;
                if (cancelToast)
                    return;
            }
            

            ForwardMenuSent2Storybard.Begin();

            await Task.Delay(500);
            if (cancelToast)
                return;

            ForwardMenuBox.Tag = null;
            Notif.Visibility = Visibility.Collapsed;
            ForwardMenuBox.Height = defaultHeight;
            ForwardMenuBox.VerticalAlignment = defaultVa;
            ForwardMenuOverlay.Visibility = Visibility.Visible;
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
            DoSearch();
        }

        private void DoSearch()
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
            ForwardButton.IsEnabled = false;
            forwardingProgressBar.Visibility = Visibility.Visible;
            forwardingProgressBar.IsIndeterminate = true;

            currentDialog = (FContactsList.SelectedItem as TLDialog);

            recieverName.Text = (FContactsList.SelectedItem as TLDialog).FullName;

            await ViewModel.Forward((FContactsList.SelectedItem as TLDialog).Peer, ForwardMessage.Text.Trim());

            await PlaySendAnimation();
            ExitDialog();
        }

        private void ForwardMessage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ForwardButton.Focus(FocusState.Programmatic);
                ForwardButton_Tapped(this, new TappedRoutedEventArgs());
                e.Handled = true; // Fix a bug causing this event to fire twice.
            }
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

        private void fDialog_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ForwardMenuBox.MaxHeight = e.NewSize.Height;
        }

        public async void InitDialog()
        {
            cancelToast = true;

            var data = ForwardMenuBox.Tag as Tuple<double, VerticalAlignment>;

            if (data != null)
            {
                ForwardMenuBox.Tag = null;
                Notif.Visibility = Visibility.Collapsed;
                ForwardMenuBox.Height = data.Item1;
                ForwardMenuBox.VerticalAlignment = data.Item2;
                ForwardMenuOverlay.Visibility = Visibility.Visible;
            }


            FContactsList.SelectedItem = null;
            ForwardHeader.Visibility = Visibility.Visible;
            ForwardSearchBox.Visibility = Visibility.Collapsed;
            ForwardMessage.Text = "";

            currentDialog = null;

            ForwardButton.IsEnabled = true;
            forwardingProgressBar.Visibility = Visibility.Collapsed;
            forwardingProgressBar.IsIndeterminate = false;

            ForwardSearchBox.Text = "";
            DoSearch();

            ForwardMenuShowStoryboard.Begin();

            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Showing += ForwardDialog_InputShowing;
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().Hiding += ForwardDialog_InputHiding;

            if (FContactsList.ItemsSource == null)
            {
                await Task.Delay(200);
                FContactsList.ItemsSource = ViewModel.FSearchDialogs;
            }
        }

        private void NotifInner_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OnGoToDialogTapped(currentDialog);
        }
        
        private void NotifInner_PointerLeft(object sender, PointerRoutedEventArgs e)
        {
            pointerOnTopOfToast = false;
        }

        private void NotifInner_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointerOnTopOfToast = true;
        }
    }
}
