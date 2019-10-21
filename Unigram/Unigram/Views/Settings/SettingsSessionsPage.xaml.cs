using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Unigram.Controls.Cells;
using Telegram.Td.Api;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsSessionsPage : Page
    {
        public SettingsSessionsViewModel ViewModel => DataContext as SettingsSessionsViewModel;

        public SettingsSessionsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsSessionsViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.TerminateCommand.Execute(e.ClickedItem);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is SessionCell cell)
            {
                cell.UpdateSession(args.Item as Session);
            }

            // Table layout
            var first = false;
            var last = false;

            if (args.Item is Session session)
            {
                var list = session.IsPasswordPending ? ViewModel.Items.FirstOrDefault() : ViewModel.Items.LastOrDefault();
                if (list == null)
                {
                    return;
                }

                var index = list.IndexOf(session);
                first = index == 0;
                last = index == list.Count - 1;
            }

            var presenter = VisualTreeHelper.GetChild(args.ItemContainer, 0) as ListViewItemPresenter;
            if (presenter != null)
            {
                presenter.CornerRadius = new CornerRadius(first ? 8 : 0, first ? 8 : 0, last ? 8 : 0, last ? 8 : 0);
            }
        }
    }
}
