using LinqToVisualTree;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Dialogs;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using static Unigram.Controls.Chats.ChatTextBox;

namespace Unigram.Controls.Chats
{
    public sealed partial class ChatSearchMask : UserControl
    {
        public ChatSearchViewModel ViewModel => DataContext as ChatSearchViewModel;

        public ChatSearchMask()
        {
            InitializeComponent();

            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.UIElement", "PreviewKeyDown"))
            {
                Field.PreviewKeyDown += OnKeyDown;
            }
            else
            {
                Field.KeyDown += OnKeyDown;
            }

            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        public void Update(ChatSearchViewModel viewModel)
        {
            DataContext = viewModel;
            Bindings.Update();
        }

        private async void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Visibility == Visibility.Visible)
            {
                await Task.Delay(100);
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            AutocompleteHeader.Margin = new Thickness(0, -height, 0, padding);
            AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");

            var scrollingHost = ListAutocomplete.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                scrollingHost.ChangeView(null, scrollingHost.ScrollableHeight, null, true);
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            if (args.Item is User user)
            {
                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.GetFullName();
                username.Text = string.IsNullOrEmpty(user.Username) ? string.Empty : $" @{user.Username}";

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }
        }

        #endregion

        #region Binding

        private string ConvertOf(int index, int count)
        {
            return string.Format(Strings.Resources.Of, index + 1, count);
        }

        #endregion

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (Field.State == ChatSearchState.Members)
            {
                ViewModel.Autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Dialog.Chat.Id, Field.Text, false, true);
            }
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (e.Key == Windows.System.VirtualKey.Enter && !shift && Field.State != ChatSearchState.Members)
            {
                ViewModel.Search(Field.Text, Field.From);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter && shift && Field.State != ChatSearchState.Members)
            {
                ViewModel.NextCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(Field.Text))
            {
                OnBackRequested();
                e.Handled = true;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Search(Field.Text, Field.From);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequested();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            SetState(ChatSearchState.Members);
            Field.Focus(FocusState.Keyboard);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is User user)
            {
                SetState(ChatSearchState.TextByMember, user);
            }
        }

        private void SetState(ChatSearchState state, User user = null)
        {
            Field.Text = string.Empty;
            Field.From = user;
            Field.State = state;

            switch (state)
            {
                case ChatSearchState.Members:
                    ToolsPanel.Visibility = Visibility.Collapsed;
                    ViewModel.Autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Dialog.Chat.Id, string.Empty, false, true);
                    break;
                case ChatSearchState.TextByMember:
                    ToolsPanel.Visibility = Visibility.Collapsed;
                    ViewModel.Autocomplete = null;
                    break;
                default:
                    ToolsPanel.Visibility = Visibility.Visible;
                    ViewModel.Autocomplete = null;
                    break;
            }
        }

        public bool OnBackRequested()
        {
            if (!string.IsNullOrEmpty(Field.Text))
            {
                Field.Text = string.Empty;
            }
            else if (Field.State == ChatSearchState.TextByMember)
            {
                SetState(ChatSearchState.Members);
            }
            else if (Field.State == ChatSearchState.Members)
            {
                SetState(ChatSearchState.Text);
            }
            else if (ViewModel?.Dialog?.Search != null)
            {
                ViewModel.Dialog.DisposeSearch();
            }

            return true;
        }
    }
}
