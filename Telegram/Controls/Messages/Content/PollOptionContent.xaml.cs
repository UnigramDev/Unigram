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
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PollOptionContent : ToggleButtonEx
    {
        private bool _allowToggle;

        private MessageViewModel _message;
        private Poll _poll;
        private PollOption _option;

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
        private Ellipse Zero;
        private Windows.UI.Xaml.Controls.ProgressBar Votes;
        private Border VotesLine;
        private Border CheckmarkIcon;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;
            Ellipse = GetTemplateChild(nameof(Ellipse)) as Rectangle;
            Loading = GetTemplateChild(nameof(Loading)) as Microsoft.UI.Xaml.Controls.ProgressRing;
            Percentage = GetTemplateChild(nameof(Percentage)) as TextBlock;
            TextText = GetTemplateChild(nameof(TextText)) as FormattedTextBlock;
            Tick = GetTemplateChild(nameof(Tick)) as Border;
            Zero = GetTemplateChild(nameof(Zero)) as Ellipse;
            Votes = GetTemplateChild(nameof(Votes)) as Windows.UI.Xaml.Controls.ProgressBar;
            VotesLine = GetTemplateChild(nameof(VotesLine)) as Border;
            CheckmarkIcon = GetTemplateChild(nameof(CheckmarkIcon)) as Border;

            TextText.TextEntityClick += TextText_TextEntityClick;

            _templateApplied = true;

            if (_message != null && _poll != null && _option != null)
            {
                UpdatePollOption(_message, _poll, _option);
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
            MessageBubble.TextEntityClick(_message, TextText, e);
        }

        public PollOption Option { get; private set; }

        private long _chatId;
        private long _messageId;
        private int _optionId;

        public void UpdatePollOption(MessageViewModel message, Poll poll, PollOption option)
        {
            _message = message;
            _poll = poll;
            _option = option;

            if (!_templateApplied)
            {
                return;
            }

            var optionId = poll.Options.IndexOf(option);
            var recycled = _chatId == message.ChatId
                && _messageId == message.Id
                && _optionId == optionId;

            var results = poll.IsClosed || poll.VoteRestrictionReason != null || poll.Options.Any(x => x.IsChosen);
            var correct = poll.Type is PollTypeQuiz quiz && quiz.CorrectOptionIds.Contains(poll.Options.IndexOf(option));

            var votes = Locale.Declension(poll.Type is PollTypeQuiz ? Strings.R.Answer : Strings.R.Vote, option.VoterCount);

            Option = option;
            IsThreeState = results;

            RootGrid.Padding = new Thickness(10, option.Media != null ? 4 : 0, 10, 0);

            if (results || !recycled || !poll.AllowsMultipleAnswers)
            {
                IsChecked = results ? null : new bool?(false);
            }

            _allowToggle = poll.AllowsMultipleAnswers && !results;

            if (_allowToggle)
            {
                CreateIcon(true);
            }
            else
            {
                UpdateIcon(false, false);
                CheckmarkIcon.Visibility = Visibility.Collapsed;
            }

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;
            Ellipse.RadiusX = Ellipse.RadiusY = poll.AllowsMultipleAnswers ? 4 : 10;
            Tick.CornerRadius = new CornerRadius(poll.AllowsMultipleAnswers ? 2 : 6);

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            Extensions.SetToolTip(Percentage, results ? votes : null);

            TextText.SetText(message.ClientService, option.Text);

            if (option.VoterCount > 0)
            {
                if (RecentVotersRoot == null)
                {
                    RecentVotersRoot = GetTemplateChild(nameof(RecentVotersRoot)) as StackPanel;
                    RecentVotersCount = GetTemplateChild(nameof(RecentVotersCount)) as TextBlock;
                    RecentVoters = GetTemplateChild(nameof(RecentVoters)) as RecentUserHeads;
                    RecentVoters.RecentUserHeadChanged += RecentVoters_RecentUserHeadChanged;
                }

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

            UpdatePollOptionMedia(message, poll, option);

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? Math.Max(poll.Options.Max(x => x.VoterCount), 1) : 1;
            Votes.Value = results ? option.VoterCount : 0;
            Votes.Opacity = results ? 1 : 0;
            VotesLine.Opacity = results ? 0 : 0.3;

            Loading.IsActive = option.IsBeingChosen;

            Tick.Visibility = (results && correct) || option.IsChosen ? Visibility.Visible : Visibility.Collapsed;

            if (option.IsChosen && poll.Type is PollTypeQuiz)
            {
                VisualStateManager.GoToState(this, correct ? "Correct" : "Wrong", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "Unknown", false);
            }

            if (results)
            {
                AutomationProperties.SetName(this, $"{option.Text.Text}, {votes}, {option.VotePercentage}%");
            }
            else
            {
                AutomationProperties.SetName(this, option.Text.Text);
            }

            _chatId = message.ChatId;
            _messageId = message.Id;
            _optionId = optionId;
        }

        private void RecentVoters_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(_message.ClientService, sender);
        }

        private void UpdatePollOptionMedia(MessageViewModel message, Poll poll, PollOption option)
        {
            // Currently, can be only of the types messageAnimation, messageLocation, messagePhoto, messageSticker, messageVenue, or messageVideo without caption

            if (option.Media is PollMediaAnimation animation)
            {
                var child = new ImageView
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.UniformToFill
                };

                child.XamlRoot = XamlRoot;
                child.SetSource(message.ClientService, animation.Animation.Thumbnail?.File, animation.Animation.Minithumbnail);

                if (Media == null)
                {
                    Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                    Media.Click += Media_Click;
                }

                Media.Content = child;
            }
            else if (option.Media is PollMediaLocation location)
            {
                var child = new ImageView
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.UniformToFill
                };

                child.XamlRoot = XamlRoot;
                child.SetSource(message.ClientService, location.Location, 32, 32, message.ChatId);

                if (Media == null)
                {
                    Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                    Media.Click += Media_Click;
                }

                Media.Content = child;
            }
            else if (option.Media is PollMediaPhoto photo)
            {
                var child = new ImageView
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.UniformToFill
                };

                child.XamlRoot = XamlRoot;
                child.SetSource(message.ClientService, photo.Photo.GetSmall()?.Photo, photo.Photo.Minithumbnail);

                if (Media == null)
                {
                    Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                    Media.Click += Media_Click;
                }

                Media.Content = child;
            }
            else if (option.Media is PollMediaSticker sticker)
            {
                Media ??= GetTemplateChild(nameof(Media)) as HyperlinkButton;
                Media.Content = new AnimatedImage
                {
                    Width = 32,
                    Height = 32,
                    FrameSize = new Windows.Foundation.Size(32, 32),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    Source = DelayedFileSource.FromSticker(message.ClientService, sticker.Sticker)
                };
            }
            else if (option.Media is PollMediaVenue venue)
            {
                var child = new ImageView
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.UniformToFill
                };

                child.XamlRoot = XamlRoot;
                child.SetSource(message.ClientService, venue.Venue.Location, 32, 32, message.ChatId);

                if (Media == null)
                {
                    Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                    Media.Click += Media_Click;
                }

                Media.Content = child;
            }
            else if (option.Media is PollMediaVideo video)
            {
                var child = new ImageView
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.UniformToFill
                };

                child.XamlRoot = XamlRoot;
                child.SetSource(message.ClientService, video.Cover?.GetSmall()?.Photo ?? video.Video.Thumbnail?.File, video.Cover?.Minithumbnail ?? video.Video.Minithumbnail);

                if (Media == null)
                {
                    Media = GetTemplateChild(nameof(Media)) as HyperlinkButton;
                    Media.Click += Media_Click;
                }

                Media.Content = child;
            }
            else
            {
                Media?.Content = null;
            }
        }

        private void Media_Click(object sender, RoutedEventArgs e)
        {
            if (_option.Media is PollMediaLocation location)
            {
                _message.Delegate.OpenLocation(location.Location, null);
            }
            else if (_option.Media is PollMediaSticker sticker)
            {

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
                    media = new GalleryAnimation(_message.ClientService, animation.Animation, _option.Text);
                }
                // Location
                else if (_option.Media is PollMediaPhoto photo)
                {
                    media = new GalleryPhoto(_message.ClientService, photo.Photo, _option.Text);
                }
                // Sticker
                // Venue
                else if (_option.Media is PollMediaVideo video)
                {
                    media = new GalleryVideo(_message.ClientService, video.Video, _option.Text);
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
            if (_source != null && _allowsMultipleAnswers == allowsMultipleAnswers)
            {
                CheckmarkIcon.Visibility = Visibility.Visible;
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
}
