//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Views;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Profile
{
    public sealed partial class SearchPostsTabPage : ProfileTabPage
    {
        public new SearchPostsViewModel ViewModel => DataContext as SearchPostsViewModel;

        private DispatcherTimer _nextFreeQueryTimer;

        public SearchPostsTabPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            //ScrollingHost.Style = BootStrapper.Current.Resources["DefaultListViewStyle"] as Style;
            //ScrollingHost.Padding = new Thickness(0);
            //ScrollingHost.ItemContainerCornerRadius = new CornerRadius(0);

            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateState(ViewModel.State);
            UpdateQueryString(ViewModel.QueryString);
            UpdateLimits(ViewModel.Limits);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Limits))
            {
                UpdateLimits(ViewModel.Limits);
            }
            else if (e.PropertyName == nameof(ViewModel.QueryString))
            {
                UpdateQueryString(ViewModel.QueryString);
            }
            else if (e.PropertyName == nameof(ViewModel.State))
            {
                UpdateState(ViewModel.State);
            }
        }

        private void UpdateState(SearchPostsState state)
        {
            InitialState.Visibility = state == SearchPostsState.Empty
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (state == SearchPostsState.NotFound)
            {
                FindName(nameof(NotFoundState));
            }
            else
            {
                UnloadObject(NotFoundState);
            }

            if (state == SearchPostsState.Loading)
            {
                FindName(nameof(LoadingState));
            }
            else
            {
                UnloadObject(LoadingState);
            }
        }

        private void UpdateQueryString(string query)
        {
            if (ViewModel.IsPremium)
            {
                ActionButton.Visibility = string.IsNullOrWhiteSpace(query)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            else
            {
                ActionButton.Visibility = Visibility.Visible;
            }
        }

        private void UpdateLimits(PublicPostSearchLimits limits)
        {
            if (limits == null)
            {
                return;
            }

            if (ViewModel.IsPremium)
            {
                if (limits.RemainingFreeQueryCount > 0 || limits.IsCurrentQueryFree)
                {
                    Title.Text = Strings.SearchPostsTitle;
                    Subtitle.Text = Strings.SearchPostsText;

                    SearchInfo.Text = Locale.Declension(Strings.R.SearchPostsFreeSearches, limits.RemainingFreeQueryCount);

                    SearchButton.Opacity = 1;
                    PaidButton.Opacity = 0;
                }
                else
                {
                    Title.Text = Strings.SearchPostsLimitReached;
                    Subtitle.Text = Locale.Declension(Strings.R.SearchPostsLimitReachedText, limits.DailyFreeQueryCount);

                    PaidButton.Text = Locale.Declension(Strings.R.SearchPostsButtonPay, limits.StarCount).ReplaceStar(Icons.Premium);

                    SearchInfo.Text = string.Format(Strings.SearchPostsFreeSearchUnlocksIn, TimeSpan.FromSeconds(limits.NextFreeQueryIn).ToDuration());

                    SearchButton.Opacity = 0;
                    PaidButton.Opacity = 1;
                }

                PremiumButton.Opacity = 0;
                PremiumInfo.Visibility = Visibility.Collapsed;

                SearchInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Title.Text = Strings.SearchPostsTitle;
                Subtitle.Text = Strings.SearchPostsText;

                PremiumButton.Opacity = 1;
                PremiumInfo.Visibility = Visibility.Visible;

                SearchButton.Opacity = 0;
                SearchInfo.Visibility = Visibility.Collapsed;

                PaidButton.Opacity = 0;
            }
        }

        private void UpdateNextFreeQueryIn(int nextFreeQueryIn)
        {
            if (_nextFreeQueryTimer == null)
            {
                _nextFreeQueryTimer = new DispatcherTimer();
                _nextFreeQueryTimer.Interval = TimeSpan.FromSeconds(1);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || ViewModel == null)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatCell cell && args.Item is Message message)
            {
                cell.UpdateMessage(ViewModel.ClientService, message, false);
                args.Handled = true;
            }
        }

        private void EmptyState_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.Text = string.Format(Strings.SearchPostsNotFoundText, ViewModel.Query);
            }
        }

        private void LoadingState_Loaded(object sender, RoutedEventArgs e)
        {
            var size = ScrollingHost.ActualSize;
            var itemHeight = 8 + 48 + 8;

            var rows = Math.Min(10, Math.Ceiling(size.Y / itemHeight));
            var shapes = new List<CanvasGeometry>();

            var maxWidth = (int)Math.Clamp(size.X - 32 - 12 - 12 - 48 - 12, 80, 280);
            var random = new Random();

            for (int i = 0; i < rows; i++)
            {
                var y = itemHeight * i;

                shapes.Add(CanvasGeometry.CreateEllipse(null, 12 + 24, y + 8 + 24, 24, 24));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 48 + 8, y + 12, random.Next(80, maxWidth), 18, 4, 4));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 48 + 8, y + 12 + 22, random.Next(80, maxWidth), 14, 4, 4));
            }

            var compositor = BootStrapper.Current.Compositor;

            var geometries = shapes.ToArray();
            var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

            var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
            var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var lookup = ThemeService.GetLookup(ActualTheme);
            if (lookup.TryGet("MenuFlyoutItemBackgroundPointerOver", out Color color))
            {
                foregroundColor = color;
                backgroundColor = color;
            }

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;
            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = compositor.CreateGeometricClip(path);
            var visual = compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(LoadingState, visual);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var view = this.GetParent<SearchChatsView>();
            view?.RaiseItemClick(e);
        }
    }
}
