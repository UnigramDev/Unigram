//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using static Telegram.Controls.Chats.ChatTextBox;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatSearchBar : UserControl
    {
        public ChatSearchViewModel ViewModel => DataContext as ChatSearchViewModel;

        private readonly EventDebouncer<TextChangedEventArgs> _debouncer;

        public ChatSearchBar()
        {
            InitializeComponent();

            _debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => Field.TextChanged += new TextChangedEventHandler(handler));
            _debouncer.Invoked += (s, args) =>
            {
                if (Field.State != ChatSearchState.Members)
                {
                    ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter);
                }
            };
        }

        public void Update(ChatSearchViewModel viewModel)
        {
            DataContext = viewModel;
            Bindings.Update();

            Field.Text = string.Empty;
            Field.From = null;
            Field.Filter = null;
            Field.State = ChatSearchState.Text;

            if (viewModel != null)
            {
                var history = viewModel.Dialog.Type is not DialogType.History and not DialogType.Thread;
                SearchPrevious.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                SearchNext.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                ToolsPanel.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
            }

            ShowHide(viewModel != null);
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var visual = ElementCompositionPreview.GetElementVisual(TopPanel);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                visual.Offset = new Vector3();

                if (show)
                {
                    _collapsed = false;
                    Field.Focus(FocusState.Keyboard);
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 48);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -48, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Offset", offset);

            batch.End();
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

                name.Text = user.FullName();

                if (user.HasActiveUsername(out string usernameValue))
                {
                    username.Text = $" @{usernameValue}";
                }
                else
                {
                    username.Text = string.Empty;
                }

                photo.SetUser(ViewModel.ClientService, user, 36);
            }
            else if (args.Item is ChatSearchMediaFilter filter)
            {
                var child = content.Children[0] as Border;
                var glyph = child.Child as TextBlock;
                var title = content.Children[1] as TextBlock;

                glyph.Text = filter.Glyph;
                title.Text = filter.Text;

                if (filter.Filter is SearchMessagesFilterVideoNote)
                {
                    glyph.FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily;
                }
                else
                {
                    glyph.FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily;
                }
            }
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
                ViewModel.Autocomplete = new UsernameCollection(ViewModel.ClientService, ViewModel.Dialog.Chat.Id, 0, Field.Text, false, true);
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && Field.State == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var shift = Window.Current.CoreWindow.IsKeyDown(Windows.System.VirtualKey.Shift);

            if (e.Key == Windows.System.VirtualKey.Enter && !shift && Field.State != ChatSearchState.Members)
            {
                _debouncer.Cancel();
                ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter && shift && Field.State != ChatSearchState.Members)
            {
                ViewModel?.NextCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(Field.Text))
            {
                Delete(false);
                e.Handled = true;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _debouncer.Cancel();
            ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter);
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

        private void FilterByMedia_Click(object sender, RoutedEventArgs e)
        {
            SetState(ChatSearchState.Media);
            Field.Focus(FocusState.Keyboard);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is User from)
            {
                SetState(ChatSearchState.TextByMember, new MessageSenderUser(from.Id));
            }
            else if (e.ClickedItem is ChatSearchMediaFilter filter)
            {
                SetState(ChatSearchState.TextByMedia, null, filter);
            }
        }

        private void SetState(ChatSearchState state, MessageSender from = null, ChatSearchMediaFilter filter = null)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (from != null)
            {
                Field.Filter = null;
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
                Field.Filter = filter;

                Field.Header = filter?.Text;
            }

            Field.Text = string.Empty;
            Field.State = state;

            switch (state)
            {
                case ChatSearchState.Members:
                    ToolsPanel.Visibility = Visibility.Collapsed;
                    viewModel.Autocomplete = new UsernameCollection(viewModel.ClientService, viewModel.Dialog.Chat.Id, 0, string.Empty, false, true);
                    break;
                case ChatSearchState.Media:
                    ToolsPanel.Visibility = Visibility.Collapsed;
                    viewModel.Autocomplete = viewModel.Filters;
                    break;
                case ChatSearchState.TextByMember:
                case ChatSearchState.TextByMedia:
                    ToolsPanel.Visibility = Visibility.Collapsed;
                    viewModel.Autocomplete = null;
                    break;
                default:
                    ToolsPanel.Visibility = viewModel.Dialog.Type is not DialogType.History and not DialogType.Thread ? Visibility.Collapsed : Visibility.Visible;
                    viewModel.Autocomplete = null;
                    break;
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && state == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
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
            else if (Field.State == ChatSearchState.TextByMedia)
            {
                SetState(ChatSearchState.Media);
            }
            else if (Field.State is ChatSearchState.Members or ChatSearchState.Media)
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
    }
}
