//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
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
                if (Field.State != ChatSearchState.Members && !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter, ViewModel.SavedMessagesTag);
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
            Field.From = null;
            Field.Filter = null;
            Field.State = ChatSearchState.Text;

            if (viewModel != null)
            {
                var history = viewModel.Dialog.Type is
                    not DialogType.History and
                    not DialogType.Thread and
                    not DialogType.SavedMessagesTopic;

                SearchPrevious.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                SearchNext.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
                ToolsPanel.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
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

            var visual = ElementComposition.GetElementVisual(TopPanel);
            var header = ElementComposition.GetElementVisual(_viewHeader);
            var clipper = ElementComposition.GetElementVisual(_viewClipperOuter);
            clipper.Clip = null; //visual.Compositor.CreateInsetClip();

            ElementCompositionPreview.SetIsTranslationEnabled(_viewClipperOuter, true);

            var batch = visual.Compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                visual.Offset = new Vector3();

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

            var height = _viewClipperOuter.ActualSize.Y - _viewDate.ActualSize.Y;
            Logger.Info(height);

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 1 : 0, height - 48);
            clip.InsertKeyFrame(show ? 0 : 1, -48);
            clip.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3(0, -height, 0));
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3());
            offset.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            //clipper.Clip.StartAnimation("TopInset", clip);
            clipper.StartAnimation("Translation", offset);

            var opacity1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            var opacity2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity1);
            //header.StartAnimation("Opacity", opacity2);

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

            var visual = ElementComposition.GetElementVisual(SecondaryRoot);
            var visual1 = ElementComposition.GetElementVisual(TagsRoot);
            var date = ElementComposition.GetElementVisual(_viewDate);
            visual1.Clip = visual.Compositor.CreateInsetClip();
            visual.Clip = visual.Compositor.CreateInsetClip();

            ElementCompositionPreview.SetIsTranslationEnabled(SecondaryRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(_viewDate, true);

            var batch = visual.Compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                //visual.Offset = new Vector3();
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 48);
            clip.InsertKeyFrame(show ? 1 : 0, -48);
            clip.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            var clip1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip1.InsertKeyFrame(show ? 0 : 1, 48);
            clip1.InsertKeyFrame(show ? 1 : 0, 0);
            clip1.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -48, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            var translation = visual.Compositor.CreateVector3KeyFrameAnimation();
            translation.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 0, 0));
            translation.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 48, 0));
            translation.Duration = TimeSpan.FromSeconds(0.167); //Constants.FastAnimation;

            visual1.Clip.StartAnimation("TopInset", clip1);
            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Translation", offset);
            date.StartAnimation("Translation", translation);

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

                photo.SetUser(ViewModel.ClientService, user, 32);
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
            Tags.Visibility = tags?.Tags.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                ViewModel.Autocomplete = new UsernameCollection(ViewModel.ClientService, ViewModel.Dialog.Chat.Id, 0, Field.Text, false, true);
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && Field.State == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var shift = WindowContext.IsKeyDown(Windows.System.VirtualKey.Shift);

            if (e.Key == Windows.System.VirtualKey.Enter && !shift && Field.State != ChatSearchState.Members)
            {
                _debouncer.Cancel();
                ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter, ViewModel.SavedMessagesTag);
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
            ViewModel?.Search(Field.Text, Field.From, Field.Filter?.Filter, ViewModel.SavedMessagesTag);
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

        private UIElement _viewHeader;
        private UIElement _viewClipperOuter;
        private UIElement _viewDate;

        public void InitializeParent(UIElement header, UIElement clipperOuter, UIElement date)
        {
            _viewHeader = header;
            _viewClipperOuter = clipperOuter;
            _viewDate = date;
        }
    }
}
