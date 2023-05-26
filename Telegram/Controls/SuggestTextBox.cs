using System;
using Telegram.Common;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public class SuggestTextBox : TextBox
    {
        public SuggestTextBox()
        {
            DefaultStyleKey = typeof(SuggestTextBox);
            TextChanged += OnTextChanged;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ControlledList != null)
            {
                ControlledList.ChoosingItemContainer -= OnChoosingItemContainer;
                ControlledList.ChoosingItemContainer += OnChoosingItemContainer;
            }
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Down && ControlledList != null)
            {
                if (ControlledList.SelectedIndex < ControlledList.Items.Count - 1)
                {
                    ControlledList.SelectedIndex = Math.Max(ControlledList.SelectedIndex + 1, StartingIndex);
                    ControlledList.ScrollIntoView(ControlledList.SelectedItem);
                }

                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Up && ControlledList != null)
            {
                if (ControlledList.SelectedIndex > StartingIndex)
                {
                    ControlledList.SelectedIndex--;
                    ControlledList.ScrollIntoView(ControlledList.SelectedItem);
                }
                else
                {
                    ControlledList.SelectedIndex = -1;
                    ControlledList.ScrollToTop();
                }

                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter && ControlledList != null)
            {
                var index = Math.Max(ControlledList.SelectedIndex, StartingIndex);
                if (index < ControlledList.Items.Count)
                {
                    var container = ControlledList.ContainerFromIndex(index) as ListViewItem;
                    if (container != null)
                    {
                        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(container);
                        var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        provider?.Invoke();
                    }
                }

                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }

        #region ControlledList

        public ListViewBase ControlledList
        {
            get { return (ListViewBase)GetValue(ControlledListProperty); }
            set { SetValue(ControlledListProperty, value); }
        }

        public static readonly DependencyProperty ControlledListProperty =
            DependencyProperty.Register("ControlledList", typeof(ListViewBase), typeof(SuggestTextBox), new PropertyMetadata(null, OnControlledListChanged));

        private static void OnControlledListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SuggestTextBox)d).OnControlledListChanged((ListViewBase)e.NewValue, (ListViewBase)e.OldValue);
        }

        private void OnControlledListChanged(ListViewBase newValue, ListViewBase oldValue)
        {
            if (oldValue != null)
            {
                oldValue.ChoosingItemContainer -= OnChoosingItemContainer;
                AutomationProperties.GetControlledPeers(this).Remove(oldValue);
            }

            if (newValue != null)
            {
                newValue.ChoosingItemContainer += OnChoosingItemContainer;
                AutomationProperties.GetControlledPeers(this).Add(newValue);
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (sender.Items.Count > StartingIndex)
            {
                sender.SelectedIndex = StartingIndex;
                sender.ChoosingItemContainer -= OnChoosingItemContainer;
            }
        }

        #endregion

        #region StartingIndex

        public int StartingIndex
        {
            get { return (int)GetValue(StartingIndexProperty); }
            set { SetValue(StartingIndexProperty, value); }
        }

        public static readonly DependencyProperty StartingIndexProperty =
            DependencyProperty.Register("StartingIndex", typeof(int), typeof(SuggestTextBox), new PropertyMetadata(0));

        #endregion
    }
}
