using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class SendPhotosView : ContentDialogBase
    {
        public SendPhotosView()
        {
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var transform = TransformToVisual((FrameworkElement)Window.Current.Content);
            var point = transform.TransformPoint(new Point());

            var bottom = point.Y + ActualHeight;
            var difference = ((FrameworkElement)Window.Current.Content).ActualHeight - bottom;

            KeyboardPlaceholder.Height = new GridLength(args.OccludedRect.Height - difference);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                Flip.Focus(FocusState.Programmatic);
            }
        }
    }
}
