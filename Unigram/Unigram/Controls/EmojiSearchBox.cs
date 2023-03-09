using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public enum EmojiSearchType
    {
        Default,
        ChatPhoto,
        EmojiStatus
    }

    public class EmojiCategorySelectedEventArgs : EventArgs
    {
        public EmojiCategory Category { get; set; }

        public EmojiCategorySelectedEventArgs(EmojiCategory category)
        {
            Category = category;
        }
    }

    public class EmojiSearchBox : Control
    {
        private TextBox TextField;

        private ScrollViewer ScrollingHost;
        private StackPanel Presenter;

        private ToggleButton SearchButton;

        private IClientService _clientService;
        private EmojiSearchType _type;

        public EmojiSearchBox()
        {
            DefaultStyleKey = typeof(EmojiSearchBox);
        }

        public event EventHandler<EmojiCategorySelectedEventArgs> CategorySelected;

        protected override void OnApplyTemplate()
        {
            TextField = GetTemplateChild(nameof(TextField)) as TextBox;
            TextField.TextChanged += OnTextChanged;
            TextField.Text = Text;

            SearchButton = GetTemplateChild(nameof(SearchButton)) as ToggleButton;
            SearchButton.Click += SearchButton_Click;

            ScrollingHost = GetTemplateChild(nameof(ScrollingHost)) as ScrollViewer;
            ScrollingHost.ViewChanging += OnViewChanging;
            //ScrollingHost.ContainerContentChanging += OnContainerContentChanging;
            //ScrollingHost.SelectionChanged += OnSelectionChanged;

            Presenter = GetTemplateChild(nameof(Presenter)) as StackPanel;

            if (_clientService != null)
            {
                SetType(_clientService, _type);
            }

            ShowHidePlaceholder(_placeholderCollapsed);
            base.OnApplyTemplate();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextField.Text))
            {
                TextChanged?.Invoke(this, null);
            }
            else
            {
                TextField.Text = string.Empty;
            }

            ClearSelection();
            UpdateSearchButton();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            Text = TextField.Text;
            TextChanged?.Invoke(this, e);

            ClearSelection();
            UpdateSearchButton();
        }

        private void UpdateSearchButton()
        {
            foreach (RadioButton button in Presenter.Children)
            {
                if (button.IsChecked is true)
                {
                    SearchButton.IsChecked = true;
                    return;
                }
            }

            SearchButton.IsChecked = !string.IsNullOrEmpty(TextField.Text) /*|| ScrollingHost.SelectedItem != null*/;
        }

        private bool _placeholderCollapsed;

        private void ShowHidePlaceholder(bool show)
        {
            if (_placeholderCollapsed != show)
            {
                return;
            }

            if (TextField == null)
            {
                return;
            }

            var placeholder = TextField.GetChild<TextBlock>("PlaceholderTextContentPresenter");
            if (placeholder == null)
            {
                return;
            }

            var element = TextField.GetChild<ScrollViewer>("ContentElement");
            if (element == null)
            {
                return;
            }

            _placeholderCollapsed = !show;

            var visual1 = ElementCompositionPreview.GetElementVisual(placeholder);
            var visual2 = ElementCompositionPreview.GetElementVisual(element);
            var anim = visual1.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(show ? 0 : 1, 0);
            anim.InsertKeyFrame(show ? 1 : 0, 1);

            visual1.StartAnimation("Opacity", anim);
            visual2.StartAnimation("Opacity", anim);
        }

        public async void SetType(IClientService clientService, EmojiSearchType type)
        {
            _clientService = clientService;
            _type = type;

            if (ScrollingHost == null)
            {
                return;
            }

            EmojiCategoryType task = type switch
            {
                EmojiSearchType.ChatPhoto => new EmojiCategoryTypeChatPhoto(),
                EmojiSearchType.EmojiStatus => new EmojiCategoryTypeEmojiStatus(),
                _ => new EmojiCategoryTypeDefault()
            };

            var response = await clientService.SendAsync(new GetEmojiCategories(task));
            if (response is EmojiCategories categories)
            {
                var foreground = BootStrapper.Current.Resources["TextControlButtonForeground"] as SolidColorBrush;

                foreach (var item in categories.Categories)
                {
                    var view = new LottieView();
                    //view.AutoPlay = offset < visible;
                    view.IsLoopingEnabled = false;
                    view.FrameSize = new Size(20, 20);
                    view.DecodeFrameType = DecodePixelType.Logical;
                    view.Width = 20;
                    view.Height = 20;
                    view.TintColor = foreground.Color;
                    //view.Tag = offset < visible ? null : new object();

                    var file = item.Icon.StickerValue;
                    if (file.Local.IsDownloadingCompleted)
                    {
                        view.Source = UriEx.ToLocal(file.Local.Path);
                    }
                    else
                    {
                        view.Source = null;

                        UpdateManager.Subscribe(view, clientService, file, /*UpdateReaction*/UpdateFile, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            clientService.DownloadFile(file.Id, 32);
                        }
                    }

                    var button = new RadioButton
                    {
                        Tag = item,
                        Content = view,
                        Margin = new Thickness(0, 0, 10, 0),
                        Width = 24,
                        Height = 24,
                        MinWidth = 24,
                        MinHeight = 24,
                        Style = BootStrapper.Current.Resources["EmojiSearchButtonStyle"] as Style
                    };

                    //button.Click += Reaction_Click;
                    button.Checked += Category_Checked;

                    Presenter.Children.Add(button);

                    //if (offset < visible)
                    //{
                    //    var visual = ElementCompositionPreview.GetElementVisual(view);
                    //    visual.CenterPoint = new Vector3(12, 12, 0);
                    //    visual.Scale = Vector3.Zero;

                    //    var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                    //    scale.InsertKeyFrame(0, Vector3.Zero);
                    //    scale.InsertKeyFrame(1, Vector3.One);
                    //    scale.DelayTime = TimeSpan.FromMilliseconds(50 * (visible - Presenter.Children.Count));

                    //    visual.StartAnimation("Scale", scale);
                    //}

                    //offset++;
                }
            }
        }

        private void Category_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.Tag is EmojiCategory category)
            {
                CategorySelected?.Invoke(this, new EmojiCategorySelectedEventArgs(category));

                ClearText();
                UpdateSearchButton();
            }
        }

        private void ClearSelection()
        {
            if (Presenter == null)
            {
                return;
            }

            foreach (RadioButton button in Presenter.Children)
            {
                button.ClearValue(ToggleButton.IsCheckedProperty);
            }
        }

        private void ClearText()
        {
            if (TextField == null)
            {
                return;
            }

            TextField.TextChanged -= OnTextChanged;
            Text = string.Empty;
            TextField.TextChanged += OnTextChanged;
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void OnViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            ShowHidePlaceholder(e.FinalView.HorizontalOffset.AlmostEqualsToZero());

            //var j = (int)Math.Floor((e.NextView.HorizontalOffset - 8) / 34);
            //var k = (int)Math.Ceiling(((e.NextView.HorizontalOffset - 8) + Shadow.ActualWidth) / 34);

            //for (int i = 0; i < Presenter.Children.Count; i++)
            //{
            //    var button = Presenter.Children[i] as HyperlinkButton;
            //    if (button.Content is LottieView view)
            //    {
            //        if (view.Tag != null && i >= j && i < k)
            //        {
            //            view.Play();
            //            view.Tag = null;
            //        }
            //        else if (view.Tag == null && (i < j || i >= k))
            //        {
            //            view.Tag = new object();
            //        }
            //    }
            //}
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var delta = point.Properties.IsHorizontalMouseWheel
                ? point.Properties.MouseWheelDelta > 0 ? 50 : -50
                : point.Properties.MouseWheelDelta < 0 ? 50 : -50;

            ScrollingHost?.ChangeView(ScrollingHost.HorizontalOffset + delta, null, null);
            base.OnPointerWheelChanged(e);
        }

        public event TextChangedEventHandler TextChanged;

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(EmojiSearchBox), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EmojiSearchBox)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (TextField != null)
            {
                TextField.Text = newValue;
            }
        }

        #endregion

        #region PlaceholderText

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(EmojiSearchBox), new PropertyMetadata(string.Empty));

        #endregion
    }
}
