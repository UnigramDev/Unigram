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
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Profile.Popups
{
    public sealed partial class ProfileRatingPopup : ContentPopup
    {
        private readonly int _pendingRatingDate;

        private readonly long _currentLevelRating;
        private readonly long _nextLevelRating;
        private readonly long _rating;
        private readonly long _ratingLevel;

        private readonly long _currentLevelPending;
        private readonly long _nextLevelPending;
        private readonly long _pending;
        private readonly long _pendingLevel;

        private bool _preview;

        public ProfileRatingPopup(IClientService clientService, User user, UserRating rating, UserRating pendingRating, int pendingRatingDate)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.StarRatingButtonUnderstood;

            _currentLevelRating = rating.CurrentLevelRating;
            _nextLevelRating = Math.Max(rating.Rating, rating.NextLevelRating);
            _rating = rating.Rating;
            _ratingLevel = rating.Level;

            if (pendingRating != null)
            {
                _currentLevelPending = pendingRating.CurrentLevelRating;
                _nextLevelPending = Math.Max(pendingRating.Rating, pendingRating.NextLevelRating);
                _pending = pendingRating.Rating;
                _pendingLevel = pendingRating.Level;
            }

            _pendingRatingDate = pendingRatingDate;

            // TODO: remove once fixed in TDLib.
            if (rating.Level < 0 && user.Id == clientService.Options.MyId)
            {
                _rating = -new Random().Next(1000, 10000);
            }
            else if (rating.Level < 0)
            {
                _rating = -1;
            }

            if (_rating < 0)
            {
                _currentLevelRating = _rating * 2;
            }

            if (_pending < 0)
            {
                _currentLevelPending = _pending * 2;
            }

            var negative = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

            RatingSlider.Minimum = _currentLevelRating;
            RatingSlider.Maximum = _nextLevelRating;

            if (rating.Level < 0)
            {
                RatingSlider.Glyph = Icons.WarningFilled;
                RatingSlider.Background = negative;

                RatingSlider.MinimumText = string.Empty;
                RatingSlider.MaximumText = Strings.StarRatingLevelNegative;
            }
            else
            {
                RatingSlider.MinimumText = string.Format(Strings.StarRatingLevel, rating.Level);

                if (!rating.IsMaximumLevelReached)
                {
                    RatingSlider.MaximumText = string.Format(Strings.StarRatingLevel, rating.Level + 1);
                }
            }

            if (pendingRating != null && pendingRating.Level != rating.Level)
            {
                PendingSlider.Minimum = _currentLevelPending;
                PendingSlider.Maximum = _nextLevelPending;

                if (pendingRating.Level < 0)
                {
                    PendingSlider.Glyph = Icons.WarningFilled;
                    PendingSlider.Background = negative;

                    PendingSlider.MinimumText = string.Empty;
                    PendingSlider.MaximumText = Strings.StarRatingLevelNegative;
                }
                else
                {
                    PendingSlider.MinimumText = string.Format(Strings.StarRatingLevel, pendingRating.Level);

                    if (!pendingRating.IsMaximumLevelReached)
                    {
                        PendingSlider.MaximumText = string.Format(Strings.StarRatingLevel, pendingRating.Level + 1);
                    }
                }

                var pending = ElementComposition.GetElementVisual(PendingSlider);
                pending.Opacity = 0;
            }
            else
            {
                PendingSlider.Visibility = Visibility.Collapsed;
            }

            if (user.Id == clientService.Options.MyId)
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.StarRatingSelfDescription);

                if (rating.Level < 0)
                {
                    RatingSlider.MaximumVisibility = Visibility.Collapsed;
                }

                if (pendingRating != null)
                {
                    var diff = pendingRatingDate - DateTime.Now.ToTimestamp();
                    var days = diff / (60 * 60 * 24);

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(Strings.StarRatingFuturePendingPointsPreview.Replace("**", string.Empty));
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Click += Preview_Click;

                    RatingInfo.Inlines.Add(Locale.Declension(Strings.R.StarRatingFuture, (int)days));
                    RatingInfo.Inlines.Add(new LineBreak());
                    RatingInfo.Inlines.Add(Locale.Declension(Strings.R.StarRatingFuturePendingPoints, _pending - _rating));
                    RatingInfo.Inlines.Add(Icons.Space);
                    RatingInfo.Inlines.Add(hyperlink);

                    hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(Strings.StarRatingFuturePendingPointsPreviewBack.Replace("**", string.Empty));
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Click += Preview_Click;

                    RatingPreview.Inlines.Clear();
                    RatingPreview.Inlines.Add(Locale.Declension(Strings.R.StarRatingFuturePreview1, (int)days));
                    RatingPreview.Inlines.Add(new LineBreak());
                    RatingPreview.Inlines.Add(Locale.Declension(Strings.R.StarRatingFuturePreview2, _pending - _rating));
                    RatingPreview.Inlines.Add(Icons.Space);
                    RatingPreview.Inlines.Add(hyperlink);
                }
                else
                {
                    RatingInfo.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.StarRatingDescription, user.FullName()));

                if (rating.Level < 0)
                {
                    RatingSlider.ValueVisibility = Visibility.Collapsed;
                    RatingSlider.MaximumVisibility = Visibility.Collapsed;

                    RatingInfo.Foreground = negative;
                    TextBlockHelper.SetMarkdown(RatingInfo, string.Format(Strings.StarRatingLevelNegativeOther, user.FullName()));
                }
                else
                {
                    RatingInfo.Visibility = Visibility.Collapsed;
                }
            }

            AddText(Description1, Strings.StarRatingDescription1, true);
            AddText(Description2, Strings.StarRatingDescription2, true);
            AddText(Description3, Strings.StarRatingDescription3, false);

            //Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/CollectibleUsername.tgs");
        }

        private void Preview_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            _preview = !_preview;

            if (PendingSlider.Visibility == Visibility.Visible)
            {
                var visual1 = ElementComposition.GetElementVisual(PendingSlider);
                var visual2 = ElementComposition.GetElementVisual(RatingSlider);

                var easeOut = visual1.Compositor.CreateLinearEasingFunction();

                var anim1 = visual1.Compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(_preview ? 0 : 1, 0, easeOut);
                anim1.InsertKeyFrame(_preview ? 1 : 0, 1, easeOut);
                anim1.Duration = TimeSpan.FromMilliseconds(333);
                anim1.DelayBehavior = Windows.UI.Composition.AnimationDelayBehavior.SetInitialValueBeforeDelay;
                anim1.DelayTime = _preview ? TimeSpan.FromMilliseconds(267) : TimeSpan.Zero;

                var anim2 = visual1.Compositor.CreateScalarKeyFrameAnimation();
                anim2.InsertKeyFrame(_preview ? 1 : 0, 0, easeOut);
                anim2.InsertKeyFrame(_preview ? 0 : 1, 1, easeOut);
                anim2.Duration = TimeSpan.FromMilliseconds(333);
                anim2.DelayBehavior = Windows.UI.Composition.AnimationDelayBehavior.SetInitialValueBeforeDelay;
                anim2.DelayTime = _preview ? TimeSpan.Zero : TimeSpan.FromMilliseconds(267);

                visual1.StartAnimation("Opacity", anim1);
                visual2.StartAnimation("Opacity", anim2);

                if (_pendingLevel > _ratingLevel)
                {
                    PendingSlider.Value = (double)(_preview ? _currentLevelPending : _pending);
                    PendingSlider.Animate(_preview ? _pending : _currentLevelPending, _preview ? TimeSpan.FromMilliseconds(267) : TimeSpan.Zero);
                    RatingSlider.Animate(_preview ? _nextLevelRating : _rating, _preview ? TimeSpan.Zero : TimeSpan.FromMilliseconds(267));
                }

                PendingSlider.IsTabStop = _preview;
                RatingSlider.IsTabStop = !_preview;
            }
            else
            {
                RatingSlider.Animate(_preview ? _pending : _rating);
            }

            RatingPreview.Visibility = Visibility.Visible;

            var visualShow = ElementComposition.GetElementVisual(RatingPreview);
            var visualHide = ElementComposition.GetElementVisual(RatingInfo);

            var hide1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(_preview ? 0 : 1, new Vector3(1));
            hide1.InsertKeyFrame(_preview ? 1 : 0, new Vector3(0));

            var hide2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(_preview ? 0 : 1, 1);
            hide2.InsertKeyFrame(_preview ? 1 : 0, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            var show1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(_preview ? 1 : 0, new Vector3(1));
            show1.InsertKeyFrame(_preview ? 0 : 1, new Vector3(0));

            var show2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(_preview ? 1 : 0, 1);
            show2.InsertKeyFrame(_preview ? 0 : 1, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);
        }

        private void AddText(RichTextBlock block, string text, bool added)
        {
            var inline = new InlineUIContainer();
            inline.Child = new BadgeControl
            {
                Text = added ? Strings.StarRatingAdded : Strings.StarRatingDeduces,
                IsUnmuted = added,
                Margin = new Thickness(0, 0, 0, -4),
                CornerRadius = new CornerRadius(4)
            };

            var index = text.IndexOf("{0}");

            var prefix = text.Substring(0, index);
            var suffix = text.Substring(index + 3);

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(prefix);
            paragraph.Inlines.Add(inline);
            paragraph.Inlines.Add(suffix);

            block.Blocks.Add(paragraph);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (_ratingLevel < 0)
            {
                RatingSlider.Value = _rating;
            }
            else
            {
                RatingSlider.Animate(_rating);
            }
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }

        private void Visual_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = ElementComposition.GetElementVisual(element);
                visual.CenterPoint = new Vector3(element.ActualSize / 2, 0);
            }
        }
    }
}
