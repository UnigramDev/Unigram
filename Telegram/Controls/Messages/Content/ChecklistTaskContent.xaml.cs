//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Numerics;
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
    public sealed partial class ChecklistTaskContent : ToggleButtonEx
    {
        private MessageViewModel _message;
        private Checklist _checklist;
        private ChecklistTask _task;

        public ChecklistTaskContent()
        {
            DefaultStyleKey = typeof(ChecklistTaskContent);
        }

        #region InitializeComponent

        private Border PhotoRoot;
        private ProfilePicture Photo;
        private Ellipse CheckmarkNotDone;
        private Path CheckmarkDone;
        private Border CheckmarkIcon;
        private FormattedTextBlock TextText;
        private TextBlock UserText;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            CheckmarkNotDone = GetTemplateChild(nameof(CheckmarkNotDone)) as Ellipse;
            CheckmarkDone = GetTemplateChild(nameof(CheckmarkDone)) as Path;
            CheckmarkIcon = GetTemplateChild(nameof(CheckmarkIcon)) as Border;
            TextText = GetTemplateChild(nameof(TextText)) as FormattedTextBlock;
            UserText = GetTemplateChild(nameof(UserText)) as TextBlock;

            TextText.TextEntityClick += TextText_TextEntityClick;

            ElementCompositionPreview.SetIsTranslationEnabled(Photo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TextText, true);

            _templateApplied = true;

            if (_message != null && _checklist != null && _task != null)
            {
                UpdateChecklistTask(_message, _checklist, _task);
            }
        }

        private Visual GetTemplatePhoto(bool show)
        {
            if (PhotoRoot == null && show)
            {
                PhotoRoot = GetTemplateChild(nameof(PhotoRoot)) as Border;

                var clip = PlaceholderHelper.Foreground.GetEllipticalClip(20, 20, 12, -2, 10);
                var photo = ElementComposition.GetElementVisual(PhotoRoot);
                var geometry = photo.Compositor.CreatePathGeometry(clip);
                photo.Clip = photo.Compositor.CreateGeometricClip(geometry);
                photo.Clip.Offset = new Vector2(12, 0);
            }

            return ElementComposition.GetElementVisual(Photo);
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

        public ChecklistTask Task { get; private set; }

        private long _chatId;
        private long _messageId;
        private int _taskId;
        private MessageSender _completedBy;

        public void UpdateChecklistTask(MessageViewModel message, Checklist checklist, ChecklistTask task)
        {
            _message = message;
            _checklist = checklist;
            _task = task;

            if (!_templateApplied)
            {
                return;
            }

            var recycled = _chatId == message.ChatId
                && _messageId == message.Id
                && _taskId == task.Id;

            IsChecked = task.CompletionDate != 0;
            IsEnabled = true;
            Task = task;

            if (checklist.CanMarkTasksAsDone || message.SchedulingState != null)
            {
                CreateIcon();

                CheckmarkNotDone.Visibility = Visibility.Collapsed;
                CheckmarkDone.Visibility = Visibility.Collapsed;
                CheckmarkIcon.Visibility = Visibility.Visible;
            }
            else
            {
                CheckmarkNotDone.Visibility = task.CompletionDate != 0 ? Visibility.Collapsed : Visibility.Visible;
                CheckmarkDone.Visibility = task.CompletionDate != 0 ? Visibility.Visible : Visibility.Collapsed;
                CheckmarkIcon.Visibility = Visibility.Collapsed;
            }

            var show = checklist.CanMarkTasksAsDone && task.CompletedBy != null;

            if (show && message.ClientService.TryGetUser(task.CompletedBy, out User user))
            {
                Photo.Source = ProfilePictureSource.User(message.ClientService, user);
                UserText.Text = user.FullName();
            }
            else if (show && message.ClientService.TryGetChat(task.CompletedBy, out Chat chat))
            {
                Photo.Source = ProfilePictureSource.Chat(message.ClientService, chat);
                UserText.Text = chat.Title;
            }

            var photo = GetTemplatePhoto(show);
            var text = ElementComposition.GetElementVisual(TextText);
            var userText = ElementComposition.GetElementVisual(UserText);

            if (checklist.CanMarkTasksAsDone && recycled && !_completedBy.AreTheSame(task.CompletedBy))
            {
                var compositor = photo.Compositor;

                var translationY = compositor.CreateScalarKeyFrameAnimation();
                translationY.InsertKeyFrame(0, show ? 0 : -8);
                translationY.InsertKeyFrame(1, show ? -8 : 0);

                var translationX = compositor.CreateScalarKeyFrameAnimation();
                translationX.InsertKeyFrame(0, show ? 0 : 12);
                translationX.InsertKeyFrame(1, show ? 12 : 0);

                var opacity = compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, show ? 0 : 1);
                opacity.InsertKeyFrame(1, show ? 1 : 0);

                text.StartAnimation("Translation.Y", translationY);
                photo.StartAnimation("Translation.X", translationX);
                photo.StartAnimation("Opacity", opacity);
                userText.StartAnimation("Opacity", opacity);

                UpdateIcon(show, true);
            }
            else
            {
                text.Properties.InsertVector3("Translation", new Vector3(0, show ? -8 : 0, 0));
                photo.Properties.InsertVector3("Translation", new Vector3(show ? 12 : 0, 0, 0));
                photo.Opacity = show ? 1 : 0;
                userText.Opacity = show ? 1 : 0;

                UpdateIcon(show, false);
            }

            TextText.SetText(message.ClientService, task.Text);

            if (checklist.CanMarkTasksAsDone || task.CompletionDate == 0)
            {
                TextText.TextDecorations = Windows.UI.Text.TextDecorations.None;
            }
            else
            {
                TextText.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
            }

            AutomationProperties.SetName(this, task.Text.Text);

            _chatId = message.ChatId;
            _messageId = message.Id;
            _taskId = task.Id;
            _completedBy = task.CompletedBy;
        }

        private void CreateIcon()
        {
            if (_source != null)
            {
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
            properties.InsertScalar("Progress", 1.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 1.0F);
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
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChecklistTaskContent), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChecklistTaskContent)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _selectionStrokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion
    }
}
