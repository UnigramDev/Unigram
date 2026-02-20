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
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
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

        private Ellipse Ellipse;
        private Microsoft.UI.Xaml.Controls.ProgressRing Loading;
        private TextBlock Percentage;
        private FormattedTextBlock TextText;
        private Grid Tick;
        private Ellipse Zero;
        private Windows.UI.Xaml.Controls.ProgressBar Votes;
        private Border VotesLine;
        private Border CheckmarkIcon;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Ellipse = GetTemplateChild(nameof(Ellipse)) as Ellipse;
            Loading = GetTemplateChild(nameof(Loading)) as Microsoft.UI.Xaml.Controls.ProgressRing;
            Percentage = GetTemplateChild(nameof(Percentage)) as TextBlock;
            TextText = GetTemplateChild(nameof(TextText)) as FormattedTextBlock;
            Tick = GetTemplateChild(nameof(Tick)) as Grid;
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

            var results = poll.IsClosed || poll.Options.Any(x => x.IsChosen);
            var correct = poll.Type is PollTypeQuiz quiz && quiz.CorrectOptionId == poll.Options.IndexOf(option);

            var votes = Locale.Declension(poll.Type is PollTypeQuiz ? Strings.R.Answer : Strings.R.Vote, option.VoterCount);

            Option = option;
            IsThreeState = results;

            if (results || !recycled || poll.Type is PollTypeRegular { AllowMultipleAnswers: false })
            {
                IsChecked = results ? null : new bool?(false);
            }

            _allowToggle = poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers && !results;

            if (_allowToggle)
            {
                CreateIcon();
            }
            else
            {
                UpdateIcon(false, false);
                CheckmarkIcon.Visibility = Visibility.Collapsed;
            }

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            Extensions.SetToolTip(Percentage, results ? votes : null);

            TextText.SetText(message.ClientService, option.Text);

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? Math.Max(poll.Options.Max(x => x.VoterCount), 1) : 1;
            Votes.Value = results ? option.VoterCount : 0;
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

        protected override void OnToggle()
        {
            if (_allowToggle)
            {
                base.OnToggle();
                UpdateIcon(IsChecked is true, true);
            }
        }

        private void CreateIcon()
        {
            if (_source != null)
            {
                CheckmarkIcon.Visibility = Visibility.Visible;
                return;
            }

            var visual = GetVisual(BootStrapper.Current.Compositor, out var source, out _props);

            _source = source;
            _previous = visual;
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

        private IAnimatedVisual GetVisual(Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = new ChecklistSelect();

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
