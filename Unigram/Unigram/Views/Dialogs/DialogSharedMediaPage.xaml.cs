using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
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
using LinqToVisualTree;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Controls;
using Template10.Common;
using System.ComponentModel;
using Unigram.Common;
using Windows.UI.Core;
using Windows.System;
using System.Windows.Input;
using Unigram.Strings;
using Unigram.ViewModels.Dialogs;

namespace Unigram.Views.Dialogs
{
    public sealed partial class DialogSharedMediaPage : Page, IMasterDetailPage
    {
        public DialogSharedMediaViewModel ViewModel => DataContext as DialogSharedMediaViewModel;

        public DialogSharedMediaPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogSharedMediaViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;

            //ScrollingMedia.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);
            //ScrollingFiles.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);
            //ScrollingLinks.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);
            //ScrollingMusic.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);
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
            if (args.VirtualKey == VirtualKey.Escape && !args.KeyStatus.IsKeyReleased && ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedItems"))
            {
                switch (ScrollingHost.SelectedIndex)
                {
                    case 0:
                        ScrollingMedia.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 1:
                        ScrollingFiles.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 2:
                        ScrollingLinks.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 3:
                        ScrollingMusic.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                }
            }
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            Themes.Media.Download_Click(sender as FrameworkElement, null);
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            //ScrollingMedia.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingFiles.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingLinks.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingMusic.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;

            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                //InfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                //InfoPanel.Visibility = Visibility.Collapsed;
            }

            ViewModel.MessagesForwardCommand.RaiseCanExecuteChanged();
            ViewModel.MessagesDeleteCommand.RaiseCanExecuteChanged();
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ViewModel.SelectedItems = new List<TLMessageCommonBase>(((ListViewBase)sender).SelectedItems.Cast<TLMessageCommonBase>());
            }
        }

        private bool ConvertSelectionMode(ListViewSelectionMode mode)
        {
            List_SelectionModeChanged(null, null);
            return mode == ListViewSelectionMode.None ? false : true;
        }

        #region Context menu

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var messageCommon = element.DataContext as TLMessageCommonBase;
            var channel = messageCommon.Parent as TLChannel;

            CreateFlyoutItem(ref flyout, MessageView_Loaded, ViewModel.MessageViewCommand, messageCommon, Strings.Android.ShowInChat);
            CreateFlyoutItem(ref flyout, MessageDelete_Loaded, ViewModel.MessageDeleteCommand, messageCommon, Strings.Android.Delete);
            CreateFlyoutItem(ref flyout, MessageForward_Loaded, ViewModel.MessageForwardCommand, messageCommon, Strings.Android.Forward);
            CreateFlyoutItem(ref flyout, MessageSelect_Loaded, ViewModel.MessageSelectCommand, messageCommon, Strings.Resources.Select);
            CreateFlyoutItem(ref flyout, MessageSave_Loaded, ViewModel.MessageSaveCommand, messageCommon, Strings.Resources.SaveAs);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<TLMessageCommonBase, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as TLMessageCommonBase);
            if (value == Visibility.Visible)
            {
                var flyoutItem = new MenuFlyoutItem();
                //flyoutItem.Loaded += (s, args) => flyoutItem.Visibility = visibility(parameter as TLMessageCommonBase);
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                flyout.Items.Add(flyoutItem);
            }
        }

        private Visibility MessageView_Loaded(TLMessageCommonBase messageCommon)
        {
            return Visibility.Visible;
        }

        private Visibility MessageSave_Loaded(TLMessageCommonBase messageCommon)
        {
            return Visibility.Visible;
        }

        private Visibility MessageDelete_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon.Parent is TLChannel channel)
            {
                if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                {
                    return Visibility.Collapsed;
                }

                if (!messageCommon.IsOut && !channel.IsCreator && !channel.HasAdminRights || (channel.AdminRights != null && !channel.AdminRights.IsDeleteMessages))
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        private Visibility MessageForward_Loaded(TLMessageCommonBase messageCommon)
        {
            return Visibility.Visible;
        }

        private Visibility MessageSelect_Loaded(TLMessageCommonBase messageCommon)
        {
            return ViewModel.SelectionMode == ListViewSelectionMode.None ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
