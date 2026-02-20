//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class CaptionTextBox : FormattedTextBox
    {
        public ViewModelBase ViewModel { get; set; }

        public IViewWithAutocomplete View { get; set; }

        public long ChatId => ViewModel is ComposeViewModel compose ? compose.Chat?.Id ?? 0 : 0;

        public CaptionTextBox()
        {
            SelectionChanged += OnSelectionChanged;
        }

        private ListViewBase _controlledList;
        public ListViewBase ControlledList
        {
            get => _controlledList;
            set => SetControlledList(value);
        }

        private void SetControlledList(ListViewBase value)
        {
            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Remove(_controlledList);
            }

            _controlledList = value;

            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Add(_controlledList);
            }
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space && Document.Selection.Length == 0)
            {
                try
                {
                    var clone = Document.Selection.GetClone();
                    if (clone.EndPosition > Document.Selection.EndPosition && AreTheSame(clone.CharacterFormat, Document.Selection.CharacterFormat))
                    {

                    }
                    else
                    {
                        Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right or VirtualKey.Tab or VirtualKey.Enter)
            {
                IAutocompleteCollection autocomplete;
                ListViewBase autocompleteList;

                if (_emojiFlyout?.Content is ChatTextFlyout presenter)
                {
                    autocomplete = presenter.ItemsSource;
                    autocompleteList = presenter.ControlledList;
                }
                else
                {
                    autocomplete = View?.Autocomplete;
                    autocompleteList = ControlledList;
                }

                var modifiers = WindowContext.KeyModifiers();

                if (e.Key is VirtualKey.Up or VirtualKey.Down && modifiers == VirtualKeyModifiers.None)
                {
                    if (autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete?.Orientation == Orientation.Vertical)
                    {
                        autocompleteList.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Up ? -1 : 1;
                        var next = autocompleteList.SelectedIndex + index;
                        if (next >= 0 && next < autocomplete.Count)
                        {
                            autocompleteList.SelectedIndex = next;
                            autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);
                        }

                        e.Handled = true;
                    }
                }
                else if (e.Key is VirtualKey.Left or VirtualKey.Right && modifiers == VirtualKeyModifiers.None)
                {
                    if (autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete?.Orientation == Orientation.Horizontal)
                    {
                        if (autocompleteList.SelectedIndex == 0 && e.Key == VirtualKey.Left)
                        {
                            autocompleteList.SelectedIndex = -1;
                            e.Handled = true;
                        }
                        else if (autocompleteList.SelectedIndex == autocompleteList.Items.Count - 1 && e.Key == VirtualKey.Right)
                        {
                            autocompleteList.SelectedIndex = 0;
                            e.Handled = true;
                        }
                        else
                        {
                            autocompleteList.SelectionMode = ListViewSelectionMode.Single;

                            var index = e.Key == VirtualKey.Left ? -1 : 1;
                            var next = autocompleteList.SelectedIndex + index;
                            if (next >= 0 && next < autocomplete.Count)
                            {
                                autocompleteList.SelectedIndex = next;
                                autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);

                                e.Handled = true;
                            }
                        }
                    }
                }
                else if (e.Key is VirtualKey.Tab or VirtualKey.Enter && autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete != null
                    && ((autocomplete.InsertOnKeyDown is false && autocompleteList.SelectedItem != null) || autocomplete.InsertOnKeyDown))
                {
                    if (modifiers == VirtualKeyModifiers.Shift)
                    {
                        return;
                    }

                    var container = autocompleteList.ContainerFromIndex(Math.Max(0, autocompleteList.SelectedIndex)) as GridViewItem;
                    if (container != null)
                    {
                        var peer = new GridViewItemAutomationPeer(container);
                        var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        provider.Invoke();
                    }

                    Logger.Debug("Tab pressed and handled");
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Tab)
                {
                    // Ignored to allow Ctrl+Tab and Ctrl+Shift+Tab to switch chats
                    if (modifiers != VirtualKeyModifiers.None)
                    {
                        return;
                    }
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        protected override void OnAccept()
        {
            if (View != null)
            {
                View?.Accept();
            }
            else
            {
                base.OnAccept();
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            void ClearAutocomplete()
            {
                _emojiQuery = null;
                _emojiFlyout?.Hide();
                CancelEmoji();

                SetAutocomplete(null);
            }

            void SetAutocomplete(IAutocompleteCollection autocomplete)
            {
                View?.Autocomplete = autocomplete;
            }

            var selection = Document.Selection.GetClone();
            var entity = AutocompleteEntityFinder.Search(selection, out string result, out int index);

            if (entity == AutocompleteEntity.Username && viewModel is ComposeViewModel compose)
            {
                if (compose.Chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup { IsChannel: false })
                {
                    ClearAutocomplete();
                    SetAutocomplete(new ChatTextBox.UsernameCollection(viewModel.ClientService, compose.Chat.Id, compose.TopicId, result, false, true, false));
                    return;
                }
            }
            else if (entity == AutocompleteEntity.Sticker)
            {
                ShowOrUpdateEmojiFlyout(index, new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, true, result, ChatId));
                SetAutocomplete(null);
                return;
            }
            else if (entity == AutocompleteEntity.Emoji)
            {
                ShowOrUpdateEmojiFlyout(index, new ChatTextBox.EmojiCollection(ViewModel.ClientService, result, ChatId));
                SetAutocomplete(null);
                return;
            }

            ClearAutocomplete();
        }

        private Flyout _emojiFlyout;
        private string _emojiQuery;
        private CancellationTokenSource _emojiToken;

        public CancellationTokenSource CancelEmoji()
        {
            _emojiToken?.Cancel();
            _emojiToken = new();
            return _emojiToken;
        }

        private async void ShowOrUpdateEmojiFlyout(int index, IAutocompleteCollection collection)
        {
            if (_emojiQuery == collection.Query)
            {
                return;
            }

            var token = CancelEmoji();
            var source = new AutocompleteCollection(collection);

            var result = await source.LoadMoreItemsAsync(0);
            if (result.Count == 0 || token.IsCancellationRequested)
            {
                // Only reset if this is the active query
                if (token == _emojiToken)
                {
                    _emojiQuery = null;
                    _emojiFlyout?.Hide();
                }

                return;
            }

            _emojiQuery = collection.Query;

            if (_emojiFlyout?.Content is ChatTextFlyout presenter)
            {
                presenter.Update(collection);
                return;
            }

            var range = Document.GetRange(index, index);
            range.GetRect(PointOptions.None, out Rect rect, out _);

            var style = new Style
            {
                TargetType = typeof(FlyoutPresenter),
                BasedOn = BootStrapper.Current.Resources["CommandFlyoutPresenterStyle"] as Style
            };

            style.Setters.Add(new Setter(FlyoutPresenter.IsDefaultShadowEnabledProperty, false));
            style.Setters.Add(new Setter(FlyoutPresenter.MinWidthProperty, 40));

            _emojiFlyout = new Flyout
            {
                Content = new ChatTextFlyout(this, source),
                AllowFocusOnInteraction = false,
                ShouldConstrainToRootBounds = false,
                FlyoutPresenterStyle = style,
            };

            _emojiFlyout.Opened += EmojiFlyout_Opened;
            _emojiFlyout.Closed += EmojiFlyout_Closed;

            _emojiFlyout.ShowAt(this, new FlyoutShowOptions
            {
                Position = new Windows.Foundation.Point(rect.X + Padding.Left - 8, rect.Y + 6),
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        void EmojiFlyout_Opened(object sender, object args)
        {
            if (sender is Flyout { Content: ChatTextFlyout { Parent: FlyoutPresenter flyout } presenter })
            {
                AutomationProperties.GetControlledPeers(this).Clear();
                AutomationProperties.GetControlledPeers(this).Add(presenter.ControlledList);

                var child = VisualTreeHelper.GetChild(flyout, 0);
                if (child is UIElement element)
                {
                    element.Translation = new System.Numerics.Vector3(0, 0, 12);
                    element.Shadow = new ThemeShadow();
                }
            }
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _emojiFlyout.Opened += EmojiFlyout_Opened;
            _emojiFlyout.Closed += EmojiFlyout_Closed;

            _emojiFlyout = null;

            AutomationProperties.GetControlledPeers(this).Clear();

            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Add(_controlledList);
            }
        }
    }

    public interface IViewWithAutocomplete
    {
        IAutocompleteCollection Autocomplete { get; set; }
        void Accept();
    }
}
