using Microsoft.UI.Xaml.Controls;
using Telegram.Navigation;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Host
{
    public sealed partial class TabbedPage : UserControl
    {
        public TabbedPage(TabViewItem newTab)
        {
            this.InitializeComponent();

            Window.Current.SetTitleBar(Footer);
            BackdropMaterial.SetApplyToRootOrPageBackground(this, true);

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;

            if (newTab != null)
            {
                Navigation.TabItems.Add(newTab);
            }
        }

        public void AddNewTab(TabViewItem newTab)
        {
            Navigation.TabItems.Insert(Navigation.SelectedIndex + 1, newTab);
            Navigation.SelectedIndex++;
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            // To ensure that the tabs in the titlebar are not occluded by shell
            // content, we must ensure that we account for left and right overlays.
            // In LTR layouts, the right inset includes the caption buttons and the
            // drag region, which is flipped in RTL. 

            // The SystemOverlayLeftInset and SystemOverlayRightInset values are
            // in terms of physical left and right. Therefore, we need to flip
            // then when our flow direction is RTL.
            if (Navigation.FlowDirection == FlowDirection.LeftToRight)
            {
                Footer.MinWidth = sender.SystemOverlayRightInset;
                Header.MinWidth = sender.SystemOverlayLeftInset;
            }
            else
            {
                Footer.MinWidth = sender.SystemOverlayLeftInset;
                Header.MinWidth = sender.SystemOverlayRightInset;
            }

            // Ensure that the height of the custom regions are the same as the titlebar.
            Footer.Height = Header.Height = sender.Height;
        }

        private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (sender.TabItems.Count > 1)
            {
                sender.TabItems.Remove(args.Tab);
            }
            else
            {
                _ = WindowContext.Current.ConsolidateAsync();
            }
        }
    }
}
