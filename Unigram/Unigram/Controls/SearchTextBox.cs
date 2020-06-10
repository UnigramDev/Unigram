using Unigram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class SearchTextBox : TextBox
    {
        public ViewModelBase ViewModel => DataContext as ViewModelBase;

        public SearchTextBox()
        {
            DefaultStyleKey = typeof(SearchTextBox);
        }

        public event RoutedEventHandler Delete;

        protected override void OnApplyTemplate()
        {
            var clean = GetTemplateChild("DeleteButton") as Button;
            if (clean != null)
            {
                clean.Click += Clean_Click;
            }

            base.OnApplyTemplate();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Text))
            {
                Delete?.Invoke(this, e);
            }
            else if (FocusState == FocusState.Unfocused)
            {
                Text = string.Empty;
            }
        }
    }
}
