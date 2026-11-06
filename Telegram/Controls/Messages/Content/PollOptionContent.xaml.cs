//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PollOptionContent : ToggleButtonEx
    {
        private bool _allowToggle;

        private MessageViewModel _message;
        private Poll _poll;
        private PollOption _option;
        private PollMedia _media;

        private bool _canVote;
        private bool _showResults;
        private int _maximum;

        public PollOptionContent()
        {
            DefaultStyleKey = typeof(PollOptionContent);
        }

        #region InitializeComponent

        private Grid RootGrid;
        private Rectangle Ellipse;
        private Microsoft.UI.Xaml.Controls.ProgressRing Loading;
        private TextBlock Percentage;
        private FormattedTextBlock TextText;
        private StackPanel RecentVotersRoot;
        private TextBlock RecentVotersCount;
        private RecentUserHeads RecentVoters;
        private HyperlinkButton Media;
        private Border Tick;
        private Border Votes;
        private Rectangle VotesIndicator;
        private Border VotesLine;
        private Border CheckmarkIcon;
        private Grid CheckmarkRoot;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;
            Ellipse = GetTemplateChild(nameof(Ellipse)) as Rectangle;
            Loading = GetTemplateChild(nameof(Loading)) as Microsoft.UI.Xaml.Controls.ProgressRing;
            Percentage = GetTemplateChild(nameof(Percentage)) as TextBlock;
            TextText = GetTemplateChild(nameof(TextText)) as FormattedTextBlock;
            Tick = GetTemplateChild(nameof(Tick)) as Border;
            Votes = GetTemplateChild(nameof(Votes)) as Border;
            VotesIndicator = GetTemplateChild(nameof(VotesIndicator)) as Rectangle;
            VotesLine = GetTemplateChild(nameof(VotesLine)) as Border;
            CheckmarkIcon = GetTemplateChild(nameof(CheckmarkIcon)) as Border;
            CheckmarkRoot = GetTemplateChild(nameof(CheckmarkRoot)) as Grid;

            TextText.TextEntityClick += TextText_TextEntityClick;

            _templateApplied = true;

            if (_message != null && _poll != null && _option != null)
            {
                UpdatePollOption(_message, _poll, _option, _canVote, _showResults, _maximum);
            }
        }

        #endregion

        protected override void OnLoaded()
        {
            _selectionStrokeBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _selectionStrokeBrush?.Unregister();
        }

        private void TextText_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            MessageBubble.TextEntityClick(_message, e);
        }

        public PollOption Option => _option;

        private long _chatId;
        private long _messageId;
        private int _optionId;
        private bool _chosen;

        public void UpdatePollOption(MessageViewModel message, Poll poll, PollOption option, bool canVote, bool showResults, int maximum)
        {
            _message = message;
            _poll = poll;
            _option = option;
            _canVote = canVote;
            _showResults = showResults;
            _maximum = maximum;

            if (!_templateApplied)
            {
                return;
            }

            var optionId = poll.Options.IndexOf(option);
            var recycled = _chatId == message.ChatId
                && _messageId == message.Id
                && _optionId == optionId;

            var quiz = poll.Type as PollTypeQuiz;
            var correct = quiz != null && quiz.CorrectOptionIds.Contains(optionId);

            // The correct options are withheld until the user can see them.
            var graded = showResults && quiz != null && quiz.CorrectOptionIds.Count > 0;

            var votes = Locale.Declension(quiz != null ? Strings.R.Answer : Strings.R.Vote, option.VoterCount);

            IsThreeState = !canVote;

            RootGrid.Padding = new Thickness(10, option.Media != null ? 4 : 0, 10, 0);

            // A null state is left behind by the results, leaving a preview has to clear it.
            if (!canVote || !recycled || !poll.AllowsMultipleAnswers || IsChecked == null)
            {
                IsChecked = canVote ? false : null;
            }

            _allowToggle = poll.AllowsMultipleAnswers && canVote;

            // A vote on a poll that hides its results has no bar to show for it.
            var hiddenChoice = option.IsChosen && !showResults && !option.IsBeingChosen;
            var hiddenSingleChoice = hiddenChoice && !poll.AllowsMultipleAnswers;

            if (_allowToggle || (hiddenChoice && poll.AllowsMultipleAnswers))
            {
                CreateIcon(true);
                UpdateIcon(IsChecked == true || hiddenChoice, false);
            }
            else
            {
                UpdateIcon(false, false);
                CheckmarkIcon.Visibility = Visibility.Collapsed;
            }

            CheckmarkRoot.Visibility = hiddenSingleChoice
                ? Visibility.Visible
                : Visibility.Collapsed;

            Ellipse.Opacity = showResults || option.IsBeingChosen || hiddenSingleChoice ? 0 : 1;
            Ellipse.RadiusX = Ellipse.RadiusY = poll.AllowsMultipleAnswers ? 4 : 10;
            Tick.CornerRadius = new CornerRadius(poll.AllowsMultipleAnswers ? 2 : 6);

            Percentage.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            Extensions.SetToolTip(Percentage, showResults ? votes : null);

            TextText.SetText(message.ClientService, option.Text);

            if (showResults && option.VoterCount > 0)
            {
                if (RecentVotersRoot == null)
                {
                    RecentVotersRoot = GetTemplateChild(nameof(RecentVotersRoot)) as StackPanel;
                    RecentVotersCount = GetTemplateChild(nameof(RecentVotersCount)) as TextBlock;
                    RecentVoters = GetTemplateChild(nameof(RecentVoters)) as RecentUserHeads;
                    RecentVoters.RecentUserHeadChanged += RecentVoters_RecentUserHeadChanged;
                }

                RecentVotersRoot.Visibility = Visibility.Visible;

                var destination = RecentVoters.Items;
                var origin = option.RecentVoterIds;

                if (destination.Count > 0 && recycled)
                {
                    destination.ReplaceDiff(origin);
                }
                else
                {
                    destination.ReplaceWith(origin);
                }

                RecentVotersCount.Text = Formatter.ShortNumber(option.VoterCount);
            }
            else if (RecentVotersRoot != null)
            {
                RecentVotersRoot.Visibility = Visibility.Collapsed;
            }

            UpdatePollOptionMedia(message, option);

            if (showResults)
            {
                float maximumVotes = Math.Max(maximum, 1);
                UpdateVotes(option.VoterCount / maximumVotes, recycled);
            }
            else
            {
                UpdateVotes(0, recycled);
            }

            Votes.Opacity = showResults ? 1 : 0;
            VotesLine.Opacity = showResults ? 0 : 0.3;

            Loading.IsActive = option.IsBeingChosen;

            Tick.Visibility = showResults && (correct || option.IsChosen) ? Visibility.Visible : Visibility.Collapsed;

            VisualStateManager.GoToState(this, graded ? correct ? "Correct" : "Wrong" : "Unknown", false);

            if (showResults)
            {
                var name = $"{option.Text.Text}, {votes}, {option.VotePercentage}%";

                if (graded && (option.IsChosen || correct))
                {
                    name += ", " + (correct ? Strings.AccDescrQuizCorrectAnswer : Strings.AccDescrQuizIncorrectAnswer);
                }

                AutomationProperties.SetName(this, name);
            }
            else
            {
                AutomationProperties.SetName(this, option.Text.Text);
            }

            // Voting leaves focus on the option, and a name change alone is never read,
            // so the outcome of a quiz answer has to be announced.
            if (recycled && option.IsChosen && !_chosen && graded)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(this);
                peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent,
                    correct ? Strings.AccDescrQuizCorrectAnswer : Strings.AccDescrQuizIncorrectAnswer, "PollVoted");
            }

            _chatId = message.ChatId;
            _messageId = message.Id;
            _optionId = optionId;
            _chosen = option.IsChosen;
        }

        private float _votesProgress = -1;

        // The indicator stays as wide as the track and slides out of its rounded clip: scaling it
        // would flatten the rounded end of a short bar. 2 pixels always stay in, so an option
        // with no votes still shows a mark.
        private void UpdateVotes(float progress, bool animate)
        {
            if (_votesProgress == progress)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(VotesIndicator);
            var properties = visual.Properties;

            if (_votesProgress < 0)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(VotesIndicator, true);

                var translation = visual.Compositor.CreateExpressionAnimation("Vector3(Max((_.Progress - 1) * this.Target.Size.X, 2 - this.Target.Size.X), 0, 0)");
                translation.SetReferenceParameter("_", properties);

                properties.InsertScalar("Progress", progress);
                visual.StartAnimation("Translation", translation);
            }
            else if (animate)
            {
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(1, progress);
                animation.Duration = Constants.SoftAnimation;

                properties.StartAnimation("Progress", animation);
            }
            else
            {
                // A write does not override an animation still running on the property.
                properties.StopAnimation("Progress");
                properties.InsertScalar("Progress", progress);
            }

            _votesProgress = progress;
        }

        private void RecentVoters_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(_message.ClientService, sender);
        }

        private void UpdatePollOptionMedia(MessageViewModel message, PollOption option)
        {
            // Currently, can be only of the types messageAnimation, messageLocation, messagePhoto, messageSticker, messageVenue, or messageVideo without caption

            // Every update delivers new objects for the same media, and a vote updates all the
            // options: rebuilding here would reload and flash each image.
            var same = IsSameMedia(_media, option.Media);

            _media = option.Media;

            if (same)
            {
                return;
            }

            if (option.Media is PollMediaAnimation animation)
            {
                var child = CreateMediaImage();
                child.SetSource(message.ClientService, animation.Animation.Thumbnail?.File, animation.Animation.Minithumbnail);

                SetMedia(child);
            }
            else if (option.Media is PollMediaLocation location)
            {
                var child = CreateMediaImage();
                child.SetSource(message.ClientService, location.Location, 32, 32, message.ChatId);

                SetMedia(child);
            }
            else if (option.Media is PollMediaPhoto photo)
            {
                var child = CreateMediaImage();
                child.SetSource(message.ClientService, photo.Photo.GetSmall()?.Photo, photo.Photo.Minithumbnail);

                SetMedia(child);
            }
            else if (option.Media is PollMediaSticker sticker)
            {
                SetMedia(new AnimatedImage
                {
                    Width = 32,
                    Height = 32,
                    FrameSize = new Windows.Foundation.Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Source = DelayedFileSource.FromSticker(message.ClientService, sticker.Sticker)
                });
            }
            else if (option.Media is PollMediaVenue venue)
            {
                var child = CreateMediaImage();
                child.SetSource(message.ClientService, venue.Venue.Location, 32, 32, message.ChatId);

                SetMedia(child);
            }
            else if (option.Media is PollMediaVideo video)
            {
                var child = CreateMediaImage();
                child.SetSource(message.ClientService, video.Cover?.GetSmall()?.Photo ?? video.Video.Thumbnail?.File, video.Cover?.Minithumbnail ?? video.Video.Minithumbnail);

                SetMedia(child);
            }
            else if (Media != null)
            {
                Media.Content = null;
                Media.Visibility = Visibility.Collapsed;
            }
        }

        private ImageView CreateMediaImage()
        {
            return new ImageView
            {
                Width = 32,
                Height = 32,
                Stretch = Stretch.UniformToFill,
                XamlRoot = XamlRoot
            };
        }

        private void SetMedia(FrameworkElement child)
        {
            if (Media == null)
            {
                Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                Media.Click += Media_Click;
            }

            Media.Content = child;
            Media.Visibility = Visibility.Visible;
        }

        private static bool IsSameMedia(PollMedia x, PollMedia y)
        {
            return (x, y) switch
            {
                (null, null) => true,
                (PollMediaAnimation a, PollMediaAnimation b) => a.Animation.AnimationValue.Id == b.Animation.AnimationValue.Id,
                (PollMediaLocation a, PollMediaLocation b) => a.Location.Latitude == b.Location.Latitude && a.Location.Longitude == b.Location.Longitude,
                (PollMediaPhoto a, PollMediaPhoto b) => a.Photo.GetSmall()?.Photo.Id == b.Photo.GetSmall()?.Photo.Id,
                (PollMediaSticker a, PollMediaSticker b) => a.Sticker.StickerValue.Id == b.Sticker.StickerValue.Id,
                (PollMediaVenue a, PollMediaVenue b) => a.Venue.Location.Latitude == b.Venue.Location.Latitude && a.Venue.Location.Longitude == b.Venue.Location.Longitude,
                (PollMediaVideo a, PollMediaVideo b) => a.Video.VideoValue.Id == b.Video.VideoValue.Id,
                _ => false
            };
        }

        private void Media_Click(object sender, RoutedEventArgs e)
        {
            if (_option.Media is PollMediaLocation location)
            {
                _message.Delegate.OpenLocation(location.Location, null);
            }
            else if (_option.Media is PollMediaSticker sticker)
            {
                _message.Delegate.OpenSticker(sticker.Sticker);
            }
            else if (_option.Media is PollMediaVenue venue)
            {
                _message.Delegate.OpenLocation(venue.Venue.Location, null);
            }
            else
            {
                GalleryMedia media = null;
                if (_option.Media is PollMediaAnimation animation)
                {
                    media = new GalleryMedia(_message.ClientService, animation.Animation, _option.Text);
                }
                // Location
                else if (_option.Media is PollMediaPhoto photo)
                {
                    media = new GalleryMedia(_message.ClientService, photo.Photo, _option.Text);
                }
                // Sticker
                // Venue
                else if (_option.Media is PollMediaVideo video)
                {
                    media = new GalleryMedia(_message.ClientService, video.Video, _option.Text);
                }

                if (media != null)
                {
                    _message.Delegate.OpenMedia(media, Media);
                }
            }
        }

        protected override void OnToggle()
        {
            if (_allowToggle)
            {
                base.OnToggle();
                UpdateIcon(IsChecked is true, true);
            }
        }

        private void CreateIcon(bool allowsMultipleAnswers)
        {
            CheckmarkIcon.Visibility = Visibility.Visible;

            if (_source != null && _allowsMultipleAnswers == allowsMultipleAnswers)
            {
                return;
            }

            var visual = GetVisual(BootStrapper.Current.Compositor, allowsMultipleAnswers, out var source, out _props);

            _source = source;
            _previous = visual;
            _allowsMultipleAnswers = allowsMultipleAnswers;
            _selectionStrokeBrush = new CompositionVisualColorSource(SelectionStroke, source, "Color_FF0000", IsConnected);

            ElementCompositionPreview.SetElementChildVisual(CheckmarkIcon, visual?.RootVisual);
        }

        private void UpdateIcon(bool selected, bool animate)
        {
            if (_props != null && _previous != null)
            {
                if (animate)
                {
                    var linearEasing = _props.Compositor.CreateLinearEasingFunction();
                    var animation = _props.Compositor.CreateScalarKeyFrameAnimation();
                    animation.Duration = _previous.Duration;
                    animation.InsertKeyFrame(1, selected ? 1 : 0, linearEasing);

                    _props.StartAnimation("Progress", animation);
                }
                else
                {
                    // A write does not override an animation still running on the property.
                    _props.StopAnimation("Progress");
                    _props.InsertScalar("Progress", selected ? 1.0F : 0.0F);
                }
            }
        }

        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        private bool _allowsMultipleAnswers;

        private IAnimatedVisual GetVisual(Compositor compositor, bool allowsMultipleAnswers, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = allowsMultipleAnswers
                ? new PollSelect()
                : new ChecklistSelect();

            if (source == null)
            {
                properties = null;
                return null;
            }

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 0.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        #region SelectionStroke

        private CompositionVisualColorSource _selectionStrokeBrush;

        public SolidColorBrush SelectionStroke
        {
            get { return (SolidColorBrush)GetValue(SelectionStrokeProperty); }
            set { SetValue(SelectionStrokeProperty, value); }
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(PollOptionContent), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PollOptionContent)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _selectionStrokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion
    }

    // The recent voters share the last line of the text when they fit beside it, and take a line
    // of their own when they do not.
    public partial class PollOptionTextPanel : Panel
    {
        private double _votersTop;

        // Taken off the box of a line that reads against the flow, which would otherwise sit at
        // the far edge of it, under the voters.
        private double _textInset;

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Children[0] as FormattedTextBlock;
            text.Measure(availableSize);

            var width = text.DesiredSize.Width;
            var height = text.DesiredSize.Height;

            _textInset = 0;

            // x:Load keeps the voters out of Children until an option first has some.
            if (Children.Count > 1 && Children[1].Visibility == Visibility.Visible)
            {
                var voters = Children[1];
                voters.Measure(availableSize);

                // Negative when the last line cannot be shared, whatever the room beside it.
                var contentEnd = text.ContentEnd();
                var votersEnd = contentEnd + voters.DesiredSize.Width;

                if (contentEnd >= 0 && votersEnd <= availableSize.Width)
                {
                    width = Math.Max(width, votersEnd);

                    if (text.ContentAgainstFlow)
                    {
                        _textInset = voters.DesiredSize.Width;
                    }
                }
                else
                {
                    height += voters.DesiredSize.Height;
                }

                _votersTop = height - text.Margin.Bottom - voters.DesiredSize.Height;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = Children[0];
            text.Arrange(new Rect(0, 0, finalSize.Width - _textInset, text.DesiredSize.Height));

            if (Children.Count > 1)
            {
                var voters = Children[1];
                voters.Arrange(new Rect(finalSize.Width - voters.DesiredSize.Width, _votersTop, voters.DesiredSize.Width, voters.DesiredSize.Height));
            }

            return finalSize;
        }
    }
}
