using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class TagsTextBox : ListView
    {
        private TagsTextBoxFooter InputPlaceholder;
        private TagsWrapPanel ItemsWrapPanel;

        public TagsTextBox()
        {
            DefaultStyleKey = typeof(TagsTextBox);
        }

        #region Separator
        public char Separator
        {
            get { return (char)GetValue(SeparatorProperty); }
            set { SetValue(SeparatorProperty, value); }
        }

        public static readonly DependencyProperty SeparatorProperty =
            DependencyProperty.Register("Separator", typeof(char), typeof(TagsTextBox), new PropertyMetadata(';'));
        #endregion

        #region PlaceholderText

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(TagsTextBox), new PropertyMetadata(string.Empty));

        #endregion

        #region CanShowPlaceholder

        internal bool CanShowPlaceholder
        {
            get { return (bool)GetValue(CanShowPlaceholderProperty); }
            set { SetValue(CanShowPlaceholderProperty, value); }
        }

        public static readonly DependencyProperty CanShowPlaceholderProperty =
            DependencyProperty.Register("CanShowPlaceholder", typeof(bool), typeof(TagsTextBox), new PropertyMetadata(true));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(TagsTextBox), new PropertyMetadata(null));

        #endregion

        internal void Initialize(TagsTextBoxFooter inline)
        {
            InputPlaceholder = inline;
            ItemsWrapPanel = (TagsWrapPanel)ItemsPanelRoot;

            InputPlaceholder.KeyDown += OnKeyDown;
            ItemsWrapPanel.LayoutUpdated += OnWrapLayoutUpdated;

            var bindPlaceholder = new Binding();
            bindPlaceholder.Path = new PropertyPath("PlaceholderText");
            bindPlaceholder.Source = this;

            var bindCanShow = new Binding();
            bindCanShow.Path = new PropertyPath("CanShowPlaceholder");
            bindCanShow.Source = this;

            var bindText = new Binding();
            bindText.Path = new PropertyPath("Text");
            bindText.Source = InputPlaceholder;
            bindText.Mode = BindingMode.TwoWay;

            InputPlaceholder.SetBinding(TextBox.PlaceholderTextProperty, bindPlaceholder);
            InputPlaceholder.SetBinding(TagsTextBoxFooter.CanShowPlaceholderProperty, bindCanShow);

            SetBinding(TextProperty, bindText);
        }

        private void OnWrapLayoutUpdated(object sender, object e)
        {
            CanShowPlaceholder = ItemsWrapPanel.Children.Count <= 1;
        }

        private void OnKeyboardUp(object sender, KeyRoutedEventArgs e)
        {
            //if (e.Key != VirtualKey.Back)
            //{
            //    var text = (TextBox)sender;
            //    InputPlaceholder.Text = text.Text;
            //    InputPlaceholder.SelectionStart = InputPlaceholder.Text.Length;
            //    InputPlaceholder.Focus(FocusState.Keyboard);
            //    text.Text = string.Empty;
            //}
        }

        private void OnKeyboardDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back || e.Key == VirtualKey.Delete)
            {
                var text = sender as TagsTextBoxItemPresenter;
                if (text != null)
                {
                    InputPlaceholder.Focus(FocusState.Keyboard);

                    var source = ItemsSource as IList;
                    if (source != null)
                    {
                        source.Remove(text.Tag);
                    }

                    //ItemsWrapPanel.Children.Remove(text);
                    //Tags.Remove(text.RealWord);

                    e.Handled = true;
                }
            }
            else
            {
                var text = (TextBox)sender;
                InputPlaceholder.Focus(FocusState.Keyboard);
                InputPlaceholder.OnKeyDownOverride(e);
                text.Text = string.Empty;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            var single = new TagsTextBoxItem();
            single.KeyDownDelegate = OnKeyboardDown;
            single.KeyUpDelegate = OnKeyboardUp;

            return single;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var single = element as TagsTextBoxItem;
            var user = item as TLUser;
            if (user != null)
            {
                single.PlaceholderText = user.FullName + Separator;
                single.Content = user;
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back && InputPlaceholder.Text.Length == 0)
            {
                if (ItemsWrapPanel.Children.Count > 1)
                {
                    var container = ContainerFromIndex(Items.Count - 1) as TagsTextBoxItem;
                    if (container != null)
                    {
                        container.Focus(FocusState.Keyboard);
                    }

                    var text = ItemsWrapPanel.Children[ItemsWrapPanel.Children.Count - 2] as TagsTextBoxItemPresenter;
                    if (text != null)
                    {
                        text.Focus(FocusState.Keyboard);
                    }
                }
            }
            else if (e.Key == VirtualKey.Left && InputPlaceholder.SelectionStart == 0 && InputPlaceholder.SelectionLength == 0)
            {
                if (ItemsWrapPanel.Children.Count > 1)
                {
                    var text = ItemsWrapPanel.Children[ItemsWrapPanel.Children.Count - 2] as TagsTextBoxItemPresenter;
                    if (text != null)
                    {
                        text.Focus(FocusState.Keyboard);
                    }
                }
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            // Commented to let back to focus tags
            //base.OnGotFocus(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            // Commented to let back to focus tags
            //base.OnLostFocus(e);
        }

        //protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        //{
        //    base.OnSelectionChanged(e);

        //    if (SelectedItem != null)
        //    {
        //        var text = new SingleTagTextBox();
        //        text.Style = (Style)App.Current.Resources["ReadonlyTextBoxStyle"];
        //        text.RealWord = InputPlaceholder.Text;
        //        text.Tag = InputPlaceholder.Text + Separator;
        //        text.SelectionStart = 0;
        //        text.KeyDown += OnKeyboardDown;
        //        text.KeyUp += OnKeyboardUp;

        //        ItemsWrapPanel.Children.Insert(ItemsWrapPanel.Children.Count - 1, text);
        //        InputPlaceholder.Text = string.Empty;
        //    }
        //}
    }

    public class TagsTextBoxFooter : TextBox
    {
        private ContentControl PlaceholderTextElement;

        public TagsTextBoxFooter()
        {
            DefaultStyleKey = typeof(TagsTextBoxFooter);
            TextChanged += OnTextChanged;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            PlaceholderTextElement = (ContentControl)GetTemplateChild("PlaceholderTextElement");
            DeterminePlaceholderElementVisibility();
        }

        public void OnKeyDownOverride(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            DeterminePlaceholderElementVisibility();
        }

        private void DeterminePlaceholderElementVisibility()
        {
            if (string.IsNullOrEmpty(this.Text) && CanShowPlaceholder)
            {
                PlaceholderTextElement.Visibility = Visibility.Visible;
            }
            else
            {
                PlaceholderTextElement.Visibility = Visibility.Collapsed;
            }
        }

        public bool CanShowPlaceholder
        {
            get { return (bool)GetValue(CanShowPlaceholderProperty); }
            set { SetValue(CanShowPlaceholderProperty, value); }
        }

        public static readonly DependencyProperty CanShowPlaceholderProperty =
            DependencyProperty.Register("CanShowPlaceholder", typeof(bool), typeof(TagsTextBoxFooter), new PropertyMetadata(true, OnCanShowPlaceholderChanged));

        private static void OnCanShowPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (TagsTextBoxFooter)d;
            if (sender.PlaceholderTextElement != null)
            {
                sender.DeterminePlaceholderElementVisibility();
            }
        }


    }

    public class TagsTextBoxItemPresenter : TextBox
    {
        public TagsTextBoxItemPresenter()
        {
            DefaultStyleKey = typeof(TagsTextBoxItemPresenter);
        }
    }

    public class TagsTextBoxItem : ListViewItem
    {
        public Action<object, KeyRoutedEventArgs> KeyDownDelegate { get; internal set; }
        public Action<object, KeyRoutedEventArgs> KeyUpDelegate { get; internal set; }

        private TagsTextBoxItemPresenter Presenter;

        public TagsTextBoxItem()
        {
            DefaultStyleKey = typeof(TagsTextBoxItem);
        }

        protected override void OnApplyTemplate()
        {
            Presenter = (TagsTextBoxItemPresenter)GetTemplateChild("Presenter");
            Presenter.KeyDown += new KeyEventHandler(KeyDownDelegate);
            Presenter.KeyUp += new KeyEventHandler(KeyUpDelegate);
        }

        #region PlaceholderText

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }


        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(TagsTextBoxItem), new PropertyMetadata(string.Empty));

        #endregion

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            Presenter?.Focus(FocusState.Keyboard);
        }
    }
}
