//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
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

        private UIElement _parent;

        public ChatTranslateBar()
        {
            InitializeComponent();

            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TranslateTo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ShowOriginal, true);
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        public void UpdateChatIsTranslatable(Chat chat, string language)
        {
            var canTranslate = ViewModel.IsTranslatable;
            if (canTranslate)
            {
                TranslateTo.Text = string.Format(Strings.TranslateToButton, TranslateService.LanguageName(SettingsService.Current.Translate.To));
            }

            ShowHide(canTranslate);
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            if (show)
            {
                ShowHideOriginal(ViewModel.IsTranslating, false);
            }

            var parent = ElementCompositionPreview.GetElementVisual(_parent);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 32);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -32, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
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


            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
            }
        }

        private void Translate_Click(object sender, RoutedEventArgs e)
        {
            var show = !ViewModel.IsTranslating;
            ViewModel.IsTranslating = show;
            ShowHideOriginal(show, true);
        }

        private bool _showOriginal = false;

        private async void ShowHideOriginal(bool show, bool animate)
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
            var point = transform.TransformPoint(new Windows.Foundation.Point()).ToVector2();

            var visual1 = ElementCompositionPreview.GetElementVisual(show ? ShowOriginal : TranslateTo);
            var visual2 = ElementCompositionPreview.GetElementVisual(show ? TranslateTo : ShowOriginal);
            var visual3 = ElementCompositionPreview.GetElementVisual(Icon);

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
