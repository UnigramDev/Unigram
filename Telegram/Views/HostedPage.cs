//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
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

    public record HostedPageScrollViewerPosition(double ScrollPosition) : HostedPagePositionBase;

    public record HostedPageListViewPosition(object DataContext, double ScrollPosition, string RelativeScrollPosition) : HostedPagePositionBase;

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
            if (scrollingHost is ScrollViewer scrollViewer && scrollViewer.VerticalOffset != 0)
            {
                return new HostedPageScrollViewerPosition(scrollViewer.VerticalOffset);
            }
            else if (scrollingHost is ListViewBase listView)
            {
                scrollViewer = listView.GetScrollViewer();

                if (scrollViewer != null && scrollViewer.VerticalOffset != 0)
                {
                    AssignDelegate(null);
                    return new HostedPageListViewPosition(DataContext, scrollViewer.VerticalOffset, GetRelativeScrollPosition(listView));
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
                        scrollingHost.ChangeView(null, scrollViewerPosition.ScrollPosition, null, true);
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
                    void handler(object sender, object e)
                    {
                        scrollingHost.Loaded -= handler;

                        if (string.IsNullOrEmpty(listViewPosition.RelativeScrollPosition))
                        {
                            scrollingHost.ChangeView(null, listViewPosition.ScrollPosition, null, true);
                        }
                        else
                        {
                            SetRelativeScrollPosition(scrollingHost, listViewPosition.ScrollPosition, listViewPosition.RelativeScrollPosition);
                        }
                    }

                    scrollingHost.Loaded += handler;
                }
            }
        }

        private string GetRelativeScrollPosition(ListViewBase listView)
        {
            string GetRelativeScrollPosition(object item)
            {
                var index = listView.Items.IndexOf(item);
                if (index != -1)
                {
                    return index.ToString();
                }

                return string.Empty;
            }

            return ListViewPersistenceHelper.GetRelativeScrollPosition(listView, GetRelativeScrollPosition);
        }

        private void SetRelativeScrollPosition(ListViewBase listView, double scrollPosition, string relativeScrollPosition)
        {
            IAsyncOperation<object> SetRelativeScrollPosition(string key)
            {
                return AsyncInfo.Run(token =>
                {
                    object item = null;
                    if (int.TryParse(key, out var index) && index < listView.Items.Count)
                    {
                        item = listView.Items[index];
                    }

                    return Task.FromResult(item);
                });
            }

            // TODO: this doesn't seem to work in FoldersPage when there are no folders available
            _ = ListViewPersistenceHelper.SetRelativeScrollPositionAsync(listView, relativeScrollPosition, SetRelativeScrollPosition);
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
