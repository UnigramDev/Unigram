//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatHistoryArrows : UserControl
    {
        public ChatHistoryArrows()
        {
            InitializeComponent();

            var visual = DropShadowEx.Attach(ArrowShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            visual = DropShadowEx.Attach(ArrowMentionsShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            visual = DropShadowEx.Attach(ArrowReactionsShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            var reactions = ElementComposition.GetElementVisual(ReactionsPanel);
            var mentions = ElementComposition.GetElementVisual(MentionsPanel);
            var messages = ElementComposition.GetElementVisual(MessagesPanel);

            reactions.CenterPoint = new Vector3(18, 36, 0);
            mentions.CenterPoint = new Vector3(18, 36, 0);
            messages.CenterPoint = new Vector3(18, 36, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(ReactionsPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(MentionsPanel, true);
        }

        public event RoutedEventHandler NextMention
        {
            add => MentionsButton.Click += value;
            remove => MentionsButton.Click -= value;
        }

        public event RightTappedEventHandler ReadMentions
        {
            add => MentionsButton.RightTapped += value;
            remove => MentionsButton.RightTapped -= value;
        }

        public event RoutedEventHandler NextReaction
        {
            add => ReactionsButton.Click += value;
            remove => ReactionsButton.Click -= value;
        }

        public event RightTappedEventHandler ReadReactions
        {
            add => ReactionsButton.RightTapped += value;
            remove => ReactionsButton.RightTapped -= value;
        }

        public event RoutedEventHandler NextMessage
        {
            add => MessagesButton.Click += value;
            remove => MessagesButton.Click -= value;
        }

        public event RightTappedEventHandler ReadMessages
        {
            add => MessagesButton.RightTapped += value;
            remove => MessagesButton.RightTapped -= value;
        }

        public int UnreadMentionCount
        {
            set
            {
                if (value > 0)
                {
                    ShowHideMentions(true);
                    Mentions.Text = value.ToString();
                }
                else
                {
                    ShowHideMentions(false);
                }
            }
        }

        public int UnreadReactionsCount
        {
            set
            {
                if (value > 0)
                {
                    ShowHideReactions(true);
                    Reactions.Text = value.ToString();
                }
                else
                {
                    ShowHideReactions(false);
                }
            }
        }

        public int UnreadCount
        {
            set
            {
                if (value > 0)
                {
                    Messages.Visibility = Visibility.Visible;
                    Messages.Text = value.ToString();
                }
                else
                {
                    Messages.Visibility = Visibility.Collapsed;
                }
            }
        }

        public bool IsVisible
        {
            get => !_messagesCollapsed;
            set => ShowHideMessages(value);
        }

        private bool _messagesCollapsed = true;
        private void ShowHideMessages(bool show)
        {
            if (_messagesCollapsed != show)
            {
                return;
            }

            _messagesCollapsed = !show;
            MessagesPanel.Visibility = Visibility.Visible;

            var reactions = ElementComposition.GetElementVisual(ReactionsPanel);
            var mentions = ElementComposition.GetElementVisual(MentionsPanel);
            var messages = ElementComposition.GetElementVisual(MessagesPanel);

            var compositor = messages.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                reactions.Properties.InsertVector3("Translation", Vector3.Zero);
                mentions.Properties.InsertVector3("Translation", Vector3.Zero);

                MessagesPanel.Visibility = _messagesCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One, easing);
            scale.Duration = Constants.SoftAnimation;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(show ? 0 : 1, 0);
            fade.InsertKeyFrame(show ? 1 : 0, 1, easing);
            fade.Duration = Constants.SoftAnimation;

            var translate = compositor.CreateScalarKeyFrameAnimation();
            translate.InsertKeyFrame(0, show ? 36 + 16 : 0);
            translate.InsertKeyFrame(1, show ? 0 : 36 + 16, easing);
            translate.Duration = Constants.SoftAnimation;

            reactions.StartAnimation("Translation.Y", translate);
            mentions.StartAnimation("Translation.Y", translate);
            messages.StartAnimation("Opacity", fade);
            messages.StartAnimation("Scale", scale);

            batch.End();
        }

        private bool _mentionsCollapsed = true;
        private void ShowHideMentions(bool show)
        {
            if (_mentionsCollapsed != show)
            {
                return;
            }

            _mentionsCollapsed = !show;
            MentionsPanel.Visibility = Visibility.Visible;

            var reactions = ElementComposition.GetElementVisual(ReactionsPanel);
            var mentions = ElementComposition.GetElementVisual(MentionsPanel);

            var compositor = mentions.Compositor;

            var batch = compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                reactions.Properties.InsertVector3("Translation", Vector3.Zero);

                MentionsPanel.Visibility = _mentionsCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One, easing);
            scale.Duration = Constants.FastAnimation;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(show ? 0 : 1, 0);
            fade.InsertKeyFrame(show ? 1 : 0, 1, easing);
            fade.Duration = Constants.FastAnimation;

            var translate = compositor.CreateScalarKeyFrameAnimation();
            translate.InsertKeyFrame(0, show ? 36 + 16 : 0);
            translate.InsertKeyFrame(1, show ? 0 : 36 + 16, easing);
            translate.Duration = Constants.FastAnimation;

            reactions.StartAnimation("Translation.Y", translate);
            mentions.StartAnimation("Opacity", fade);
            mentions.StartAnimation("Scale", scale);

            batch.End();
        }

        private bool _reactionsCollapsed = true;
        private void ShowHideReactions(bool show)
        {
            if (_reactionsCollapsed != show)
            {
                return;
            }

            _reactionsCollapsed = !show;
            ReactionsPanel.Visibility = Visibility.Visible;

            var reactions = ElementComposition.GetElementVisual(ReactionsPanel);

            var compositor = reactions.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ReactionsPanel.Visibility = _reactionsCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One, easing);
            scale.Duration = Constants.FastAnimation;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(show ? 0 : 1, 0);
            fade.InsertKeyFrame(show ? 1 : 0, 1, easing);
            fade.Duration = Constants.FastAnimation;

            reactions.StartAnimation("Opacity", fade);
            reactions.StartAnimation("Scale", scale);

            batch.End();
        }
    }
}
