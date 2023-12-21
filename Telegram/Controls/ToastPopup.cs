using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public class ToastPopup
    {
        public static TeachingTip Show(PremiumFeature source)
        {
            var text = source switch
            {
                PremiumFeatureVoiceRecognition => Strings.PrivacyVoiceMessagesPremiumOnly.Replace(" *Telegram Premium* ", " **Telegram Premium** "),
                PremiumFeatureAccentColor => Strings.UserColorApplyPremium,
                PremiumFeatureRealTimeChatTranslation => Strings.ShowTranslateChatButtonLocked,
                _ => Strings.UnlockPremium
            };

            return Show(Extensions.ReplacePremiumLink(text), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
        }

        public static TeachingTip Show(string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(null, text, null, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(string text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(null, ClientEx.ParseMarkdown(text), icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FormattedText text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(null, text, null, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FormattedText text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(null, text, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FrameworkElement target, string text, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, null, placement, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FrameworkElement target, string text, AnimatedImageSource icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, ClientEx.ParseMarkdown(text), icon, placement, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FrameworkElement target, FormattedText text, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, null, placement, requestedTheme, dismissAfter);
        }

        public static TeachingTip Show(FrameworkElement target, FormattedText text, AnimatedImageSource icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = icon,
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowToastImpl(Window.Current, target, text, animated, placement, requestedTheme, dismissAfter);
        }

        public static TeachingTip ShowToastImpl(Window app, FrameworkElement target, FormattedText text, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };

            TextBlockHelper.SetFormattedText(label, text);
            Grid.SetColumn(label, 1);

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            content.Children.Add(label);

            if (icon != null)
            {
                content.Children.Add(icon);
            }

            var toast = new TeachingTip
            {
                Target = target,
                PreferredPlacement = placement,
                IsLightDismissEnabled = target != null && (dismissAfter == null || dismissAfter == TimeSpan.Zero),
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                MinWidth = 0,
            };

            if (requestedTheme != ElementTheme.Default)
            {
                toast.RequestedTheme = requestedTheme;
            }

            if (app.Content is IToastHost host)
            {
                void handler(object sender, object e)
                {
                    host.Disconnect(toast);
                    toast.Closed -= handler;
                }

                host.Connect(toast);
                toast.Closed += handler;
            }

            if (target == null || dismissAfter.HasValue)
            {
                var timer = new DispatcherTimer();
                timer.Interval = dismissAfter ?? TimeSpan.FromSeconds(3);

                void handler(object sender, object e)
                {
                    timer.Tick -= handler;
                    toast.IsOpen = false;
                }

                timer.Tick += handler;
                timer.Start();
            }

            toast.IsOpen = true;
            return toast;
        }


        public static Task<ContentDialogResult> ShowActionAsync(string text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(null, ClientEx.GetMarkdownText(text), action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(FormattedText text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(null, text, action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(FrameworkElement target, FormattedText text, string action, AnimatedImageSource icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = icon,
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowActionAsync(target, text, action, animated, placement, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(string text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(null, ClientEx.GetMarkdownText(text), action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(FormattedText text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(null, text, action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(FrameworkElement target, FormattedText text, string action, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            var toast = ShowToastImpl(Window.Current, target, text, icon, placement, requestedTheme, dismissAfter);
            if (toast.Content is Grid content)
            {
                var tsc = new TaskCompletionSource<ContentDialogResult>();
                var undo = new Button()
                {
                    Content = action,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = BootStrapper.Current.Resources["AccentTextButtonStyle"] as Style,
                    Margin = new Thickness(8, -4, -4, -4),
                    Padding = new Thickness(4, 5, 4, 6)
                };

                void handler(object sender, RoutedEventArgs e)
                {
                    tsc.TrySetResult(ContentDialogResult.Primary);
                    undo.Click -= handler;

                    toast.IsOpen = false;
                }

                void closed(TeachingTip sender, TeachingTipClosedEventArgs e)
                {
                    tsc.TrySetResult(ContentDialogResult.None);
                    sender.Closed -= closed;
                }

                undo.Click += handler;
                toast.Closed += closed;

                Grid.SetColumn(undo, 2);
                content.Children.Add(undo);

                return tsc.Task;
            }

            return Task.FromResult(ContentDialogResult.None);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(string text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(null, ClientEx.GetMarkdownText(text), action, dismissAfter, TeachingTipPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(FormattedText text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(null, text, action, dismissAfter, TeachingTipPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(FrameworkElement target, FormattedText text, string action, TimeSpan dismissAfter, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            var animated = new Grid
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(-4, -12, 8, -12)
            };

            var slice = new SelfDestructTimer
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Colors.White),
                Center = 16,
                Radius = 14.5
            };

            var total = (int)dismissAfter.TotalSeconds;

            slice.Maximum = total;
            slice.Value = DateTime.Now.Add(dismissAfter);

            var value = new AnimatedTextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 3),
                Text = total.ToString()
            };

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };

            void handler(object sender, object e)
            {
                total--;

                if (total == 0)
                {
                    timer.Tick -= handler;
                    timer.Stop();
                }
                else
                {
                    value.Text = total.ToString();
                }
            }

            timer.Tick += handler;
            timer.Start();

            animated.Children.Add(slice);
            animated.Children.Add(value);

            return ShowActionAsync(target, text, action, animated, placement, requestedTheme, dismissAfter);
        }
    }
}
