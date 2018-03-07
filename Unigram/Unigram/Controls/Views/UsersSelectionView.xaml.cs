using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Supergroups;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class UsersSelectionView : Grid
    {
        public UsersSelectionViewModel ViewModel => DataContext as UsersSelectionViewModel;

        public UsersSelectionView()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                DataContext = new SettingsBlockUserViewModel(null, null, null);
            }

            InitializeComponent();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(async x =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                }
            });
        }

        public void Attach()
        {
            ViewModel.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                List.SelectionChanged -= ListView_SelectionChanged;
                foreach (var item in e.NewItems)
                {
                    var listItem = List.Items?.SingleOrDefault(li => li is User user && (item as User).Id == user.Id);
                    if (listItem != null)
                    {
                        List.SelectedItems.Add(item);
                    }
                }
                List.SelectionChanged += ListView_SelectionChanged;
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                List.SelectionChanged -= ListView_SelectionChanged;
                foreach (var item in e.OldItems)
                {
                    var listItem = List.Items?.SingleOrDefault(li => li is User user && (item as User).Id == user.Id);
                    if (listItem != null)
                    {
                        List.SelectedItems.Remove(item);
                    }
                }
                List.SelectionChanged += ListView_SelectionChanged;
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is User user && ViewModel.SelectedItems.All(selectedUser => selectedUser.Id != user.Id))
                    {
                        ViewModel.SelectedItems.Add(user);
                    }
                }
            }

            if (e.RemovedItems != null)
            {
                foreach (var item in e.RemovedItems)
                {
                    if (item is User user && ViewModel.SelectedItems.Any(selectedUser => selectedUser.Id == user.Id))
                    {
                        ViewModel.SelectedItems.Remove(user);
                    }
                }
            }
        }

        private void SearchListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is User user)
                    {
                        ViewModel.SelectedItems.Add(user);
                    }
                }
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SingleCommand.Execute(e.ClickedItem as User);
            }
        }

        private async void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ContentPanel.Visibility = Visibility.Visible;

                ViewModel.Search = null;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;

                var items = ViewModel.Search = new SearchUsersCollection(ViewModel.ProtoService, SearchField.Text);
                await items.LoadMoreItemsAsync(0);
            }
        }

        private void TagsTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
        }

        #region Binding

        private Visibility ConvertMaximum(int maximum, bool infinite)
        {
            return (maximum == int.MaxValue && infinite) || maximum == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        public object Header { get; set; }

        public DataTemplate HeaderTemplate { get; set; }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var user = args.Item as User;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = LastSeenConverter.GetLabel(user, false);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        private void Search_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var result = args.Item as SearchResult;
            var chat = result.Chat;
            var user = result.User ?? ViewModel.ProtoService.GetUser(chat);

            if (user == null)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                if (result.IsPublic)
                {
                    subtitle.Text = $"@{user.Username}";
                }
                else
                {
                    subtitle.Text = LastSeenConverter.GetLabel(user, true);
                }

                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "TextHighlighters"))
                {
                    if (subtitle.Text.StartsWith($"@{result.Query}", StringComparison.OrdinalIgnoreCase))
                    {
                        var highligher = new TextHighlighter();
                        highligher.Foreground = new SolidColorBrush(Colors.Red);
                        highligher.Background = new SolidColorBrush(Colors.Transparent);
                        highligher.Ranges.Add(new TextRange { StartIndex = 1, Length = result.Query.Length });

                        subtitle.TextHighlighters.Add(highligher);
                    }
                    else
                    {
                        subtitle.TextHighlighters.Clear();
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(Search_ContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
