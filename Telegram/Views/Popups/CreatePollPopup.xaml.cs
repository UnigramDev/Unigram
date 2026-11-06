//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Controls.Drawers;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public partial class CreatePollViewModel : ViewModelBase
    {
        public CreatePollViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator) : base(clientService, settingsService, aggregator)
        {
        }
    }

    public sealed partial class CreatePollPopup : ModalPopup
    {
        private readonly CreatePollViewModel _viewModel;

        private IList<CountryInfo> _countryCodes = [];
        private int _openPeriod = 24 * 60 * 60;

        public CreatePollPopup(IClientService clientService, INavigationService navigationService, FormattedText question, bool forceQuiz, bool forceRegular, bool channel)
        {
            InitializeComponent();

            _viewModel = new CreatePollViewModel(clientService,
                clientService.Session.Resolve<ISettingsService>(),
                clientService.Session.Resolve<IEventAggregator>());
            _viewModel.NavigationService = navigationService;

            QuestionText.DataContext = _viewModel;
            DescriptionText.DataContext = _viewModel;
            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            Title = Strings.NewPoll;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            Items = new ObservableCollection<PollOptionViewModel>();
            Items.CollectionChanged += Items_CollectionChanged;
            Items.Add(new PollOptionViewModel(string.Empty, forceQuiz, false, Remove_Click));

            // After Items: SetText raises ContentChanged at once, and its handler reads them.
            if (question != null)
            {
                QuestionText.SetText(question);
            }

            Duration.Badge = Locale.Declension(Strings.R.Hours, 24);

            if (forceQuiz)
            {
                Quiz.IsChecked = true;
                Quiz.Visibility = Visibility.Collapsed;
            }
            else if (forceRegular)
            {
                Quiz.IsChecked = false;
                Quiz.Visibility = Visibility.Collapsed;
            }

            if (channel)
            {
                IsAnonymous.IsChecked = false;
                IsAnonymous.Visibility = Visibility.Collapsed;
                AllowsAddingOptions.IsChecked = false;
                AllowsAddingOptions.Visibility = Visibility.Collapsed;
            }
            else
            {
                MembersOnly.IsChecked = false;
                MembersOnly.Visibility = Visibility.Collapsed;
                CountriesOnly.IsChecked = false;
                CountriesOnly.Visibility = Visibility.Collapsed;
            }
        }

        public InputMessagePoll Input
        {
            get
            {
                var question = QuestionText.GetFormattedText();
                MutableVector<InputPollOption> options = [];
                MutableVector<int> correct = [];

                foreach (var item in Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Text.Text))
                    {
                        continue;
                    }

                    // Indexes into the options that get sent, which leave out the blank ones.
                    if (item.IsChecked)
                    {
                        correct.Add(options.Count);
                    }

                    options.Add(new InputPollOption(item.Text, null));
                }

                var description = DescriptionText.GetFormattedText();
                var media = default(InputPollMedia);
                var isAnonymous = IsAnonymous.IsChecked == false;
                var allowsMultipleAnswers = AllowsMultipleAnswers.IsChecked == true;
                var allowsRevoting = AllowsRevoting.IsChecked == true;
                var membersOnly = MembersOnly.IsChecked == true;
                var shuffleOptions = ShuffleOptions.IsChecked == true;
                var hideResultsUntilCloses = LimitDuration.IsChecked == true && HideResults.IsChecked == true;
                var openPeriod = LimitDuration.IsChecked == true ? _openPeriod : 0;
                var closeDate = 0;

                // The selection stays when the toggle is turned off, and only counts while it's on.
                MutableVector<string> countryCodes = [];

                if (CountriesOnly.IsChecked == true)
                {
                    foreach (var country in _countryCodes)
                    {
                        countryCodes.Add(country.CountryCode);
                    }
                }

                InputPollType type;
                if (Quiz.IsChecked == true)
                {
                    type = new InputPollTypeQuiz(correct, QuizExplanation.GetFormattedText(), null);
                }
                else
                {
                    type = new InputPollTypeRegular(AllowsAddingOptions.IsChecked == true);
                }

                return new InputMessagePoll(question, options, description, media, isAnonymous, allowsMultipleAnswers, allowsRevoting, membersOnly, countryCodes, shuffleOptions, hideResultsUntilCloses, type, openPeriod, closeDate);
            }
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Items.Count >= _viewModel.ClientService.Options.PollAnswerCountMax)
            {
                AddAnOption.Visibility = Visibility.Collapsed;
                AddInfo.Text = Strings.AddAnOptionInfoMax;
            }
            else
            {
                AddAnOption.Visibility = Visibility.Visible;
                AddInfo.Text = string.Format(Strings.AddAnOptionInfo,
                    Locale.Declension(Strings.R.Option, _viewModel.ClientService.Options.PollAnswerCountMax - Items.Count));
            }

            UpdatePrimaryButton();
        }

        private void UpdatePrimaryButton()
        {
            var condition = !QuestionText.IsEmpty;
            condition = condition && Items.Count(x => !string.IsNullOrWhiteSpace(x.Text.Text)) >= 2;

            if (Quiz.IsChecked == true)
            {
                condition = condition && Items.Any(x => x.IsChecked && !string.IsNullOrWhiteSpace(x.Text.Text));
            }

            IsPrimaryButtonEnabled = condition;
            IsLightDismissEnabled = !condition;
        }

        public ObservableCollection<PollOptionViewModel> Items { get; private set; }

        private void AddAnOption_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Items.Count < _viewModel.ClientService.Options.PollAnswerCountMax && !string.IsNullOrEmpty(AddAnOption.Text))
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

        private void Question_ContentChanged(object sender, EventArgs e)
        {
            UpdatePrimaryButton();
        }

        private void Option_ContentChanged(object sender, EventArgs e)
        {
            if (sender is FormattedTextInputHost textBox && textBox.Tag is PollOptionViewModel option)
            {
                option.Text = textBox.GetFormattedText();
            }

            UpdatePrimaryButton();
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check && check.DataContext is PollOptionViewModel option)
            {
                var exclusive = AllowsMultipleAnswers.IsChecked != true;

                foreach (var item in Items)
                {
                    if (item == option)
                    {
                        item.IsChecked = true;
                    }
                    else if (exclusive)
                    {
                        item.IsChecked = false;
                    }
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
            if (sender is FormattedTextInputHost text && text.DataContext is PollOptionViewModel option)
            {
                text.Tag = option;
                text.DataContext = _viewModel;
                text.CustomEmoji = text.Parent.GetChild<CustomEmojiCanvas>();

                text.SetText(option.Text);

                if (option.FocusOnLoaded)
                {
                    text.MoveCaretToEnd();
                    text.FocusInput(FocusState.Keyboard);
                }

                option.FocusOnLoaded = false;
            }
        }

        private void Option_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not FormattedTextInputHost text || text.Tag is not PollOptionViewModel option)
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

        private void QuestionText_GotFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(QuestionEmoji, true);
        }

        private void QuestionText_LostFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(QuestionEmoji, false);
        }

        private void DescriptionText_GotFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(DescriptionEmoji, true);
        }

        private void DescriptionText_LostFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(DescriptionEmoji, false);
        }

        private void Option_GotFocus(object sender, RoutedEventArgs e)
        {
            AddAnOption.IsReadOnly = false;

            if (sender is FormattedTextInputHost textBox && textBox.Parent != null)
            {
                OnVisibleChanged(textBox.Parent.GetChild<GlyphButton>(), true);
            }
        }

        private void Option_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextInputHost textBox && textBox.Parent != null)
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

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Opacity = newValue ? 1 : 0;
                visual.Scale = new Vector3(true ? newValue ? 1 : 0 : 1);

                sender.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
            };

            var anim1 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(0, newValue ? 0 : 1);
            anim1.InsertKeyFrame(1, newValue ? 1 : 0);
            visual.StartAnimation("Opacity", anim1);

            var anim2 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
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

            var inner = container.GetChild<FormattedTextInputHost>();
            if (inner == null)
            {
                return;
            }

            inner.FocusInput(FocusState.Keyboard);
        }

        private void Multiple_Toggled(object sender, RoutedEventArgs e)
        {
            // IsChecked is set in XAML, and this can be raised during InitializeComponent.
            if (Items == null)
            {
                return;
            }

            if (AllowsMultipleAnswers.IsChecked != true)
            {
                var found = false;

                foreach (var item in Items)
                {
                    if (item.IsChecked)
                    {
                        item.IsChecked = !found;
                        found = true;
                    }
                }
            }

            UpdatePrimaryButton();
        }

        private void IsAnonymous_Toggled(object sender, RoutedEventArgs e)
        {
            // IsChecked is set in XAML, and this can be raised during InitializeComponent.
            if (Items != null)
            {
                UpdateAllowsAddingOptions();
            }
        }

        private void UpdateAllowsAddingOptions()
        {
            // IsAnonymous being checked means showing who voted, so the poll is not anonymous.
            var allowed = IsAnonymous.IsChecked == true && Quiz.IsChecked != true;

            AllowsAddingOptions.IsEnabled = allowed;

            if (!allowed)
            {
                AllowsAddingOptions.IsChecked = false;
            }
        }

        private void Quiz_Toggled(object sender, RoutedEventArgs e)
        {
            QuizSettings.Visibility = Quiz.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (Quiz.IsChecked == true)
            {
                AllowsRevoting.IsChecked = false;
            }

            foreach (var item in Items)
            {
                item.IsChecked = false;
                item.IsQuiz = Quiz.IsChecked == true;
            }

            AddAnOption.Margin = new Thickness(Quiz.IsChecked == true ? 28 + 24 : 24, 8, 24, 0);

            UpdateAllowsAddingOptions();
            UpdatePrimaryButton();
        }

        private FormattedTextInputHost _target;

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // What has focus is the box inside the host, on either side of the switch.
            var element = FocusManagerEx.TryGetFocusedElement(XamlRoot) as DependencyObject;
            if (element?.GetParentOrSelf<FormattedTextInputHost>() is not FormattedTextInputHost textBox)
            {
                return;
            }

            _target = textBox;

            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(textBox, FlyoutPlacementMode.Auto, FlyoutShowMode.Transient, source: sender as FrameworkElement);
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                _target?.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                _target?.InsertEmoji(sticker);
            }

            _target?.FocusInput(FocusState.Programmatic);
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _target = null;
        }

        private async void CountryCodes_Click(object sender, RoutedEventArgs e)
        {
            var popup = new ChooseCountriesPopup(_viewModel.ClientService, _countryCodes.Select(x => x.CountryCode));

            var confirm = await _viewModel.ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                _countryCodes = popup.SelectedItems;
                CountryCodes.Badge = _countryCodes.Count > 1
                    ? Locale.Declension(Strings.R.PollV2AllowedCountriesListManyP, _countryCodes.Count)
                    : _countryCodes.Count > 0
                    ? _countryCodes[0].Name
                    : Strings.SearchCountriesSelect;
            }
        }

        private void CountriesOnly_Toggled(object sender, RoutedEventArgs e)
        {
            CountryCodes.Visibility = CountriesOnly.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LimitDuration_Toggled(object sender, RoutedEventArgs e)
        {
            Duration.Visibility = LimitDuration.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            HideResults.Visibility = LimitDuration.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Duration_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            foreach (var hours in new[] { 1, 3, 8, 24, 72 })
            {
                flyout.CreateFlyoutItem(SetDuration, hours, Locale.Declension(Strings.R.Hours, hours));
            }

            flyout.ShowAt(Duration, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void SetDuration(int hours)
        {
            _openPeriod = hours * 60 * 60;
            Duration.Badge = Locale.Declension(Strings.R.Hours, hours);
        }
    }

    public partial class PollOptionViewModel : BindableBase
    {
        private readonly Action<PollOptionViewModel> _remove;

        public PollOptionViewModel(string text, bool quiz, bool focus, Action<PollOptionViewModel> remove)
        {
            _text = text.AsFormattedText();
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
