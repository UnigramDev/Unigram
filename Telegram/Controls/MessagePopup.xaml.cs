//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public sealed partial class MessagePopup : ContentPopup
    {
        public MessagePopup()
        {
            InitializeComponent();
        }

        public MessagePopup(string message)
            : this(message, null)
        {

        }

        public MessagePopup(string message, string title)
        {
            InitializeComponent();

            Message = message;
            Title = title;
            PrimaryButtonText = "OK";
        }

        public string Message
        {
            get => TextBlockHelper.GetMarkdown(MessageLabel);
            set => TextBlockHelper.SetMarkdown(MessageLabel, value);
        }

        public FormattedText FormattedMessage
        {
            get => TextBlockHelper.GetFormattedText(MessageLabel);
            set => TextBlockHelper.SetFormattedText(MessageLabel, value);
        }

        public object CheckBoxLabel
        {
            get => CheckBox.Content.ToString();
            set
            {
                CheckBox.Content = value;
                CheckBox.Visibility = (value is string str ? string.IsNullOrWhiteSpace(str) : value == null) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool? IsChecked
        {
            get => CheckBox.IsChecked;
            set => CheckBox.IsChecked = value;
        }

        private bool _isCheckedRequired;
        public bool IsCheckedRequired
        {
            get => _isCheckedRequired;
            set
            {
                _isCheckedRequired = value;

                if (value)
                {
                    IsPrimaryButtonEnabled = CheckBox.IsChecked == true;
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsPrimaryButtonEnabled = !_isCheckedRequired || CheckBox.IsChecked is true;
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.AppName,
                Message = message,
                PrimaryButtonText = primary ?? Strings.OK,
                SecondaryButtonText = secondary ?? string.Empty,
                CloseButtonText = tertiary ?? string.Empty,
            };

            if (requestedTheme != ElementTheme.Default)
            {
                popup.RequestedTheme = requestedTheme;
            }

            if (destructive)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.AppName,
                FormattedMessage = message,
                PrimaryButtonText = primary ?? Strings.OK,
                SecondaryButtonText = secondary ?? string.Empty,
                CloseButtonText = tertiary ?? string.Empty
            };

            if (requestedTheme != ElementTheme.Default)
            {
                popup.RequestedTheme = requestedTheme;
            }

            if (destructive)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FrameworkElement target, string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            var tsc = new TaskCompletionSource<ContentDialogResult>();
            var popup = new TeachingTipEx
            {
                Title = title,
                Subtitle = message,
                ActionButtonContent = primary,
                ActionButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                CloseButtonContent = secondary,
                PreferredPlacement = target != null ? TeachingTipPlacementMode.Top : TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                Target = target,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
                // TODO:
                RequestedTheme = target?.ActualTheme ?? requestedTheme
            };

            popup.ActionButtonClick += (s, args) =>
            {
                popup.IsOpen = false;
                tsc.TrySetResult(ContentDialogResult.Primary);
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
                tsc.TrySetResult(ContentDialogResult.Secondary);
            };

            host.ToastOpened(popup);
            popup.IsOpen = true;
            return tsc.Task;
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FrameworkElement target, FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            var content = new TextBlock
            {
                Style = BootStrapper.Current.Resources["BodyTextBlockStyle"] as Style
            };

            TextBlockHelper.SetFormattedText(content, message);

            var tsc = new TaskCompletionSource<ContentDialogResult>();
            var popup = new TeachingTipEx
            {
                Title = title,
                Content = content,
                ActionButtonContent = primary,
                ActionButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                CloseButtonContent = secondary,
                PreferredPlacement = target != null ? TeachingTipPlacementMode.Top : TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                Target = target,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
                // TODO:
                RequestedTheme = target?.ActualTheme ?? requestedTheme
            };

            popup.ActionButtonClick += (s, args) =>
            {
                popup.IsOpen = false;
                tsc.TrySetResult(ContentDialogResult.Primary);
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
                tsc.TrySetResult(ContentDialogResult.Secondary);
            };

            host.ToastOpened(popup);
            popup.IsOpen = true;
            return tsc.Task;
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FrameworkElement target, string message, string title = null, FrameworkElement content = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            var tsc = new TaskCompletionSource<ContentDialogResult>();
            var popup = new TeachingTipEx
            {
                Title = title,
                Subtitle = message,
                Content = content,
                ActionButtonContent = primary,
                ActionButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                CloseButtonContent = secondary,
                PreferredPlacement = target != null ? TeachingTipPlacementMode.Top : TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                Target = target,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
                // TODO:
                RequestedTheme = target?.ActualTheme ?? requestedTheme
            };

            AutomationProperties.SetName(popup, title);

            popup.ActionButtonClick += (s, args) =>
            {
                popup.IsOpen = false;
                tsc.TrySetResult(ContentDialogResult.Primary);
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
                tsc.TrySetResult(ContentDialogResult.Secondary);
            };

            host.ToastOpened(popup);
            popup.IsOpen = true;
            return tsc.Task;
        }
    }

    public partial class TeachingTipEx : TeachingTip
    {
        public TeachingTipEx()
        {
            DefaultStyleKey = typeof(TeachingTipEx);

            RegisterPropertyChangedCallback(TitleProperty, OnTitleChanged);
        }

        private void OnTitleChanged(DependencyObject sender, DependencyProperty dp)
        {
            AutomationProperties.SetName(this, Title);
        }

        protected override void OnApplyTemplate()
        {
            // TODO: Name
            var container = GetTemplateChild("Container") as Border;

            var rootElement = container?.Child as FrameworkElement;
            if (rootElement != null)
            {
                rootElement.Loaded += Container_Loaded;
            }

            base.OnApplyTemplate();
        }

        private void Container_Loaded(object sender, RoutedEventArgs e)
        {
            //var subtitleTextBlock = GetTemplateChild("SubtitleTextBlock") as TextBlock;
            //if (subtitleTextBlock.Visibility == Visibility.Visible)
            //{
            //    subtitleTextBlock.Focus(FocusState.Keyboard);
            //}
            //else
            {
                var focusable = FocusManager.FindFirstFocusableElement(sender as DependencyObject) as Control;
                focusable?.Focus(FocusState.Programmatic);
            }
        }
    }
}
