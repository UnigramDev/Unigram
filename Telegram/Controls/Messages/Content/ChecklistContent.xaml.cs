//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class ChecklistContent : Control, IContent, IDiffEqualityComparer<ChecklistTask>
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private readonly Dictionary<int, ChecklistTaskContent> _cache = new();

        private IList<ChecklistTask> _prevValue;

        public ChecklistContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(ChecklistContent);
        }

        #region InitializeComponent

        private FormattedTextBlock TitleText;
        private TextBlock Type;
        private StackPanel Tasks;
        private TextBlock Completed;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            TitleText = GetTemplateChild(nameof(TitleText)) as FormattedTextBlock;
            Type = GetTemplateChild(nameof(Type)) as TextBlock;
            Tasks = GetTemplateChild(nameof(Tasks)) as StackPanel;
            Completed = GetTemplateChild(nameof(Completed)) as TextBlock;

            TitleText.TextEntityClick += TitleText_TextEntityClick;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var recycled = _message?.ChatId == message.ChatId && _message?.Id == message.Id;

            _message = message;

            var checklist = message.Content as MessageChecklist;
            if (checklist == null | !_templateApplied)
            {
                return;
            }

            if (!checklist.List.CanMarkTasksAsDone && !message.IsOutgoing && message.ForwardInfo == null)
            {
                Type.Text = string.Format(Strings.MessagePersonalTodoList, message.ClientService.GetTitle(message.SenderId, true));
            }
            else if (message.Chat.Type is ChatTypePrivate || !checklist.List.CanMarkTasksAsDone || message.ForwardInfo != null)
            {
                Type.Text = Strings.MessageTodoList;
            }
            else
            {
                Type.Text = Strings.MessageGroupTodoList;
            }

            TitleText.SetText(message.ClientService, checklist.List.Title);

            var completed = 0;

            void UpdateItem(ChecklistTask oldItem, ChecklistTask newItem, int index = 0)
            {
                if (newItem != null)
                {
                    oldItem.Text = newItem.Text;
                    oldItem.CompletionDate = newItem.CompletionDate;
                    oldItem.CompletedBy = newItem.CompletedBy;
                }

                if (oldItem.CompletionDate != 0)
                {
                    completed++;
                }

                UpdateButton(message, checklist.List, oldItem, index);
            }

            if (_prevValue == null || !recycled)
            {
                _cache.Clear();
                Tasks.Children.Clear();

                for (int i = 0; i < checklist.List.Tasks.Count; i++)
                {
                    UpdateItem(checklist.List.Tasks[i], null, i);
                }
            }
            else
            {
                // PERF: run diff asynchronously?
                var prev = _prevValue ?? Array.Empty<ChecklistTask>();
                var diff = DiffUtil.CalculateDiff(prev, checklist.List.Tasks, this, Constants.DiffOptions);

                foreach (var step in diff.Steps)
                {
                    if (step.Status == DiffStatus.Add)
                    {
                        UpdateItem(step.Items[0].NewValue, null, step.NewStartIndex);
                    }
                    else if (step.Status == DiffStatus.Move && step.OldStartIndex < Tasks.Children.Count && step.NewStartIndex < Tasks.Children.Count)
                    {
                        UpdateItem(step.Items[0].OldValue, step.Items[0].NewValue);
                        Tasks.Children.Move((uint)step.OldStartIndex, (uint)step.NewStartIndex);
                    }
                    else if (step.Status == DiffStatus.Remove && step.OldStartIndex < Tasks.Children.Count)
                    {
                        if (step.Items[0].OldValue is ChecklistTask oldTask)
                        {
                            _cache.Remove(oldTask.Id);
                        }

                        Tasks.Children.RemoveAt(step.OldStartIndex);
                    }
                }

                foreach (var item in diff.NotMovedItems)
                {
                    UpdateItem(item.OldValue, item.NewValue);
                }
            }

            _prevValue = checklist.List.Tasks;
            Completed.Text = Locale.Declension(Strings.R.TodoCompleted, checklist.List.Tasks.Count, completed);
        }

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
            foreach (var child in Tasks.Children)
            {
                if (child is ChecklistTaskContent button
                    && button.Task.Id == options.ChecklistTaskId)
                {
                    var transform = child.TransformToVisual(this);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X, point.Y, button.ActualWidth, button.ActualHeight);
                }
            }

            return Rect.Empty;
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageChecklist;
        }

        private void TitleText_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            MessageBubble.TextEntityClick(_message, TitleText, e);
        }

        private async void Task_Click(object sender, RoutedEventArgs e)
        {
            if (_message?.SchedulingState != null)
            {
                await MessagePopup.ShowAsync(XamlRoot, Strings.MessageScheduledVote, Strings.AppName, Strings.OK);
                return;
            }

            var button = sender as ChecklistTaskContent;
            if (button.IsChecked == null)
            {
                return;
            }

            var task = button.Task as ChecklistTask;
            if (task == null)
            {
                return;
            }

            var checklist = _message?.Content as MessageChecklist;
            if (checklist == null)
            {
                return;
            }

            if (!_message.ClientService.IsPremium)
            {
                ToastPopup.ShowFeaturePromo(_message.Delegate.NavigationService, new PremiumFeatureChecklists());
                return;
            }
            else if (!checklist.List.CanMarkTasksAsDone)
            {
                if (_message.ForwardInfo != null)
                {
                    ToastPopup.Show(XamlRoot, Strings.TodoCompleteForbiddenForward, ToastPopupIcon.Error);
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format(Strings.TodoCompleteForbidden, _message.ClientService.GetTitle(_message.SenderId)), ToastPopupIcon.Error);
                }

                return;
            }

            var markedAsDone = new List<int>();
            var markedAsNotDone = new List<int>();

            if (task.CompletionDate != 0)
            {
                markedAsNotDone.Add(task.Id);
            }
            else
            {
                markedAsDone.Add(task.Id);
            }

            _message.ClientService.Send(new MarkChecklistTasksAsDone(_message.ChatId, _message.Id, markedAsDone, markedAsNotDone));
        }

        private void UpdateButton(MessageViewModel message, Checklist checklist, ChecklistTask item, int index)
        {
            var button = GetOrCreateButton(item.Id, index);
            button.UpdateChecklistTask(message, checklist, item);
        }

        private ChecklistTaskContent GetOrCreateButton(int taskId, int index)
        {
            if (_cache.TryGetValue(taskId, out ChecklistTaskContent button))
            {
                return button;
            }

            button = new ChecklistTaskContent();
            button.Click += Task_Click;

            _cache[taskId] = button;
            Tasks.Children.Insert(Math.Min(index, Tasks.Children.Count), button);

            return button;
        }

        public bool CompareItems(ChecklistTask oldItem, ChecklistTask newItem)
        {
            return oldItem.Id == newItem.Id;
        }
    }
}
