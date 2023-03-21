//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class CreatePollPopup : ContentPopup
    {
        private const int MAXIMUM_OPTIONS = 10;

        public CreatePollPopup(bool forceQuiz, bool forceRegular, bool forceAnonymous)
        {
            InitializeComponent();

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

        public string Question
        {
            get
            {
                return QuestionText.Text.Format();
            }
        }

        public IList<string> Options
        {
            get
            {
                return Items.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => x.Text.Format()).ToList();
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
                AddInfo.Text = string.Format(Strings.AddAnOptionInfo, Locale.Declension("Option", MAXIMUM_OPTIONS - Items.Count - 1));
            }

            UpdatePrimaryButton();
        }

        private void UpdatePrimaryButton()
        {
            var condition = !string.IsNullOrEmpty(Question);
            condition = condition && Items.Count(x => !string.IsNullOrEmpty(x.Text)) >= 2;

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

        private void Question_TextChanged(object sender, TextChangedEventArgs e)
        {
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
            if (sender is TextBox text && text.DataContext is PollOptionViewModel option && option.FocusOnLoaded)
            {
                option.FocusOnLoaded = false;

                text.SelectionStart = text.Text.Length;
                text.Focus(FocusState.Keyboard);
            }
        }

        private void Option_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Back && sender is TextBox text && text.DataContext is PollOptionViewModel option && string.IsNullOrEmpty(text.Text))
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
        }

        private void Option_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox text && text.DataContext is PollOptionViewModel option)
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
        }

        private void Focus(int option)
        {
            var container = Presenter.ContainerFromIndex(option) as ContentPresenter;
            if (container == null)
            {
                return;
            }

            var inner = container.Descendants<TextBox>().FirstOrDefault();
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
    }

    public class PollOptionViewModel : BindableBase
    {
        private readonly Action<PollOptionViewModel> _remove;

        public PollOptionViewModel(string text, bool quiz, bool focus, Action<PollOptionViewModel> remove)
        {
            _text = text;
            _isQuiz = quiz;
            _focusOnLoaded = focus;
            _remove = remove;
            RemoveCommand = new RelayCommand(() => _remove(this));
        }

        private string _text;
        public string Text
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

        public RelayCommand RemoveCommand { get; }
    }
}
