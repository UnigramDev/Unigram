//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public enum ToastPopupIcon
    {
        None,
        AntiSpam,
        Archived,
        AutoNightOff,
        AutoRemoveOff,
        AutoRemoveOn,
        Ban,
        Copied,
        Error,
        ExpiredStory,
        Forward,
        Gif,
        Gift,
        Info,
        JoinRequested,
        LinkCopied,
        Mute,
        MuteFor,
        Pin,
        Premium,
        SavedMessages,
        SoundDownload,
        SoundOff,
        SoundOn,
        SpeedLimit,
        StarsSent,
        StarsTopup,
        Success,
        Transcribe,
        Translate,
        Unmute,
        Unpin,
        VideoConversion
    }

    public partial class ToastPopup : TeachingTip
    {
        public ToastPopup()
        {
            DefaultStyleKey = typeof(ToastPopup);
        }

        public static void ShowError(XamlRoot xamlRoot, Error error)
        {
            Show(xamlRoot, string.Format(Strings.UnknownErrorCode, error.Message), ToastPopupIcon.Error);
        }

        public static void ShowOptionPromo(INavigationService navigationService)
        {
            ShowPromo(navigationService, Strings.OptionPremiumRequiredMessage, Strings.OptionPremiumRequiredButton, null);
        }

        public static void ShowFeaturePromo(INavigationService navigationService, PremiumFeature feature)
        {
            var text = feature switch
            {
                PremiumFeatureAccentColor => Strings.UserColorApplyPremium,
                PremiumFeatureRealTimeChatTranslation => Strings.ShowTranslateChatButtonLocked,
                PremiumFeatureChecklists => Strings.TodoPremiumRequired,
                PremiumFeatureMessageEffects => Strings.AnimatedEffectPremium,
                PremiumFeatureUniqueReactions => Strings.UnlockPremiumEmojiReaction,
                PremiumFeatureCustomEmoji => Strings.UnlockPremiumEmojiHint,
                _ => Strings.UnlockPremium
            };

            ShowFeaturePromo(navigationService, text, feature);
        }

        public static void ShowFeaturePromo(INavigationService navigationService, string text, PremiumFeature feature)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
            };

            var markdown = ClientEx.ParseMarkdown(text);
            if (markdown.Entities.Count == 1)
            {
                var e1 = markdown.Entities[0];
                if (e1.Offset > 0)
                {
                    label.Inlines.Add(markdown.Text.Substring(0, e1.Offset));
                }

                if (e1.Type is TextEntityTypeBold)
                {
                    var hyperlink = new Hyperlink();

                    void handler(object sender, object e)
                    {
                        var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                        foreach (var popup in popups)
                        {
                            if (popup.Child is ContentDialog dialog)
                            {
                                dialog.Hide();
                            }
                        }

                        hyperlink.Click -= handler;

                        if (feature != null)
                        {
                            navigationService.ShowPromo(feature);
                        }
                        else
                        {
                            navigationService.ShowPromo();
                        }
                    }

                    hyperlink.Click += handler;
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Inlines.Add(markdown.Text.Substring(e1.Offset, e1.Length));

                    label.Inlines.Add(hyperlink);
                }

                if (e1.Offset + e1.Length < markdown.Text.Length)
                {
                    label.Inlines.Add(markdown.Text.Substring(e1.Offset + e1.Length));
                }
            }
            else
            {
                TextBlockHelper.SetFormattedText(label, markdown);
            }

            Show(navigationService.XamlRoot, label, ToastPopupIcon.Premium);
        }

        public static async void ShowPromo(INavigationService navigationService, string text, string action, PremiumSource source, PremiumFeature feature = null)
        {
            var markdown = ClientEx.ParseMarkdown(text);

            var confirm = await ShowActionAsync(navigationService.XamlRoot, markdown, action, ToastPopupIcon.Premium);
            if (confirm == ContentDialogResult.Primary)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                foreach (var popup in popups)
                {
                    if (popup.Child is MessageEffectMenuFlyout)
                    {
                        popup.IsOpen = false;
                    }
                }

                if (feature != null)
                {
                    navigationService.ShowPromo(feature);
                }
                else
                {
                    navigationService.ShowPromo(source);
                }
            }
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), null, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), icon, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), icon, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, text, null, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FrameworkElement label, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
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

            return ShowImpl(xamlRoot, label, animated, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
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

            return ShowImpl(xamlRoot, text, animated, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
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

            return ShowImpl(xamlRoot, text, animated, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, string text, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, ToastPopupIcon.None, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, string text, ToastPopupIcon icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, ClientEx.ParseMarkdown(text), icon, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, FormattedText text, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, ToastPopupIcon.None, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, FormattedText text, ToastPopupIcon icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
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

            return ShowImpl(target.XamlRoot, text, animated, placement, requestedTheme, dismissAfter, target);
        }

        public static ToastPopup ShowImpl(XamlRoot xamlRoot, FormattedText text, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, FrameworkElement target = null)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
            };

            TextBlockHelper.SetFormattedText(label, text);
            return ShowImpl(xamlRoot, label, icon, placement, requestedTheme, dismissAfter, target);
        }

        public static ToastPopup ShowImpl(XamlRoot xamlRoot, FrameworkElement label, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, FrameworkElement target = null)
        {
            Logger.Info();
            Grid.SetColumn(label, 1);

            var content = new Grid();
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.Children.Add(label);

            if (icon != null)
            {
                content.Children.Add(icon);
            }

            var toast = new ToastPopup
            {
                Target = target,
                PreferredPlacement = placement,
                IsLightDismissEnabled = target != null && (dismissAfter == null || dismissAfter == TimeSpan.Zero),
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                MinWidth = 0,
                XamlRoot = xamlRoot
            };

            if (requestedTheme != ElementTheme.Default)
            {
                toast.RequestedTheme = requestedTheme;
            }

            try
            {
                if (xamlRoot.Content is IToastHost host)
                {
                    void handler(object sender, object e)
                    {
                        host.ToastClosed(toast);
                        toast.Closed -= handler;
                    }

                    host.ToastOpened(toast);
                    toast.Closed += handler;
                }
            }
            catch
            {
                Logger.Info("XamlRoot.Content thrown");
                return null;
            }

            if ((target == null || dismissAfter.HasValue) && (dismissAfter == null || dismissAfter.Value.TotalSeconds > 0))
            {
                var timer = new DispatcherTimer();
                timer.Interval = dismissAfter ?? TimeSpan.FromSeconds(3);

                void handler(object sender, object e)
                {
                    Logger.Info("closed");

                    timer.Tick -= handler;
                    toast.IsOpen = false;
                }

                timer.Tick += handler;
                timer.Start();
            }

            toast.IsOpen = true;
            return toast;
        }


        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, text, action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, ToastPopupIcon? icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
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

            return ShowActionAsync(xamlRoot, text, action, animated, placement, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
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

            return ShowActionAsync(xamlRoot, text, action, animated, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }
        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, text, action, icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            var toast = ShowImpl(xamlRoot, text, icon, placement, requestedTheme, dismissAfter);
            if (toast?.Content is Grid content)
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
                    Logger.Info("closed");

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

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FrameworkElement text, string action, FrameworkElement icon, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, CancellationToken cancellationToken = default)
        {
            var toast = ShowImpl(xamlRoot, text, icon, placement, requestedTheme, dismissAfter);
            if (toast?.Content is Grid content)
            {
                toast.MaxWidth = 500;

                if (cancellationToken != default)
                {
                    cancellationToken.Register(() =>
                    {
                        toast.IsOpen = false;
                    });
                }

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
                    Logger.Info("closed");

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

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, string text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, dismissAfter, TeachingTipPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, FormattedText text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(xamlRoot, text, action, dismissAfter, TeachingTipPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, FormattedText text, string action, TimeSpan dismissAfter, TeachingTipPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark)
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

            return ShowActionAsync(xamlRoot, text, action, animated, placement, requestedTheme, dismissAfter);
        }

        public event EventHandler<TextUrlClickEventArgs> Click;

        // Used by TextBlockHelper
        public void OnClick(string url)
        {
            Click?.Invoke(this, new TextUrlClickEventArgs(url));
        }
    }
}
