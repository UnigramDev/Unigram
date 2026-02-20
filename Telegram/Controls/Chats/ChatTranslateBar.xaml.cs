//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatTranslateBar : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private ChatView _chatView;

        public ChatTranslateBar()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 32);

            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TranslateTo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ShowOriginal, true);
        }

        public float AnimatedHeight => _collapsed ? 0 : 32;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
        }

        public void UpdateChatIsTranslatable(Chat chat, string language)
        {
            var canTranslate = ViewModel.CanTranslate;
            if (canTranslate)
            {
                TranslateTo.Text = string.Format(Strings.TranslateToButton, TranslateService.LanguageName(SettingsService.Current.Translate.To));

                MenuButton.Visibility = ViewModel.IsPremium
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                HideButton.Visibility = ViewModel.IsPremium
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (language != null || !chat.IsTranslatable)
            {
                ShowHide(canTranslate);
            }
        }

        private SlidePanel.SlideState _collapsed;

        private void ShowHide(bool show)
        {
            if (show)
            {
                ShowHideOriginal(ViewModel.IsTranslating, _collapsed != show);
            }

            if (_collapsed != show)
            {
                return;
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }

        public IEnumerable<UIElement> GetAnimatableVisuals()
        {
            if (_collapsed)
            {
                yield break;
            }

            // TODO: translate button should be animated too

            if (HideButton.Visibility == Visibility.Visible)
            {
                yield return HideButton;
            }
            else
            {
                yield return MenuButton;
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var languageName = TranslateService.LanguageName(ViewModel.DetectedLanguage);

            var translateTo = flyout.CreateFlyoutItem(ViewModel.EditTranslate, Strings.TranslateTo, Icons.Translate);
            translateTo.KeyboardAcceleratorTextOverride = TranslateService.LanguageName(SettingsService.Current.Translate.To);

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(ViewModel.StopTranslate, string.Format(Strings.DoNotTranslateLanguage, languageName), Icons.HandRight);
            flyout.CreateFlyoutItem(ViewModel.HideTranslate, Strings.Hide, Icons.DismissCircle);

            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var button = new Button
            {
                Content = grid,
                Style = BootStrapper.Current.Resources["ListEmptyButtonStyle"] as Style,
                CornerRadius = new CornerRadius(4),
            };

            var block = new FormattedTextBlock
            {
                FontSize = 12,
                IsTextSelectionEnabled = false,
                IsHitTestVisible = false,
                AutoFontSize = false,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(11, 3, 11, 5),
                VerticalAlignment = VerticalAlignment.Center,
                HyperlinkStyle = Windows.UI.Xaml.Documents.UnderlineStyle.None,
                EmojiStyle = BootStrapper.Current.Resources["MessageCustomEmojiStyle"] as Style
            };

            grid.Children.Add(block);

            void click(object sender, RoutedEventArgs e)
            {
                button.Click -= click;
                flyout.Hide();

                ViewModel.ShowPopup(new CocoonAboutPopup());
            }

            button.Click += click;

            var content = new MenuFlyoutContent
            {
                Content = button,
                Padding = new Thickness(4, 2, 4, 2)
            };

            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(content);

            var markdown = ClientEx.ParseMarkdown(Strings.CocoonPoweredBy);

            var index = markdown.Text.IndexOf("\uD83E\uDD5A");
            if (index >= 0)
            {
                markdown.Entities.Add(new TextEntity(index, 2, new TextEntityTypeCustomEmoji(5197252827247841976)));
            }

            var link = ClientEx.ParseMarkdown(Strings.CocoonPoweredByLink);
            if (link.Entities.Count == 1)
            {
                link.Entities.Add(new TextEntity(link.Entities[0].Offset, link.Entities[0].Length, new TextEntityTypeTextUrl()));
            }

            block.SetText(ViewModel.ClientService, FormattedText.Join(" ", markdown, link));

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Translate_Click(object sender, RoutedEventArgs e)
        {
            ShowHideOriginal(ViewModel.TranslateChat());
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.HideTranslate();
        }

        private bool _showOriginal = false;

        private async void ShowHideOriginal(bool show, bool animate = true)
        {
            if (_showOriginal == show && animate)
            {
                return;
            }

            _showOriginal = show;

            if (ShowOriginal.ActualWidth == 0)
            {
                await ShowOriginal.UpdateLayoutAsync();
            }

            var transform = TranslateRoot.TransformToVisual(show ? ShowOriginal : TranslateTo);
            var point = transform.TransformVector2();

            var visual1 = ElementComposition.GetElementVisual(show ? ShowOriginal : TranslateTo);
            var visual2 = ElementComposition.GetElementVisual(show ? TranslateTo : ShowOriginal);
            var visual3 = ElementComposition.GetElementVisual(Icon);

            AutomationProperties.SetName(Translate, show ? ShowOriginal.Text : TranslateTo.Text);

            if (animate is false)
            {
                visual1.Properties.InsertVector3("Translation", Vector3.Zero);
                visual2.Properties.InsertVector3("Translation", Vector3.Zero);
                visual3.Properties.InsertVector3("Translation", new Vector3(-point.X - 28, 0, 0));
                visual1.Opacity = 1;
                visual2.Opacity = 0;

                return;
            }

            var compositor = visual1.Compositor;
            var duration = Constants.FastAnimation;

            var translation1 = compositor.CreateScalarKeyFrameAnimation();
            translation1.InsertKeyFrame(0, -4);
            translation1.InsertKeyFrame(1, 0);
            translation1.Duration = duration;

            var translation2 = compositor.CreateScalarKeyFrameAnimation();
            translation2.InsertKeyFrame(0, 0);
            translation2.InsertKeyFrame(1, 4);
            translation2.Duration = duration;

            var translation3 = compositor.CreateScalarKeyFrameAnimation();
            translation3.InsertKeyFrame(1, -point.X - 28);
            translation3.Duration = duration;

            var opacity1 = compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(0, 0);
            opacity1.InsertKeyFrame(1, 1);
            opacity1.Duration = duration;

            var opacity2 = compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(0, 1);
            opacity2.InsertKeyFrame(1, 0);
            opacity2.Duration = duration;

            visual1.StartAnimation("Translation.Y", translation1);
            visual2.StartAnimation("Translation.Y", translation2);
            visual3.StartAnimation("Translation.X", translation3);
            visual1.StartAnimation("Opacity", opacity1);
            visual2.StartAnimation("Opacity", opacity2);
        }
    }
}
