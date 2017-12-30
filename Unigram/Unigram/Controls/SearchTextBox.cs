using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Template10.Mvvm;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class SearchTextBox : TextBox
    {
        public ViewModelBase ViewModel => DataContext as ViewModelBase;

        public SearchTextBox()
        {
            DefaultStyleKey = typeof(SearchTextBox);

            //DataContextChanged += OnDataContextChanged;
            TextChanged += OnTextChanged;
        }

        public event RoutedEventHandler Clean;

        protected override void OnApplyTemplate()
        {
            var clean = GetTemplateChild("CleanButton") as Button;
            if (clean != null)
            {
                clean.Click += Clean_Click;
                clean.Click += Clean;
            }

            var search = GetTemplateChild("SearchButton") as Button;
            if (search != null)
            {
                search.Click += Search_Click;
            }

            base.OnApplyTemplate();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            Text = string.Empty;
            Focus(FocusState.Keyboard);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Keyboard);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null && ViewModel.NavigationService != null)
            {
                ViewModel.NavigationService.FrameFacade.BackRequested += OnBackRequested;
            }
        }

        private void OnBackRequested(object sender, Template10.Common.HandledEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Text) == false)
            {
                e.Handled = true;

                Text = string.Empty;
                Focus(FocusState.Keyboard);
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            GetBindingExpression(TextProperty)?.UpdateSource();

            if (string.IsNullOrWhiteSpace(Text))
            {
                VisualStateManager.GoToState(this, "ButtonCollapsed1", false);
                SearchCommand?.Execute(null);
            }
            else
            {
                VisualStateManager.GoToState(this, "ButtonVisible1", false);
            }
        }

        //protected override void OnKeyDown(KeyRoutedEventArgs e)
        //{
        //    if (e.Key == Windows.System.VirtualKey.Enter)
        //    {
        //        GetBindingExpression(TextProperty)?.UpdateSource();
        //        SearchCommand?.Execute(null);
        //    }

        //    base.OnKeyDown(e);
        //}

        #region SearchCommand
        public ICommand SearchCommand
        {
            get { return (ICommand)GetValue(SearchCommandProperty); }
            set { SetValue(SearchCommandProperty, value); }
        }

        public static readonly DependencyProperty SearchCommandProperty =
            DependencyProperty.Register("SearchCommand", typeof(ICommand), typeof(SearchTextBox), new PropertyMetadata(null));
        #endregion
    }
}
