//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.Views.Popups;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsThemesPage : HostedPage
    {
        public SettingsThemesViewModel ViewModel => DataContext as SettingsThemesViewModel;

        public SettingsThemesPage()
        {
            InitializeComponent();
            Title = Strings.ColorThemes;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ViewModel.Window.UpdateChatTheme(ActualTheme, null, null, null, null))
            {
                ViewModel.Aggregator.Publish(new UpdateDefaultBackground(false, ViewModel.ClientService.GetDefaultBackground(false)));
                ViewModel.Aggregator.Publish(new UpdateDefaultBackground(true, ViewModel.ClientService.GetDefaultBackground(true)));
            }

            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);

            _background ??= new MessageBubbleBackgroundCoordinator(Bubbles, ViewModel.Window);
            _background.Attach(Message2);

            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                Message1.Mockup(ViewModel.ClientService, Strings.FontSizePreviewLine1, user, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.FontSizePreviewLine2, true, DateTime.Now);
            }
        }

        private MessageBubbleBackgroundCoordinator _background;

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (List.ItemFromContainer(sender) is not ThemeData data)
            {
                // A variant card: Reset puts a seeded one back, Delete removes one made with "+".
                if (ItemsControl.ItemsControlFromItemContainer(sender) is ListView variants
                    && variants.ItemFromContainer(sender) is ThemeSettingsData item
                    && ViewModel.SelectedItem is ThemeData selected)
                {
                    var menu = new MenuFlyout();

                    if (selected.CanReset(item))
                    {
                        menu.CreateFlyoutItem(ViewModel.ResetVariant, item, Strings.Reset, Icons.ArrowReset);
                    }

                    if (selected.CanDelete(item))
                    {
                        menu.CreateFlyoutItem(ViewModel.DeleteVariant, item, Strings.Delete, Icons.Delete, destructive: true);
                    }

                    if (menu.Items.Count > 0)
                    {
                        menu.ShowAt(sender, args);
                    }
                }

                return;
            }

            var theme = data.Info;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.CreateTheme, theme, Strings.CreateNewThemeMenu, Icons.Color);

            if (theme is ThemeCustomInfo custom)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ShareTheme, custom, Strings.ShareFile, Icons.Share);
                flyout.CreateFlyoutItem(ViewModel.EditTheme, custom, Strings.Edit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.DeleteTheme, custom, Strings.Delete, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is ThemeData theme && args.ItemContainer.ContentTemplateRoot is BaseThemeCell cell)
            {
                cell.Update(theme, theme.SelectedAccent);
            }
            else if (args.Item is ThemeSettings settings && args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                content.Background = new SolidColorBrush(settings.AccentColor.ToColor());

                var bubble = content.Children[0] as Border;
                if (true || settings.HasOutgoingMessageAccentColor)
                {
                    bubble.Background = new SolidColorBrush(settings.GetOutgoingMessageAccentColor());
                    bubble.Visibility = Visibility.Visible;
                }
                else
                {
                    bubble.Visibility = Visibility.Collapsed;
                }
            }

            args.Handled = true;
        }

        #endregion

        private void CreateVariant_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateVariant();
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem == e.ClickedItem)
            {
                if (e.ClickedItem is ThemeSettingsData settings && ViewModel.SelectedItem is ThemeData theme)
                {
                    // A copy: the editor changes what it is given as you go, and cancelling must leave
                    // the card as it was.
                    ViewModel.NavigationService.ShowPopup(new BackgroundPopup(ViewModel.NavigationService, true), new BackgroundParameters(theme.Type, settings.Name, ThemeSettingsStore.Copy(settings)));
                }
            }
        }
    }

    public class AccentCell : Grid, IMultipleElement
    {
        public void UpdateState(bool selected, bool animate, bool multiple)
        {
            var compositor = BootStrapper.Current.Compositor;

            if (!selected && !animate)
            {
                ElementCompositionPreview.SetElementChildVisual(this, compositor.CreateSpriteVisual());
            }

            var size = 36f;

            var outer = compositor.CreateEllipseGeometry();
            outer.Center = new Vector2(size / 2);
            outer.Radius = new Vector2(size / 2 - 4);

            var inner = compositor.CreateEllipseGeometry();
            inner.Center = new Vector2(size / 2);
            inner.Radius = new Vector2(8);

            var outerShape = compositor.CreateSpriteShape(outer);
            outerShape.StrokeBrush = compositor.CreateColorBrush(Colors.White);
            outerShape.StrokeThickness = 2;
            outerShape.IsStrokeNonScaling = true;
            outerShape.CenterPoint = new Vector2(size / 2);

            var innerShape1 = compositor.CreateSpriteShape(inner);
            innerShape1.FillBrush = compositor.CreateColorBrush(Colors.White);
            innerShape1.CenterPoint = new Vector2(size / 2);
            innerShape1.Scale = new Vector2(0.25f);

            var innerShape2 = compositor.CreateSpriteShape(inner);
            innerShape2.FillBrush = compositor.CreateColorBrush(Colors.White);
            innerShape2.CenterPoint = new Vector2(size / 2);
            innerShape2.Scale = new Vector2(0.25f);

            var innerShape3 = compositor.CreateSpriteShape(inner);
            innerShape3.FillBrush = compositor.CreateColorBrush(Colors.White);
            innerShape3.CenterPoint = new Vector2(size / 2);
            innerShape3.Scale = new Vector2(0.25f);

            var child = ElementCompositionPreview.GetElementVisual(Children[0]);
            child.CenterPoint = new Vector3(8);

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(size);
            visual.Shapes.Add(outerShape);
            visual.Shapes.Add(innerShape1);
            visual.Shapes.Add(innerShape2);
            visual.Shapes.Add(innerShape3);

            ElementCompositionPreview.SetElementChildVisual(this, visual);

            if (animate)
            {
                var duration = Constants.FastAnimation;

                var outerAnim = compositor.CreateVector2KeyFrameAnimation();
                outerAnim.InsertKeyFrame(selected ? 0 : 1, new Vector2(size / 28));
                outerAnim.InsertKeyFrame(selected ? 1 : 0, new Vector2(1));
                outerAnim.Duration = duration;

                var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(selected ? 0 : 1, Vector3.One);
                scaleAnim.InsertKeyFrame(selected ? 1 : 0, Vector3.Zero);
                scaleAnim.Duration = duration;

                var inner1Anim = compositor.CreateScalarKeyFrameAnimation();
                inner1Anim.InsertKeyFrame(selected ? 0 : 1, 0);
                inner1Anim.InsertKeyFrame(selected ? 1 : 0, -6);
                inner1Anim.Duration = duration;

                var inner3Anim = compositor.CreateScalarKeyFrameAnimation();
                inner3Anim.InsertKeyFrame(selected ? 0 : 1, 0);
                inner3Anim.InsertKeyFrame(selected ? 1 : 0, 6);
                inner3Anim.Duration = duration;

                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(selected ? 0 : 1, 0);
                fadeIn.InsertKeyFrame(selected ? 1 : 0, 1);
                fadeIn.Duration = duration;

                outerShape.StartAnimation("Scale", outerAnim);
                innerShape1.StartAnimation("Offset.X", inner1Anim);
                innerShape3.StartAnimation("Offset.X", inner3Anim);

                visual.StartAnimation("Opacity", fadeIn);
                child.StartAnimation("Scale", scaleAnim);
            }
            else
            {
                outerShape.Scale = new Vector2(selected ? 1 : size / 28);
                innerShape1.Offset = new Vector2(selected ? -6 : 0, 0);
                innerShape3.Offset = new Vector2(selected ? 6 : 0, 0);

                visual.Opacity = selected ? 1 : 0;
                child.Scale = new Vector3(selected ? 0 : 1);
            }
        }
    }
}
