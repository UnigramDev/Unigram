using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Dialogs;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class DialogSearchTextBox : SearchTextBox
    {
        public DialogSearchViewModel View { get; set; }

        public DialogSearchTextBox()
        {
            SelectionChanged += OnSelectionChanged;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if ((args.VirtualKey == VirtualKey.Enter || args.VirtualKey == VirtualKey.Tab) && args.EventType == CoreAcceleratorKeyEventType.KeyDown && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                var key = Window.Current.CoreWindow.GetKeyState(VirtualKey.Enter);

                if (Autocomplete != null && View.Autocomplete != null)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send || args.VirtualKey == VirtualKey.Tab)
                    {
                        AcceptsReturn = false;
                        var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as ListViewItem;
                        if (container != null)
                        {
                            var peer = new ListViewItemAutomationPeer(container);
                            var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            invokeProv.Invoke();
                        }
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }

                    return;
                }

                //// If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                //if (ApplicationSettings.Current.IsSendByEnterEnabled)
                //{
                //    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                //    if (send)
                //    {
                //        AcceptsReturn = false;
                //        await SendAsync();
                //    }
                //    else
                //    {
                //        AcceptsReturn = true;
                //    }
                //}
                //else
                //{
                //    var send = key.HasFlag(CoreVirtualKeyStates.Down) && ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                //    if (send)
                //    {
                //        AcceptsReturn = false;
                //        await SendAsync();
                //    }
                //    else
                //    {
                //        AcceptsReturn = true;
                //    }
                //}
            }
        }

        public ListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                if (Autocomplete != null && View.Autocomplete != null)
                {
                    Autocomplete.SelectionMode = ListViewSelectionMode.Single;

                    var index = e.Key == VirtualKey.Up ? -1 : 1;
                    var next = Autocomplete.SelectedIndex + index;
                    if (next >= 0 && next < View.Autocomplete.Count)
                    {
                        Autocomplete.SelectedIndex = next;
                        Autocomplete.ScrollIntoView(Autocomplete.SelectedItem);
                    }

                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Enter)
            {
                SearchCommand?.Execute(Text.ToString());
            }
            else if (e.Key == VirtualKey.Back && (IsFiltering || From != null) && string.IsNullOrEmpty(Text))
            {
                IsFiltering = From != null;
                From = null;
                Text = string.Empty;

                e.Handled = true;
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var text = Text.ToString();

            if (IsFiltering)
            {
                View.Autocomplete = GetUsernames(text);
            }
            else
            {
                View.Autocomplete = null;
            }
        }

        private List<TLUser> GetUsernames(string username)
        {
            var query = LocaleHelper.GetQuery(username);
            bool IsMatch(TLUser user)
            {
                return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
            }

            var results = new List<TLUser>();

            if (View.Dialog.Full is TLChatFull chatFull && chatFull.Participants is TLChatParticipants chatParticipants)
            {
                foreach (var participant in chatParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        // Results should be upside down
                        results.Insert(0, participant.User);
                    }
                }
            }
            else if (View.Dialog.Full is TLChannelFull channelFull && channelFull.Participants is TLChannelsChannelParticipants channelParticipants)
            {
                foreach (var participant in channelParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        // Results should be upside down
                        results.Insert(0, participant.User);
                    }
                }
            }

            if (results.Count > 0)
            {
                return results;
            }

            return null;
        }

        #region From

        public TLUser From
        {
            get { return (TLUser)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(TLUser), typeof(DialogSearchTextBox), new PropertyMetadata(null, OnFromChanged));

        private static void OnFromChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DialogSearchTextBox)d).UpdateState((TLUser)e.NewValue, ((DialogSearchTextBox)d).IsFiltering);
        }

        #endregion

        #region IsFiltering

        public bool IsFiltering
        {
            get { return (bool)GetValue(IsFilteringProperty); }
            set { SetValue(IsFilteringProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSearching.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFilteringProperty =
            DependencyProperty.Register("IsFiltering", typeof(bool), typeof(DialogSearchTextBox), new PropertyMetadata(false, OnIsFilteringChanged));

        private static void OnIsFilteringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DialogSearchTextBox)d).UpdateState(((DialogSearchTextBox)d).From, (bool)e.NewValue);
        }

        #endregion

        private void UpdateState(TLUser from, bool filtering)
        {
            VisualStateManager.GoToState(this, from != null || filtering ? "FromState" : "BaseState", false);

            if (filtering)
            {
                Focus(FocusState.Keyboard);
                View.Autocomplete = GetUsernames(string.Empty);
            }
        }
    }
}
