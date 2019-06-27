using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Template10.Mvvm;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class CreatePollView : TLContentDialog
    {
        private const int MAXIMUM_OPTIONS = 10;

        public CreatePollView()
        {
            InitializeComponent();

            Title = Strings.Resources.NewPoll;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            Items = new ObservableCollection<PollOptionViewModel>();
            Items.CollectionChanged += Items_CollectionChanged;
            Items.Add(new PollOptionViewModel(false, option => Remove_Click(option)));
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

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (MAXIMUM_OPTIONS - Items.Count <= 0)
            {
                Add.Visibility = Visibility.Collapsed;
                Separator.Visibility = Visibility.Visible;
                InfoLabel.Text = Strings.Resources.AddAnOptionInfoMax;
            }
            else
            {
                Add.Visibility = Visibility.Visible;
                Separator.Visibility = Visibility.Collapsed;
                InfoLabel.Text = string.Format(Strings.Resources.AddAnOptionInfo, Locale.Declension("Option", MAXIMUM_OPTIONS - Items.Count));
            }

            IsPrimaryButtonEnabled = Items.Count >= 2;
        }

        public ObservableCollection<PollOptionViewModel> Items { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Items.Count < MAXIMUM_OPTIONS)
            {
                Items.Add(new PollOptionViewModel(true, option => Remove_Click(option)));
            }
        }

        private void Remove_Click(PollOptionViewModel option)
        {
            Items.Remove(option);
            Focus(Items.Count - 1);
        }

        private void Option_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox text && text.DataContext is PollOptionViewModel option && option.FocusOnLoaded)
            {
                option.FocusOnLoaded = false;
                text.Focus(FocusState.Keyboard);
            }
        }

        private void Option_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox text && text.DataContext is PollOptionViewModel option)
            {
                var index = Items.IndexOf(option);
                if (index < Items.Count - 1)
                {
                    Focus(index + 1);
                }
                else
                {
                    Add_Click(null, null);
                }
            }
        }

        private void Focus(int option)
        {
            var container = Presenter.ContainerFromIndex(option) as ContentPresenter;
            if (container == null)
            {
                return;
            }

            var inner = container.Descendants<TextBox>().FirstOrDefault() as TextBox;
            if (inner == null)
            {
                return;
            }

            inner.Focus(FocusState.Keyboard);
        }
    }

    public class PollOptionViewModel : BindableBase
    {
        private readonly Action<PollOptionViewModel> _remove;

        public PollOptionViewModel(bool focus, Action<PollOptionViewModel> remove)
        {
            _focusOnLoaded = focus;
            _remove = remove;
            RemoveCommand = new RelayCommand(() => _remove(this));
        }

        private string _text;
        public string Text
        {
            get { return _text; }
            set { Set(ref _text, value); }
        }

        private bool _focusOnLoaded;
        public bool FocusOnLoaded
        {
            get { return _focusOnLoaded; }
            set { Set(ref _focusOnLoaded, value); }
        }

        public RelayCommand RemoveCommand { get; }
    }
}
