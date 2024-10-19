using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Navigation;
using Telegram.Services;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Host
{
    // TODO: Inheriting from TabViewItem causes TabView to throw E_INVALIDARG in release.
    //public partial class TabbedPageItem : TabViewItem
    //{
    //    private bool _isBackButtonVisible;
    //    public bool IsBackButtonVisible
    //    {
    //        get => _isBackButtonVisible;
    //        set
    //        {
    //            if (_isBackButtonVisible != value)
    //            {
    //                _isBackButtonVisible = value;
    //                IsBackButtonVisibleChanged?.Invoke(this, EventArgs.Empty);
    //            }
    //        }
    //    }

    //    public event EventHandler IsBackButtonVisibleChanged;
    //}

    public sealed partial class TabbedPage : UserControl, IPopupHost, IToastHost
    {
        public TabbedPage(TabViewItem newTab, bool forWebApps)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            Window.Current.SetTitleBar(Footer);
            BackdropMaterial.SetApplyToRootOrPageBackground(this, true);

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            if (forWebApps)
            {
                //Navigation.IsAddTabButtonVisible = true;
                //Navigation.AddTabButtonClick += Navigation_AddTabButtonClick;
                Navigation.TabWidthMode = TabViewWidthMode.Compact;

                Navigation.SelectionChanged += Navigation_SelectionChanged;

                Footer.Width = 46;
                MenuButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Visible;
            }
            else
            {
                MenuButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;

                coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            }

            if (newTab != null)
            {
                Navigation.TabItems.Add(newTab);

                //newTab.IsClosable = false;
                //newTab.IsBackButtonVisibleChanged += OnIsBackButtonVisibleChanged;
            }
        }

        public void ToastOpened(TeachingTip toast)
        {
            Resources.Remove("TeachingTip");
            Resources.Add("TeachingTip", toast);
        }

        public void ToastClosed(TeachingTip toast)
        {
            if (Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    Resources.Remove("TeachingTip");
                }
            }
        }

        public void PopupOpened()
        {
            Window.Current.SetTitleBar(null);
        }

        public void PopupClosed()
        {
            Window.Current.SetTitleBar(Footer);
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (e.RemovedItems?.Count > 0 && e.RemovedItems[0] is TabbedPageItem removedItem)
            //{
            //    removedItem.IsClosable = false;
            //    removedItem.IsBackButtonVisibleChanged -= OnIsBackButtonVisibleChanged;
            //}

            //if (e.AddedItems?.Count > 0 && e.AddedItems[0] is TabbedPageItem addedItem)
            //{
            //    addedItem.IsClosable = false;
            //    addedItem.IsBackButtonVisibleChanged += OnIsBackButtonVisibleChanged;

            //    OnIsBackButtonVisibleChanged(addedItem, null);
            //}
        }

        private bool _backButtonCollapsed = true;

        private void OnIsBackButtonVisibleChanged(object sender, EventArgs e)
        {
            //if (sender is not TabbedPageItem item)
            //{
            //    return;
            //}

            //MenuButton.IsChecked = item.IsBackButtonVisible;

            //var show = item.IsBackButtonVisible;
            //if (show != _backButtonCollapsed)
            //{
            //    return;
            //}

            //_backButtonCollapsed = !show;
            //BackButton.Visibility = Visibility.Visible;

            //var tabContainerGrid = Navigation.GetChild<Grid>(x => x.Name == "TabContainerGrid");
            //if (tabContainerGrid == null)
            //{
            //    return;
            //}

            //ElementCompositionPreview.SetIsTranslationEnabled(tabContainerGrid, true);

            //var visual1 = ElementComposition.GetElementVisual(BackButton);
            //var visual2 = ElementComposition.GetElementVisual(tabContainerGrid);

            //var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            //batch.Completed += (s, args) =>
            //{
            //    visual2.Properties.InsertVector3("Translation", Vector3.Zero);
            //    BackButton.Visibility = show
            //        ? Visibility.Visible
            //        : Visibility.Collapsed;
            //};

            //var offset = visual1.Compositor.CreateScalarKeyFrameAnimation();
            //offset.InsertKeyFrame(0, show ? -40 : 0);
            //offset.InsertKeyFrame(1, show ? 0 : -40);
            //offset.Duration = Constants.FastAnimation;

            //var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            //scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            //scale.Duration = Constants.FastAnimation;

            //var opacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            //opacity.InsertKeyFrame(show ? 0 : 1, 0);
            //opacity.InsertKeyFrame(show ? 1 : 0, 1);

            //visual1.CenterPoint = new Vector3(24);

            //visual2.StartAnimation("Translation.X", offset);
            //visual1.StartAnimation("Scale", scale);
            //visual1.StartAnimation("Opacity", opacity);
            //batch.End();
        }

        private void Navigation_AddTabButtonClick(TabView sender, object args)
        {
            throw new System.NotImplementedException();
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
