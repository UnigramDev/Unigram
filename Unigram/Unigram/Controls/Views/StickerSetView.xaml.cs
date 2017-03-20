using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
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
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class StickerSetView : ContentDialogBase
    {
        public StickerSetViewModel ViewModel => DataContext as StickerSetViewModel;

        public StickerSetView()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<StickerSetViewModel>();

            //Loaded += async (s, args) =>
            //{
            //    await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            //};
        }

        private static StickerSetView _current;
        public static StickerSetView Current
        {
            get
            {
                if (_current == null)
                    _current = new StickerSetView();

                return _current;
            }
        }

        public ItemClickEventHandler ItemClick { get; set; }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLInputStickerSetBase parameter)
        {
            return ShowAsync(parameter, null);
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLInputStickerSetBase parameter, ItemClickEventHandler callback)
        {
            ViewModel.StickerSet = new TLStickerSet();
            ViewModel.Items[0].Key = new TLStickerSet();
            ViewModel.Items[0].Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                ItemClick = callback;
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return ShowAsync();
        }

        private string ConvertIsInstalled(bool installed, bool archived, bool official, bool masks)
        {
            if (installed && !archived)
            {
                return official 
                    ? string.Format(masks ? "Archive {0} masks" : "Archive {0} stickers", ViewModel.StickerSet.Count)
                    : string.Format(masks ? "Remove {0} masks" : "Remove {0} stickers", ViewModel.StickerSet.Count);
            }

            return official || archived
                ? string.Format(masks ? "Show {0} masks" : "Show {0} stickers", ViewModel.StickerSet.Count)
                : string.Format(masks ? "Add {0} masks" : "Add {0} stickers", ViewModel.StickerSet.Count);
        }

        private Border LineTop;
        private Border LineAccent;

        private SpriteVisual _backgroundVisual;
        private ExpressionAnimation _expression;

        private void GridView_Loaded(object sender, RoutedEventArgs e)
        {
            LineTop = List.Descendants<Border>().FirstOrDefault(x => ((Border)x).Name.Equals("LineTop")) as Border;
            LineAccent = List.Descendants<Border>().FirstOrDefault(x => ((Border)x).Name.Equals("LineAccent")) as Border;

            var scroll = List.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                scroll.ViewChanged += Scroll_ViewChanged;
                Scroll_ViewChanged(scroll, null);

                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;

                var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroll);
                _backgroundVisual = ElementCompositionPreview.GetElementVisual(Yolo).Compositor.CreateSpriteVisual();
                _backgroundVisual.Brush = _backgroundVisual.Compositor.CreateColorBrush(brush.Color);
                _backgroundVisual.Size = new System.Numerics.Vector2((float)Yolo.ActualWidth, (float)Yolo.ActualHeight);

                ElementCompositionPreview.SetElementChildVisual(Yolo, _backgroundVisual);

                _expression = _backgroundVisual.Compositor.CreateExpressionAnimation("Max(Maximum, Scrolling.Translation.Y)");
                _expression.SetReferenceParameter("Scrolling", props);
                _expression.SetScalarParameter("Maximum", -(float)Yolo.Margin.Top + 1);
                _backgroundVisual.StartAnimation("Offset.Y", _expression);
            }

            var panel = List.ItemsPanelRoot as ItemsWrapGrid;
            if (panel != null)
            {
                panel.SizeChanged += (s, args) =>
                {
                    Scroll_ViewChanged(scroll, null);
                };
            }
        }

        private void Scroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scroll = sender as ScrollViewer;
            var top = 1;
            var bottom = 1;

            if (scroll.VerticalOffset <= Header.Margin.Top)
            {
                top = 0;
            }
            if (scroll.VerticalOffset == scroll.ScrollableHeight)
            {
                bottom = 0;
            }

            //if (LineTop.BorderThickness.Bottom != top)
            //{
            //    if (top == 0)
            //    {
            //        MaskTitleAndStatusBar();
            //    }
            //    else
            //    {
            //        SetupTitleAndStatusBar();
            //    }
            //}

            LineTop.BorderThickness = new Thickness(0, 0, 0, top);
            LineAccent.BorderThickness = new Thickness(0, top > 0 ? 0 : 1, 0, 0);
            LineBottom.BorderThickness = new Thickness(0, bottom, 0, 0);
        }

        // SystemControlBackgroundChromeMediumLowBrush

        private void SetupTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var titleBrush = Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
            var overlayBrush = OverlayBrush as SolidColorBrush;

            if (overlayBrush != null)
            {
                titlebar.BackgroundColor = titleBrush.Color;
                titlebar.ButtonBackgroundColor = titleBrush.Color;

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var statusBar = StatusBar.GetForCurrentView();
                    statusBar.BackgroundColor = titleBrush.Color;
                    statusBar.ForegroundColor = Colors.Black;
                }
            }
        }

        private void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var itemWidth = (e.NewSize.Width - 24) / 5d;
            var minHeigth = itemWidth * 3d - 12 + 48;
            var top = Math.Max(0, e.NewSize.Height - minHeigth);

            Header.Margin = new Thickness(0, top, 0, 0);

            Yolo.Height = e.NewSize.Height;
            Yolo.Margin = new Thickness(0, top, 0, -top);

            if (_backgroundVisual != null && _expression != null)
            {
                _backgroundVisual.Size = new System.Numerics.Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                _expression.SetScalarParameter("Maximum", -(float)top + 1);
                _backgroundVisual.StopAnimation("Offset.Y");
                _backgroundVisual.StartAnimation("Offset.Y", _expression);
            }
        }

        //protected override void UpdateView(Rect bounds)
        //{
        //    if (BackgroundElement == null) return;

        //    BackgroundElement.MinHeight = bounds.Height;
        //    BackgroundElement.BorderThickness = new Thickness(0);
        //}

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.None);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClick != null)
            {
                ItemClick.Invoke(this, e);
                Hide(ContentDialogBaseResult.OK);
            }
        }
    }
}
