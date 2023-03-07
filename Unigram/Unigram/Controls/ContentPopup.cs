//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Views.Host;
using Windows.UI.ViewManagement;

namespace Unigram.Controls
{
    public class ContentPopup : ContentDialog
    {
        private ContentDialogResult _result;

        private Border BackgroundElement;

        public ContentPopup()
        {
            DefaultStyleKey = typeof(ContentPopup);
            DefaultButton = ContentDialogButton.Primary;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            Opened += OnOpened;
            Closed += OnClosed;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (XamlRoot.Content is RootPage root)
            {
                root.PopupOpened();
            }
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (XamlRoot.Content is RootPage root)
            {
                root.PopupClosed();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
#warning TODO
            //ApplicationView.GetForCurrentView().VisibleBoundsChanged += ApplicationView_VisibleBoundsChanged;

            CharacterReceived += OnCharacterReceived;

            //ApplicationView_VisibleBoundsChanged(ApplicationView.GetForCurrentView());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            //ApplicationView.GetForCurrentView().VisibleBoundsChanged -= ApplicationView_VisibleBoundsChanged;

            CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (args.Character != '\r' || DefaultButton == ContentDialogButton.Primary)
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                Hide(ContentDialogResult.Primary);
                args.Handled = true;
            }
        }

        private void ApplicationView_VisibleBoundsChanged(ApplicationView sender, object args = null)
        {
            if (Content is FrameworkElement && !IsFullWindow)
            {
                if (VerticalContentAlignment == VerticalAlignment.Center)
                {
                    BackgroundElement.MaxHeight = Math.Min(sender.VisibleBounds.Height - 40 - 40, 640);
                }
                else
                {
                    BackgroundElement.MaxHeight = Math.Min(sender.VisibleBounds.Height - 40 - 40, ContentMaxHeight);
                }
            }
        }

        public bool IsFullWindow { get; set; } = false;

        public bool FocusPrimaryButton { get; set; } = true;
        public bool IsLightDismissEnabled { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;

            VisualStateManager.GoToState(this, IsPrimaryButtonSplit ? "PrimaryAsSplitButton" : "NoSplitButton", false);

            var button = GetTemplateChild("PrimaryButton") as Button;
            if (button != null && FocusPrimaryButton)
            {
                button.Loaded += PrimaryButton_Loaded;
            }

            var rectangle = GetTemplateChild("LightDismiss") as Rectangle;
            if (rectangle == null)
            {
                return;
            }

            //if (ActualTheme == ElementTheme.Dark)
            //{
            //    rectangle.Fill = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00));
            //}
            //else
            //{
            //    rectangle.Fill = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
            //}

            rectangle.PointerReleased += Rectangle_PointerReleased;
        }

        private void PrimaryButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && FocusPrimaryButton)
            {
                button.Focus(FocusState.Keyboard);
            }
        }

        private void Rectangle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased && IsLightDismissEnabled)
            {
                Hide();
            }
        }

        public async Task<ContentDialogResult> OpenAsync(XamlRoot xamlRoot)
        {
            await this.ShowQueuedAsync(xamlRoot);
            return _result;
        }

        protected void SetResult(ContentDialogResult result)
        {
            _result = result;
        }

        public void Hide(ContentDialogResult result)
        {
            _result = result;

            if (result == ContentDialogResult.Primary)
            {
                var button = GetTemplateChild("PrimaryButton") as Button;
                if (button != null)
                {
                    var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                    if (invoke != null)
                    {
                        invoke.Invoke();
                        return;
                    }
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var button = GetTemplateChild("SecondaryButton") as Button;
                if (button != null)
                {
                    var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                    if (invoke != null)
                    {
                        invoke.Invoke();
                        return;
                    }
                }
            }

            Hide();
        }

        #region IsPrimaryButtonSplit

        public bool IsPrimaryButtonSplit
        {
            get => (bool)GetValue(IsPrimaryButtonSplitProperty);
            set => SetValue(IsPrimaryButtonSplitProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonSplitProperty =
            DependencyProperty.Register("IsPrimaryButtonSplit", typeof(bool), typeof(ContentPopup), new PropertyMetadata(false));

        #endregion

        #region SecondaryBackground

        public Brush SecondaryBackground
        {
            get { return (Brush)GetValue(SecondaryBackgroundProperty); }
            set { SetValue(SecondaryBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SecondaryBackgroundProperty =
            DependencyProperty.Register("SecondaryBackground", typeof(Brush), typeof(ContentPopup), new PropertyMetadata(null));

        #endregion

        #region ContentMaxHeight

        public double ContentMaxHeight
        {
            get { return (double)GetValue(ContentMaxHeightProperty); }
            set { SetValue(ContentMaxHeightProperty, value); }
        }

        public static readonly DependencyProperty ContentMaxHeightProperty =
            DependencyProperty.Register("ContentMaxHeight", typeof(double), typeof(ContentPopup), new PropertyMetadata(double.PositiveInfinity));

        #endregion
    }
}
