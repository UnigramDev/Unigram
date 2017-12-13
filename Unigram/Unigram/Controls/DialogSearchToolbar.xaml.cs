using LinqToVisualTree;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Unigram.ViewModels.Dialogs;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public sealed partial class DialogSearchToolBar : UserControl
    {
        public DialogSearchViewModel ViewModel => DataContext as DialogSearchViewModel;

        public DialogSearchToolBar()
        {
            this.InitializeComponent();

            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        private async void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Visibility == Visibility.Visible)
            {
                await Task.Delay(100);
                CaptionInput.Focus(FocusState.Keyboard);
            }
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLUser user)
            {
                CaptionInput.IsFiltering = false;
                CaptionInput.Text = string.Empty;
                //CaptionInput.PlaceholderText = string.Empty;

                ViewModel.From = user;
                ViewModel.Autocomplete = null;
            }
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            AutocompleteHeader.Margin = new Thickness(0, -height, 0, padding);
            AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");

            var scrollingHost = ListAutocomplete.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                scrollingHost.ChangeView(null, scrollingHost.ScrollableHeight, null, true);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Dialog.Search = null;
        }
    }
}
