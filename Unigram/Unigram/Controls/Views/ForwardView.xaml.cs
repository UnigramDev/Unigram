using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
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
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI;
using Template10.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Unigram.Controls.Views
{
    public sealed partial class ForwardView : ContentDialogBase
    {
        public ForwardViewModel ViewModel => DataContext as ForwardViewModel;

        private ForwardView()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ForwardViewModel>();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Bindings.Update();
        }

        private static ForwardView _current;
        public static ForwardView Current
        {
            get
            {
                if (_current == null)
                    _current = new ForwardView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLKeyboardButtonSwitchInline switchInline, TLUser bot)
        {
            ViewModel.SwitchInline = switchInline;
            ViewModel.SwitchInlineBot = bot;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(string message, bool hasUrl)
        {
            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = message;
            ViewModel.SendMessageUrl = hasUrl;

            return ShowAsync();
        }

        private new IAsyncOperation<ContentDialogBaseResult> ShowAsync()
        {
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.OnNavigatedToAsync(null, NavigationMode.New, null);
            });

            Loaded += handler;
            return base.ShowAsync();
        }



        private ScrollViewer _scrollingHost;

        private Visual _groupHeader;
        private SpriteVisual _background;
        private ExpressionAnimation _expression;
        private ExpressionAnimation _expressionHeader;
        private ExpressionAnimation _expressionClip;

        private void GridView_Loaded(object sender, RoutedEventArgs e)
        {
            var scroll = List.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                _scrollingHost = scroll;
                _scrollingHost.ChangeView(null, 0, null, true);
                scroll.ViewChanged += Scroll_ViewChanged;
                Scroll_ViewChanged(scroll, null);

                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
                var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroll);

                if (_background == null)
                {
                    _background = ElementCompositionPreview.GetElementVisual(BackgroundPanel).Compositor.CreateSpriteVisual();
                    ElementCompositionPreview.SetElementChildVisual(BackgroundPanel, _background);
                }

                _background.Brush = _background.Compositor.CreateColorBrush(brush.Color);
                _background.Size = new System.Numerics.Vector2((float)BackgroundPanel.ActualWidth, (float)BackgroundPanel.ActualHeight);
                _background.Clip = _background.Compositor.CreateInsetClip();

                _groupHeader = ElementCompositionPreview.GetElementVisual(GroupHeader);

                _expression = _expression ?? _background.Compositor.CreateExpressionAnimation("Max(Maximum, Scrolling.Translation.Y)");
                _expression.SetReferenceParameter("Scrolling", props);
                _expression.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _background.StopAnimation("Offset.Y");
                _background.StartAnimation("Offset.Y", _expression);

                _expressionHeader = _expressionHeader ?? _background.Compositor.CreateExpressionAnimation("Max(0, Maximum - Scrolling.Translation.Y)");
                _expressionHeader.SetReferenceParameter("Scrolling", props);
                _expressionHeader.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top);
                _groupHeader.StopAnimation("Offset.Y");
                _groupHeader.StartAnimation("Offset.Y", _expressionHeader);

                _expressionClip = _expressionClip ?? _background.Compositor.CreateExpressionAnimation("Min(0, Maximum - Scrolling.Translation.Y)");
                _expressionClip.SetReferenceParameter("Scrolling", props);
                _expressionClip.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _background.Clip.StopAnimation("Offset.Y");
                _background.Clip.StartAnimation("Offset.Y", _expressionClip);
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

        private void GroupHeader_Loaded(object sender, RoutedEventArgs e)
        {
            var groupHeader = sender as Grid;
            if (groupHeader != null)
            {
                if (_scrollingHost != null)
                {
                    Scroll_ViewChanged(_scrollingHost, null);
                }
            }
        }

        private void Scroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scroll = sender as ScrollViewer;
            var top = 1;
            var accent = 0;
            var bottom = 1;

            if (scroll.VerticalOffset <= BackgroundPanel.Margin.Top)
            {
                top = 0;
            }
            if (scroll.VerticalOffset < BackgroundPanel.Margin.Top)
            {
                accent = 1;
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
            LineAccent.BorderThickness = new Thickness(0, accent, 0, 0);
            LineBottom.BorderThickness = new Thickness(0, bottom, 0, 0);
        }

        // SystemControlBackgroundChromeMediumLowBrush

        private void SetupTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
            }
        }

        private void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var itemWidth = (e.NewSize.Width - 24) / 5d;
            var minHeigth = itemWidth * 3d - 12 + 48;
            var top = Math.Max(0, e.NewSize.Height - minHeigth);

            if (!IsFullScreenMode())
            {
                top = 0;
            }

            if (top == 0)
            {
                Header.Visibility = Visibility.Collapsed;
            }
            else
            {
                Header.Visibility = Visibility.Visible;
            }

            Header.Height = top;

            BackgroundPanel.Height = e.NewSize.Height;
            BackgroundPanel.Margin = new Thickness(0, top, 0, -top);

            if (_background != null && _expression != null && _expressionClip != null)
            {
                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;

                _background.Brush = _background.Compositor.CreateColorBrush(brush.Color);
                _background.Size = new System.Numerics.Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                _background.Clip = _background.Compositor.CreateInsetClip();

                _expression.SetScalarParameter("Maximum", -(float)top + 1);
                _background.StopAnimation("Offset.Y");
                _background.StartAnimation("Offset.Y", _expression);

                _expressionHeader.SetScalarParameter("Maximum", -(float)top);
                _groupHeader.StopAnimation("Offset.Y");
                _groupHeader.StartAnimation("Offset.Y", _expressionHeader);

                _expressionClip.SetScalarParameter("Maximum", -(float)top + 1);
                _background.Clip.StopAnimation("Offset.Y");
                _background.Clip.StartAnimation("Offset.Y", _expressionClip);
            }
        }

        //protected override void UpdateView(Rect bounds)
        //{
        //    if (BackgroundElement == null) return;

        //    BackgroundElement.MinHeight = bounds.Height;
        //    BackgroundElement.BorderThickness = new Thickness(0);
        //}

        private void LightDismiss_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.None);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SelectedItem = e.ClickedItem as ITLDialogWith;
            ViewModel.SendCommand.Execute();
        }
    }
}
