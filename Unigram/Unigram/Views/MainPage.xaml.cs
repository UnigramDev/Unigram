using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Services.SerializationService;
using Unigram.Controls;
using Unigram.Core.Dependency;
using Unigram.Core.Notifications;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        private object _lastSelected;

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;

            DataContext = UnigramContainer.Instance.ResolverType<MainViewModel>();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnStateChanged(null, null);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main");
                ViewModel.NavigationService = MasterDetail.NavigationService;
            }

            if (e.Parameter is string)
            {
                var parameter = SerializationService.Json.Deserialize((string)e.Parameter) as string;
                if (parameter != null)
                {
                    var data = Toast.SplitArguments((string)parameter);
                    if (data.ContainsKey("from_id"))
                    {
                        var user = ViewModel.CacheService.GetUser(int.Parse(data["from_id"]));
                        if (user == null)
                        {
                            // TODO: ViewModel.ProtoService.GetUsersAsync
                            var users = await ViewModel.ProtoService.GetUsersAsync(new TLVector<TLInputUserBase>(new[] { new TLInputUser { UserId = int.Parse(data["from_id"]) } }));
                            if (users.IsSucceeded)
                            {
                                user = users.Value[0];
                            }
                        }

                        if (user != null)
                        {
                            ViewModel.NavigationService.Navigate(typeof(UserInfoPage), user);
                        }
                    }
                }
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                lvMasterChats.IsItemClickEnabled = true;
                lvMasterChats.SelectionMode = ListViewSelectionMode.None;
                lvMasterChats.SelectedItem = null;
            }
            else
            {
                lvMasterChats.IsItemClickEnabled = false;
                lvMasterChats.SelectionMode = ListViewSelectionMode.Single;
                lvMasterChats.SelectedItem = _lastSelected;
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            _lastSelected = e.ClickedItem;

            var dialog = e.ClickedItem as TLDialog;
            if (dialog.With is TLUserBase)
            {
                ViewModel.NavigationService.Navigate(typeof(UserInfoPage), dialog.With);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvMasterChats.SelectedItem != null && _lastSelected != lvMasterChats.SelectedItem)
            {
                _lastSelected = lvMasterChats.SelectedItem;

                var dialog = lvMasterChats.SelectedItem as TLDialog;
                if (dialog.With is TLUserBase)
                {
                    ViewModel.NavigationService.Navigate(typeof(UserInfoPage), dialog.With);
                }
            }
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            //PLEASE REMOVE THE BELOW LINE ONCE THE CHATPAGE HAS BEEN IMPLEMENTED
            ViewModel.NavigationService.Navigate(typeof(DialogSharedMediaPage));
        }
    }

    // TEST
    public class PivotItemsPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var items = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.Visibility == Visibility.Visible)
                {
                    items += 1;
                }
            }

            var width = (availableSize.Width + 48) / items;
            var height = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var childWidth = width;
                var child = Children[i];
                if (child.Visibility == Visibility.Visible)
                {
                    //if (i == items + 1)
                    //{
                    //    childWidth -= 48; // Ellipse button
                    //}

                    child.Measure(new Size(childWidth, availableSize.Height));
                    height = Math.Max(height, child.DesiredSize.Height);
                }
            }

            return new Size(availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var items = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.Visibility == Visibility.Visible)
                {
                    items += 1;
                }
            }

            var width = (finalSize.Width + 48) / items;
            var height = 0d;
            var x = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var childWidth = width;
                var child = Children[i];
                if (child.Visibility == Visibility.Visible)
                {
                    //if (i == items + 1)
                    //{
                    //    childWidth -= 48; // Ellipse button
                    //}

                    child.Arrange(new Rect(x, 0, childWidth, finalSize.Height));
                    height = Math.Max(height, child.DesiredSize.Height);
                    x += childWidth;
                }
            }

            return new Size(finalSize.Width, height);
        }
    }
}
