//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views
{
    public enum HostedNavigationMode
    {
        Child,
        Root,
        RootWhenParameterless,
    }

    public record HostedPagePositionBase;

    public record HostedPageScrollViewerPosition(double VerticalOffset) : HostedPagePositionBase;

    public record HostedPageListViewPosition(object DataContext, double VerticalOffset) : HostedPagePositionBase;

    public partial class HostedPage : PageEx
    {
        #region ShowHeader

        public bool ShowHeader
        {
            get { return (bool)GetValue(HasHeaderProperty); }
            set { SetValue(HasHeaderProperty, value); }
        }

        public static readonly DependencyProperty HasHeaderProperty =
            DependencyProperty.Register("ShowHeader", typeof(bool), typeof(HostedPage), new PropertyMetadata(true));

        #endregion

        #region ShowHeaderBackground

        public bool ShowHeaderBackground
        {
            get { return (bool)GetValue(ShowHeaderBackgroundProperty); }
            set { SetValue(ShowHeaderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ShowHeaderBackgroundProperty =
            DependencyProperty.Register("ShowHeaderBackground", typeof(bool), typeof(HostedPage), new PropertyMetadata(true));

        #endregion

        #region Action

        public UIElement Action
        {
            get { return (UIElement)GetValue(ActionProperty); }
            set { SetValue(ActionProperty, value); }
        }

        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register("Action", typeof(UIElement), typeof(HostedPage), new PropertyMetadata(null));

        #endregion

        #region Title

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(HostedPage), new PropertyMetadata(null));

        public virtual string GetTitle()
        {
            return Title;
        }

        #endregion

        #region NavigationMode

        public HostedNavigationMode NavigationMode { get; set; }

        #endregion

        public virtual HostedPagePositionBase GetPosition()
        {
            var scrollingHost = FindName("ScrollingHost");
            if (scrollingHost is ScrollViewer scrollViewer)
            {
                return new HostedPageScrollViewerPosition(scrollViewer.VerticalOffset);
            }
            else if (scrollingHost is ListViewBase listView)
            {
                scrollViewer = listView.GetScrollViewer();

                if (scrollViewer != null)
                {
                    AssignDelegate(null);
                    return new HostedPageListViewPosition(DataContext, scrollViewer.VerticalOffset);
                }
            }

            return null;
        }

        public virtual void SetPosition(HostedPagePositionBase position)
        {
            if (position is HostedPageScrollViewerPosition scrollViewerPosition)
            {
                var scrollingHost = FindName("ScrollingHost") as ScrollViewer;
                if (scrollingHost != null)
                {
                    void handler(object sender, RoutedEventArgs e)
                    {
                        scrollingHost.Loaded -= handler;
                        scrollingHost.ChangeView(null, scrollViewerPosition.VerticalOffset, null, true);
                    }

                    scrollingHost.Loaded += handler;
                }
            }
            else if (position is HostedPageListViewPosition listViewPosition)
            {
                DataContext = listViewPosition.DataContext;
                AssignDelegate(this);

                var scrollingHost = FindName("ScrollingHost") as ListViewBase;
                if (scrollingHost != null)
                {
                    void handler(object sender, RoutedEventArgs e)
                    {
                        scrollingHost.Loaded -= handler;
                        scrollingHost.ChangeView(null, listViewPosition.VerticalOffset, null, true);
                    }

                    scrollingHost.Loaded += handler;
                }
            }
        }

        private void AssignDelegate(object content)
        {
            switch (DataContext)
            {
                case IDelegable<IProfileDelegate> delegable:
                    delegable.Delegate = content as IProfileDelegate;
                    break;
                case IDelegable<ISettingsDelegate> delegable:
                    delegable.Delegate = content as ISettingsDelegate;
                    break;
                case IDelegable<IUserDelegate> delegable:
                    delegable.Delegate = content as IUserDelegate;
                    break;
                case IDelegable<ISupergroupDelegate> delegable:
                    delegable.Delegate = content as ISupergroupDelegate;
                    break;
                case IDelegable<ISupergroupEditDelegate> delegable:
                    delegable.Delegate = content as ISupergroupEditDelegate;
                    break;
                case IDelegable<IBusinessRepliesDelegate> delegable:
                    delegable.Delegate = content as IBusinessRepliesDelegate;
                    break;
                case IDelegable<IBusinessChatLinksDelegate> delegable:
                    delegable.Delegate = content as IBusinessChatLinksDelegate;
                    break;
            }
        }
    }
}
