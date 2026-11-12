//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using static Telegram.Controls.Chats.ChatTextBox;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatSearchBar : UserControl
    {
        public ChatSearchViewModel ViewModel => DataContext as ChatSearchViewModel;

        private readonly EventDebouncer<TextChangedEventArgs> _debouncer;

        private readonly ThemeShadowEx _islandShadow;

        public ChatSearchBar()
        {
            InitializeComponent();

            _islandShadow = new ThemeShadowEx(IslandShadow, Constants.BubbleElevation);

            if (ApiInfo.CanCreateThemeShadow)
            {
                RootAutocomplete.Shadow = new ThemeShadow();
                RootAutocomplete.Translation = new Vector3(0, 0, 32);
            }

            ListAutocomplete.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnAutocompleteChanged);
            OnAutocompleteChanged(null, null);

            _debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => Field.TextChanged += new TextChangedEventHandler(handler));
            _debouncer.Invoked += (s, args) =>
            {
                if (Field.State != ChatSearchState.Members && !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    ViewModel?.Search(Field.Text, Field.From, ViewModel.SavedMessagesTag, null);
                }
            };
        }

        public void Update(ChatSearchViewModel viewModel)
        {
            if (_empty == (viewModel == null))
            {
                return;
            }

            _empty = viewModel == null;

            if (viewModel != null)
            {
                DataContext = viewModel;
                Bindings.Update();
            }

            Field.Text = viewModel?.Query ?? string.Empty;
            Field.From = viewModel?.From;
            Field.State = ChatSearchState.Text;

            if (viewModel != null)
            {
                var history = viewModel.Dialog.Type is
                    not DialogType.History and
                    not DialogType.Thread;

                SearchPrevious.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                SearchNext.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                FilterByMember.Visibility = history ? Visibility.Collapsed : Visibility.Visible;

                if (viewModel.From != null)
                {
                    SetState(ChatSearchState.TextByMember, viewModel.From);
                }
            }

            ShowHide(viewModel != null);
        }

        private bool _collapsed = true;
        private bool _empty = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            IsHitTestVisible = show;

            ShowHideSavedTags(show);

            // The chat hides its own header and the headers under it as this shows.
            var visual = ElementComposition.GetElementVisual(Island);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_collapsed)
                {
                    DataContext = null;
                    Visibility = Visibility.Collapsed;
                }
                else
                {
                    Field.Focus(FocusState.Keyboard);
                    Field.SelectAll();
                }
            };

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);
            opacity.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);

            batch.End();
        }

        private bool _savedTagsCollapsed = true;

        private void ShowHideSavedTags(bool show)
        {
            if (_savedTagsCollapsed != show)
            {
                return;
            }

            _savedTagsCollapsed = !show;
            Field.PlaceholderText = show
                ? Strings.SavedTagSearchHint
                : Strings.Search;
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is User user && args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.FullName();

                if (user.HasActiveUsername(out string usernameValue))
                {
                    username.Text = $" @{usernameValue}";
                }
                else
                {
                    username.Text = string.Empty;
                }

                photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
            }
            else if (args.Item is Message message && args.ItemContainer.ContentTemplateRoot is ChatCell cell)
            {
                cell.UpdateMessage(ViewModel.ClientService, message, true);
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private string ConvertOf(int index, int count)
        {
            if (count == 0)
            {
                return "0/0";
            }

            return string.Format("{0}/{1}", index + 1, count);
        }

        private object ConvertTag(SavedMessagesTags tags)
        {
            Tags.UpdateMessageReactions(ViewModel, tags);
            TagsRoot.Visibility = tags?.Tags.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateAutocompleteCard();
            return null;
        }

        #endregion

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.Equals(Field.Text, Strings.SearchFrom) && ViewModel.IsFromEnabled && Field.State == ChatSearchState.Text)
            {
                SetState(ChatSearchState.Members);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (Field.State == ChatSearchState.Members)
            {
                ViewModel.Autocomplete = new UsernameCollection(ViewModel.ClientService, ViewModel.Dialog.Chat.Id, ViewModel.Dialog.TopicId, Field.Text, false, false, true, true);
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && Field.State == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var modifiers = WindowContext.KeyModifiers();

            if (e.Key == VirtualKey.Enter && modifiers == VirtualKeyModifiers.None && Field.State != ChatSearchState.Members)
            {
                _debouncer.Cancel();
                ViewModel?.Search(Field.Text, Field.From, ViewModel.SavedMessagesTag, ListAutocomplete.SelectedItem as Message);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter && modifiers == VirtualKeyModifiers.Shift && Field.State != ChatSearchState.Members)
            {
                ViewModel?.NextCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Back && string.IsNullOrEmpty(Field.Text))
            {
                Delete(false);
                e.Handled = true;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _debouncer.Cancel();
            ViewModel?.Search(Field.Text, Field.From, ViewModel.SavedMessagesTag, null);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Delete(true);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequested();
        }

        private void FilterByMember_Click(object sender, RoutedEventArgs e)
        {
            SetState(ChatSearchState.Members);
            Field.Focus(FocusState.Keyboard);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is User from)
            {
                SetState(ChatSearchState.TextByMember, new MessageSenderUser(from.Id));
            }
            else if (e.ClickedItem is Message message)
            {
                ViewModel.SetSelectedItem(message);
            }
        }

        private void SetState(ChatSearchState state, MessageSender from = null)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (from != null)
            {
                Field.From = from;

                if (viewModel.ClientService.TryGetUser(from, out User user))
                {
                    Field.Header = user.FullName();
                }
                else if (viewModel.ClientService.TryGetChat(from, out Chat chat))
                {
                    Field.Header = chat.Title;
                }
            }
            else
            {
                Field.From = null;
                Field.Header = null;
            }

            Field.Text = string.Empty;
            Field.State = state;

            switch (state)
            {
                case ChatSearchState.Members:
                    FilterByMember.Visibility = Visibility.Collapsed;
                    viewModel.Autocomplete = new UsernameCollection(viewModel.ClientService, viewModel.Dialog.Chat.Id, viewModel.Dialog.TopicId, string.Empty, false, false, true, true);
                    break;
                case ChatSearchState.TextByMember:
                    FilterByMember.Visibility = Visibility.Collapsed;
                    viewModel.Autocomplete = null;
                    break;
                default:
                    FilterByMember.Visibility = viewModel.Dialog.Type is not DialogType.History and not DialogType.Thread ? Visibility.Collapsed : Visibility.Visible;
                    viewModel.Autocomplete = null;
                    break;
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && state == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;

            _debouncer.Cancel();
            ViewModel?.Search(Field.Text, Field.From, ViewModel.SavedMessagesTag, null);
        }

        private void Delete(bool allowDispose)
        {
            if (!string.IsNullOrEmpty(Field.Text))
            {
                Field.Text = string.Empty;
            }
            else if (Field.State == ChatSearchState.TextByMember)
            {
                SetState(ChatSearchState.Members);
            }
            else if (Field.State is ChatSearchState.Members)
            {
                SetState(ChatSearchState.Text);
            }
            else if (ViewModel?.Dialog?.Search != null && allowDispose)
            {
                ViewModel.Dialog.DisposeSearch();
            }
        }

        public bool OnBackRequested()
        {
            SetState(ChatSearchState.Text);

            if (ViewModel?.Dialog?.Search != null)
            {
                ViewModel.Dialog.DisposeSearch();
            }

            return true;
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //var visible = e.NewSize.Width > 0 && e.NewSize.Height >= 2;
            //visible &= ListAutocomplete.Visibility == Visibility.Visible;
            //Field.CornerRadius = new CornerRadius(4, 4, visible ? 0 : 4, visible ? 0 : 4);
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            AutocompleteSlot.Visibility = Visibility.Visible;
            UpdateAutocompleteCard();
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            AutocompleteSlot.Visibility = Visibility.Collapsed;
            UpdateAutocompleteCard();
        }

        private INotifyCollectionChanged _autocomplete;

        private void OnAutocompleteChanged(DependencyObject sender, DependencyProperty dp)
        {
            // A new source is handed over empty and filled asynchronously, so the assignment alone
            // never reports a row: the collection has to be watched as well.
            if (_autocomplete != null)
            {
                _autocomplete.CollectionChanged -= OnAutocompleteCollectionChanged;
                _autocomplete = null;
            }

            var source = ListAutocomplete.ItemsSource;

            if (source is INotifyCollectionChanged notifying)
            {
                _autocomplete = notifying;
                _autocomplete.CollectionChanged += OnAutocompleteCollectionChanged;
            }

            UpdateAutocomplete();

            // A collapsed list is never measured, so its panel never asks for a first page. Members
            // are handed over as an empty collection, so nothing would ever ask on their behalf and
            // the list would stay empty because it is empty. Search results arrive loaded already.
            if (source is ISupportIncrementalLoading { HasMoreItems: true } incremental && source is ICollection { Count: 0 })
            {
                _ = incremental.LoadMoreItemsAsync(20);
            }
        }

        private void OnAutocompleteCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateAutocomplete();
        }

        // Height comes from the number of rows, never from the content: a list that measures its
        // own content hands the virtualizing panel a viewport that moves as it realizes items, and
        // against a source that loads incrementally a viewport that grows asks for more items and
        // grows again, which is a layout that never settles. Rows are uniform, so counting them
        // settles it before layout runs.
        private void UpdateAutocomplete()
        {
            var count = ListAutocomplete.ItemsSource is ICollection collection ? collection.Count : 0;
            var height = Math.Min(ListAutocomplete.MaxHeight, count * AutocompleteRowHeight);

            // An incremental source adds one item at a time, and each row is a layout pass.
            if (ListAutocomplete.Height == height)
            {
                return;
            }

            var visible = count > 0;

            ListAutocomplete.Height = height;
            ListAutocomplete.Visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateAutocompleteCard();
        }

        // The tags stay at the top of the card while the rows scroll under them.
        private void UpdateAutocompleteCard()
        {
            var tags = TagsRoot.Visibility == Visibility.Visible;
            var rows = ListAutocomplete.Visibility == Visibility.Visible;
            var visible = tags || rows;

            TagsDivider.Visibility = tags && rows
                ? Visibility.Visible
                : Visibility.Collapsed;

            RootAutocomplete.Visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Shown only while the field has focus, which collapses the slot otherwise.
            visible &= AutocompleteSlot.Visibility == Visibility.Visible;

            // The field's bottom corners meet the card's square top while it shows, and the buttons
            // at either end of the field follow its corners on their outer side.
            var bottom = visible ? 0 : 16;
            var button = visible ? 4 : 16;

            Field.CornerRadius = new CornerRadius(16, 16, bottom, bottom);
            QueryButton.CornerRadius = new CornerRadius(16, 4, 4, button);
            DeleteButton.CornerRadius = new CornerRadius(4, 16, button, 4);
        }

        // The two templates are different heights, and neither is measured here: a member row is the
        // 32px picture inside MentionTemplate's 6px padding, a result row is a ChatCell at its own
        // MinHeight of 64. Both are declared in Themes/Messages.xaml.
        private double AutocompleteRowHeight => ListAutocomplete.ItemsSource is UsernameCollection
            ? 44
            : 64;

        private void SelectedItemLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Field.Padding = new Thickness(0, 3, 6 + e.NewSize.Width, 6);
        }
    }
}
