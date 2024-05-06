//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public class CreatePollViewModel : ViewModelBase
    {
        public CreatePollViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator) : base(clientService, settingsService, aggregator)
        {
        }
    }

    public sealed partial class CreatePollPopup : ContentPopup
    {
        private const int MAXIMUM_OPTIONS = 10;

        private readonly CreatePollViewModel _viewModel;

        public CreatePollPopup(IClientService clientService, bool forceQuiz, bool forceRegular, bool forceAnonymous)
        {
            InitializeComponent();

            _viewModel = new CreatePollViewModel(clientService,
                TypeResolver.Current.Resolve<ISettingsService>(clientService.SessionId),
                TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId));

            QuestionText.DataContext = _viewModel;
            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.SessionId);

            Title = Strings.NewPoll;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            Items = new ObservableCollection<PollOptionViewModel>();
            Items.CollectionChanged += Items_CollectionChanged;
            Items.Add(new PollOptionViewModel(string.Empty, forceQuiz, false, option => Remove_Click(option)));

            if (forceQuiz)
            {
                Quiz.IsChecked = true;
                Quiz.Visibility = Visibility.Collapsed;
                Multiple.Visibility = Visibility.Collapsed;
                Settings.Footer = string.Empty;
            }
            else if (forceRegular)
            {
                Quiz.IsChecked = false;
                Quiz.Visibility = Visibility.Collapsed;
                Settings.Footer = string.Empty;
            }

            if (forceAnonymous)
            {
                Anonymous.IsChecked = true;
                Anonymous.Visibility = Visibility.Collapsed;
            }
            else
            {
                Anonymous.IsChecked = true;
                Anonymous.Visibility = Visibility.Visible;
            }
        }

        public FormattedText Question
        {
            get
            {
                return QuestionText.GetFormattedText();
            }
        }

        public IList<FormattedText> Options
        {
            get
            {
                return Items.Where(x => !string.IsNullOrWhiteSpace(x.Text.Text)).Select(x => x.Text).ToList();
            }
        }

        public bool IsAnonymous
        {
            get
            {
                return Anonymous.IsChecked == true;
            }
        }

        public PollType Type
        {
            get
            {
                if (Quiz.IsChecked == true)
                {
                    return new PollTypeQuiz(Items.IndexOf(Items.FirstOrDefault(x => x.IsChecked)), QuizExplanation.GetFormattedText());
                }

                return new PollTypeRegular(Multiple.IsChecked == true);
            }
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (MAXIMUM_OPTIONS - Items.Count - 1 <= 0)
            {
                AddAnOption.Visibility = Visibility.Collapsed;
                AddInfo.Text = Strings.AddAnOptionInfoMax;
            }
            else
            {
                AddAnOption.Visibility = Visibility.Visible;
                AddInfo.Text = string.Format(Strings.AddAnOptionInfo,
                    Locale.Declension(Strings.R.Option, MAXIMUM_OPTIONS - Items.Count - 1));
            }

            UpdatePrimaryButton();
        }

        private void UpdatePrimaryButton()
        {
            var condition = !QuestionText.IsEmpty;
            condition = condition && Items.Count(x => !string.IsNullOrEmpty(x.Text.Text)) >= 2;

            if (Quiz.IsChecked == true)
            {
                condition = condition && Items.Count(x => x.IsChecked) == 1;
            }

            IsPrimaryButtonEnabled = condition;
            IsLightDismissEnabled = condition;
        }

        public ObservableCollection<PollOptionViewModel> Items { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void AddAnOption_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Items.Count < MAXIMUM_OPTIONS && !string.IsNullOrEmpty(AddAnOption.Text))
            {
                Items.Add(new PollOptionViewModel(AddAnOption.Text, Quiz.IsChecked == true, true, option => Remove_Click(option)));
            }

            AddAnOption.IsReadOnly = true;
            AddAnOption.Text = string.Empty;
        }

        private void Remove_Click(PollOptionViewModel option)
        {
            Items.Remove(option);
            Focus(Items.Count - 1);
        }

        private void Question_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdatePrimaryButton();
        }

        private void Option_TextChanged(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox textBox && textBox.Tag is PollOptionViewModel option)
            {
                option.Text = textBox.GetFormattedText();
            }

            UpdatePrimaryButton();
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check && check.DataContext is PollOptionViewModel option)
            {
                foreach (var item in Items)
                {
                    item.IsChecked = item == option;
                }
            }

            UpdatePrimaryButton();
        }

        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdatePrimaryButton();
        }

        private void Option_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox text && text.DataContext is PollOptionViewModel option)
            {
                text.Tag = option;
                text.DataContext = _viewModel;
                text.CustomEmoji = text.Parent.GetChild<CustomEmojiCanvas>();

                text.SetText(option.Text);

                if (option.FocusOnLoaded)
                {
                    text.Document.Selection.SetRange(int.MaxValue, int.MaxValue);
                    text.Focus(FocusState.Keyboard);
                }

                option.FocusOnLoaded = false;
            }
        }

        private void Option_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not FormattedTextBox text || text.Tag is not PollOptionViewModel option)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Back && text.IsEmpty)
            {
                e.Handled = true;

                var index = Items.IndexOf(option);
                if (index > 0)
                {
                    Focus(index - 1);
                }
                else if (index < Items.Count - 1)
                {
                    Focus(1);
                }
                else
                {
                    AddAnOption.Focus(FocusState.Keyboard);
                }

                Items.Remove(option);
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;

                var index = Items.IndexOf(option);
                if (index < Items.Count - 1)
                {
                    Focus(index + 1);
                }
                else
                {
                    AddAnOption.Focus(FocusState.Keyboard);
                }
            }
        }

        private void Question_GotFocus(object sender, RoutedEventArgs e)
        {
            AddAnOption.IsReadOnly = false;

            if (sender is FormattedTextBox textBox)
            {
                OnVisibleChanged(textBox.Parent.GetChild<GlyphButton>(), true);
            }
        }

        private void Question_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox textBox)
            {
                OnVisibleChanged(textBox.Parent.GetChild<GlyphButton>(), false);
            }
        }

        private static void OnVisibleChanged(DependencyObject d, bool value)
        {
            var sender = d as UIElement;
            var newValue = value;
            var oldValue = !value;

            if (newValue == oldValue || (sender.Visibility == Visibility.Collapsed && !newValue))
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(sender);

            visual.CenterPoint = new Vector3(16, 12, 0);
            sender.Visibility = Visibility.Visible;

            var batch = Window.Current.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Opacity = newValue ? 1 : 0;
                visual.Scale = new Vector3(true ? newValue ? 1 : 0 : 1);

                sender.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
            };

            var anim1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(0, newValue ? 0 : 1);
            anim1.InsertKeyFrame(1, newValue ? 1 : 0);
            visual.StartAnimation("Opacity", anim1);

            var anim2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim2.InsertKeyFrame(0, new Vector3(newValue ? 0 : 1));
            anim2.InsertKeyFrame(1, new Vector3(newValue ? 1 : 0));
            visual.StartAnimation("Scale", anim2);

            batch.End();
        }

        private void Focus(int option)
        {
            var container = Presenter.ContainerFromIndex(option) as ContentPresenter;
            if (container == null)
            {
                return;
            }

            var inner = container.GetChild<FormattedTextBox>();
            if (inner == null)
            {
                return;
            }

            inner.Focus(FocusState.Keyboard);
        }

        private void Multiple_Toggled(object sender, RoutedEventArgs e)
        {
            if (Multiple.IsChecked == true)
            {
                Quiz.IsChecked = false;
            }
        }

        private void Quiz_Toggled(object sender, RoutedEventArgs e)
        {
            QuizSettings.Visibility = Quiz.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (Quiz.IsChecked == true)
            {
                Multiple.IsChecked = false;
            }

            foreach (var item in Items)
            {
                item.IsChecked = false;
                item.IsQuiz = Quiz.IsChecked == true;
            }

            AddAnOption.Margin = new Thickness(Quiz.IsChecked == true ? 28 + 24 : 24, 8, 24, 0);

            UpdatePrimaryButton();
        }

        private FormattedTextBox _target;

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            var element = FocusManager.GetFocusedElement();
            if (element is not FormattedTextBox textBox)
            {
                return;
            }

            _target = textBox;

            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(textBox, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                _target?.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                _target?.InsertEmoji(sticker);
            }

            _target?.Focus(FocusState.Programmatic);
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _target = null;
        }
    }

    public class PollOptionViewModel : BindableBase
    {
        private readonly Action<PollOptionViewModel> _remove;

        public PollOptionViewModel(string text, bool quiz, bool focus, Action<PollOptionViewModel> remove)
        {
            _text = new FormattedText(text, Array.Empty<TextEntity>());
            _isQuiz = quiz;
            _focusOnLoaded = focus;
            _remove = remove;
        }

        private FormattedText _text;
        public FormattedText Text
        {
            get => _text;
            set => Set(ref _text, value);
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }

        private bool _isQuiz;
        public bool IsQuiz
        {
            get => _isQuiz;
            set => Set(ref _isQuiz, value);
        }

        private bool _focusOnLoaded;
        public bool FocusOnLoaded
        {
            get => _focusOnLoaded;
            set => Set(ref _focusOnLoaded, value);
        }

        public void Remove()
        {
            _remove(this);
        }
    }
}
