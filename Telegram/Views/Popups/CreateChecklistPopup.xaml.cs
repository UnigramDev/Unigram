//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public partial class CreateChecklistViewModel : ViewModelBase
    {
        public CreateChecklistViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator) : base(clientService, settingsService, aggregator)
        {
        }
    }

    public sealed partial class CreateChecklistPopup : ContentPopup
    {
        private readonly CreateChecklistViewModel _viewModel;

        private readonly Checklist _checklist;

        public CreateChecklistPopup(IClientService clientService, FormattedText title)
            : this(clientService, title, null, true, false, null)
        {
        }

        public CreateChecklistPopup(IClientService clientService, Checklist checklist, bool canBeEdited, bool addTask, ChecklistTask taskToEdit)
            : this(clientService, null, checklist, canBeEdited, addTask, taskToEdit)
        {

        }

        private CreateChecklistPopup(IClientService clientService, FormattedText title, Checklist checklist, bool canBeEdited, bool addTask, ChecklistTask taskToEdit)
        {
            InitializeComponent();

            _checklist = checklist;
            _viewModel = new CreateChecklistViewModel(clientService,
                clientService.Session.Resolve<ISettingsService>(),
                clientService.Session.Resolve<IEventAggregator>());

            TitleText.DataContext = _viewModel;
            TitleText.MaxLength = (int)clientService.Options.ChecklistTitleLengthMax;

            if (title != null)
            {
                TitleText.SetText(title);
            }

            AddTask.DataContext = _viewModel;
            AddTask.MaxLength = (int)clientService.Options.ChecklistTaskTextLengthMax;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            if (checklist == null)
            {
                base.Title = Strings.TodoTitle;
                PrimaryButtonText = Strings.Create;
            }
            else if (canBeEdited)
            {
                base.Title = Strings.TodoEditTitle;
                PrimaryButtonText = Strings.TodoEditTasksButton;
            }
            else
            {
                base.Title = Strings.TodoAddTasksTitle;
                PrimaryButtonText = Strings.TodoAddTasksButton;
            }

            SecondaryButtonText = Strings.Cancel;

            Items = new ObservableCollection<ChecklistTaskViewModel>();
            Items.CollectionChanged += Items_CollectionChanged;

            if (checklist != null)
            {
                TitleText.SetText(checklist.Title);

                foreach (var task in checklist.Tasks)
                {
                    Items.Add(new ChecklistTaskViewModel(task.Id, task.Text, !canBeEdited, task.Id == taskToEdit?.Id));
                }

                if (addTask)
                {
                    Items.Add(new ChecklistTaskViewModel(string.Empty, false, true));
                }
            }
            else
            {
                Items.Add(new ChecklistTaskViewModel(string.Empty, false, false));
            }

            if (checklist != null)
            {
                OthersCanMarkTasksAsDoneButton.IsChecked = checklist.OthersCanMarkTasksAsDone;
                OthersCanAddTasksButton.IsChecked = checklist.OthersCanAddTasks;

                if (!canBeEdited)
                {
                    TitleRoot.Visibility = Visibility.Collapsed;
                    Settings.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                OthersCanMarkTasksAsDoneButton.IsChecked = true;
                OthersCanAddTasksButton.IsChecked = true;
            }
        }

        public FormattedText Title
        {
            get
            {
                return TitleText.GetFormattedText();
            }
        }

        public IList<InputChecklistTask> Tasks => GetTasks(false);

        public IList<InputChecklistTask> AddedTasks => GetTasks(true);

        private IList<InputChecklistTask> GetTasks(bool addedTasksOnly)
        {
            var tasks = new List<InputChecklistTask>();
            var nextTaskId = Math.Max(0, Items.Max(x => x.Id));

            foreach (var item in Items)
            {
                if (string.IsNullOrEmpty(item.Text.Text))
                {
                    continue;
                }

                if (item.Id == -1)
                {
                    tasks.Add(new InputChecklistTask(++nextTaskId, ClientEx.ParseMarkdown(item.Text)));
                }
                else
                {
                    if (addedTasksOnly)
                    {
                        continue;
                    }

                    tasks.Add(new InputChecklistTask(item.Id, ClientEx.ParseMarkdown(item.Text)));
                }
            }

            return tasks;
        }

        public bool OthersCanMarkTasksAsDone => OthersCanMarkTasksAsDoneButton.IsChecked == true;

        public bool OthersCanAddTasks => OthersCanAddTasksButton.IsChecked == true;

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Items.Count >= _viewModel.ClientService.Options.ChecklistTaskCountMax)
            {
                AddTaskRoot.Visibility = Visibility.Collapsed;
                AddInfo.Text = Strings.TodoAddTaskInfoMax;
            }
            else
            {
                AddTaskRoot.Visibility = Visibility.Visible;
                AddInfo.Text = Locale.Declension(Strings.R.TodoNewTaskInfo, _viewModel.ClientService.Options.ChecklistTaskCountMax - Items.Count);
            }

            UpdatePrimaryButton();
        }

        private void UpdatePrimaryButton()
        {
            var condition = !TitleText.IsEmpty;
            condition = condition && Items.Count(x => !string.IsNullOrEmpty(x.Text.Text)) >= 1;

            IsPrimaryButtonEnabled = condition;
            IsLightDismissEnabled = condition;
        }

        public ObservableCollection<ChecklistTaskViewModel> Items { get; private set; }


        private void TitleText_GotFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(TitleEmoji, true);
        }

        private void TitleText_LostFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(TitleEmoji, false);
        }

        private void AddTask_TextChanged(object sender, EventArgs e)
        {
            EmojiFlyout.Hide();

            if (Items.Count < _viewModel.ClientService.Options.ChecklistTaskCountMax && !AddTask.IsReadOnly && !AddTask.IsEmpty)
            {
                Items.Add(new ChecklistTaskViewModel(AddTask.GetFormattedText(true, false), false, true));
                AddTask.IsReadOnly = true;
            }
        }

        private void AddTask_GotFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(AddTaskEmoji, true);
        }

        private void AddTask_LostFocus(object sender, RoutedEventArgs e)
        {
            OnVisibleChanged(AddTaskEmoji, false);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ChecklistTaskViewModel task })
            {
                Items.Remove(task);
                Focus(Items.Count - 1);
            }
        }

        private void Title_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdatePrimaryButton();
        }

        private void Task_TextChanged(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox textBox && textBox.Tag is ChecklistTaskViewModel task)
            {
                task.Text = textBox.GetFormattedText(parseMarkdown: false);
            }

            UpdatePrimaryButton();
        }

        private void Task_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox text && text.Tag is ChecklistTaskViewModel task)
            {
                if (task.FocusOnLoaded)
                {
                    text.Document.Selection.SetRange(int.MaxValue, int.MaxValue);
                    text.Focus(FocusState.Keyboard);
                }

                task.FocusOnLoaded = false;
            }
        }

        private void Task_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not FormattedTextBox text || text.Tag is not ChecklistTaskViewModel task)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Back && text.IsEmpty)
            {
                e.Handled = true;

                var index = Items.IndexOf(task);

                Items.Remove(task);

                if (index > 0)
                {
                    Focus(index - 1);
                }
                else if (index < Items.Count - 1)
                {
                    Focus(1);
                }
                else
                {
                    AddTask.Focus(FocusState.Keyboard);
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;

                var index = Items.IndexOf(task);
                if (index < Items.Count - 1)
                {
                    Focus(index + 1);
                }
                else
                {
                    AddTask.Focus(FocusState.Keyboard);
                }
            }
        }

        private void Task_GotFocus(object sender, RoutedEventArgs e)
        {
            AddTask.IsReadOnly = false;

            if (sender is FormattedTextBox textBox && textBox.Parent != null)
            {
                OnVisibleChanged(textBox.Parent.GetChild<GlyphButton>(), true);
            }
        }

        private void Task_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FormattedTextBox textBox && textBox.Parent != null)
            {
                OnVisibleChanged(textBox.Parent.GetChild<GlyphButton>(), false);
            }
        }

        private static void OnVisibleChanged(DependencyObject d, bool value)
        {
            var sender = d as UIElement;
            var newValue = value;
            var oldValue = !value;

            if (newValue == oldValue || (sender.Visibility == Visibility.Collapsed && !newValue))
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(sender);

            visual.CenterPoint = new Vector3(16, 12, 0);
            sender.Visibility = Visibility.Visible;

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Opacity = newValue ? 1 : 0;
                visual.Scale = new Vector3(true ? newValue ? 1 : 0 : 1);

                sender.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
            };

            var anim1 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(0, newValue ? 0 : 1);
            anim1.InsertKeyFrame(1, newValue ? 1 : 0);
            visual.StartAnimation("Opacity", anim1);

            var anim2 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim2.InsertKeyFrame(0, new Vector3(newValue ? 0 : 1));
            anim2.InsertKeyFrame(1, new Vector3(newValue ? 1 : 0));
            visual.StartAnimation("Scale", anim2);

            batch.End();
        }

        private void Focus(int task)
        {
            var container = ScrollingHost.ContainerFromIndex(task) as SelectorItem;
            if (container == null)
            {
                return;
            }

            var inner = container.GetChild<FormattedTextBox>();
            if (inner == null)
            {
                return;
            }

            if (inner.IsEnabled)
            {
                inner.Focus(FocusState.Keyboard);
            }
            else
            {
                AddTask.Focus(FocusState.Keyboard);
            }
        }

        private FormattedTextBox _target;

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: FormattedTextBox textBox })
            {
                return;
            }

            _target = textBox;

            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(textBox, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                _target?.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                _target?.InsertEmoji(sticker);
            }

            _target?.Focus(FocusState.Programmatic);
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _target = null;
        }

        private void OthersCanMarkTasksAsDone_Click(object sender, RoutedEventArgs e)
        {
            OthersCanAddTasksButton.Visibility = OthersCanMarkTasksAsDone
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.Handled = true;

            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is ChecklistTaskViewModel task)
            {
                var text = content.FindName("Text") as FormattedTextBox;
                var customEmoji = content.FindName("CustomEmoji") as CustomEmojiCanvas;
                var emoji = content.FindName("Emoji") as Button;
                var handle = content.FindName("Handle") as Border;
                var remove = content.FindName("Remove") as Button;

                text.Tag = task;
                text.DataContext = _viewModel;
                text.MaxLength = (int)_viewModel.ClientService.Options.ChecklistTaskTextLengthMax;
                text.CustomEmoji = customEmoji;

                text.SetText(task.Text);
                text.IsEnabled = !task.IsReadOnly;

                if (task.IsReadOnly)
                {
                    emoji.Tag = null;
                    handle.Opacity = 0.6;
                    remove.Visibility = Visibility.Collapsed;
                }
                else
                {
                    emoji.Tag = text;
                    handle.Opacity = 1;
                    remove.Visibility = Visibility.Visible;
                    remove.Tag = task;

                    // If the container is recycled the box may still be loaded
                    if (task.FocusOnLoaded && text.IsLoaded)
                    {
                        text.Document.Selection.SetRange(int.MaxValue, int.MaxValue);
                        text.Focus(FocusState.Keyboard);

                        task.FocusOnLoaded = false;
                    }
                }
            }
        }

        private int _reorderingIndex;
        private ChecklistTaskViewModel _reorderingTask;

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is ChecklistTaskViewModel task)
                {
                    if (Items.Count(x => !x.IsReadOnly) < 2 || task.IsReadOnly)
                    {
                        ScrollingHost.CanReorderItems = false;
                        e.Cancel = true;
                    }
                    else
                    {
                        _reorderingIndex = Items.IndexOf(task);
                        _reorderingTask = task;

                        ScrollingHost.CanReorderItems = true;
                    }
                }
            }
            catch
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ScrollingHost.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ChecklistTaskViewModel task)
            {
                var index = Items.IndexOf(task);
                var compare = Items[index < Items.Count - 1 ? index + 1 : index - 1];

                if (compare.IsReadOnly)
                {
                    Items.RemoveAt(index);
                    Items.Insert(_reorderingIndex, task);
                }
            }
        }

        private async void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary && !AreTheSame())
            {
                var deferral = args.GetDeferral();

                var confirm = await MessagePopup.ShowAsync(XamlRoot, null as FrameworkElement, Strings.CancelTodoAlertText, Strings.CancelTodoAlertTitle, Strings.PassportDiscard, Strings.Cancel, destructive: true);
                if (confirm != ContentDialogResult.Primary)
                {
                    args.Cancel = true;
                }

                deferral.Complete();
            }
        }

        private bool AreTheSame()
        {
            if (_checklist == null)
            {
                return true;
            }

            if (OthersCanMarkTasksAsDone != _checklist.OthersCanMarkTasksAsDone || OthersCanAddTasks != _checklist.OthersCanAddTasks || !Title.AreTheSame(_checklist.Title))
            {
                return false;
            }

            var tasks = Tasks;
            if (tasks.Count != _checklist.Tasks.Count)
            {
                return false;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                var x = tasks[i];
                var y = _checklist.Tasks[i];

                if (x.Id != y.Id || !x.Text.AreTheSame(y.Text))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public partial class ChecklistTaskViewModel : BindableBase
    {
        private readonly Action<ChecklistTaskViewModel> _remove;

        public ChecklistTaskViewModel(string text, bool readOnly, bool focus)
        {
            Id = -1;

            _text = text.AsFormattedText();
            _isReadOnly = readOnly;
            _focusOnLoaded = focus;
        }

        public ChecklistTaskViewModel(int id, FormattedText text, bool readOnly, bool focus)
        {
            Id = id;

            _text = text;
            _isReadOnly = readOnly;
            _focusOnLoaded = focus;
        }

        public ChecklistTaskViewModel(FormattedText text, bool readOnly, bool focus)
        {
            Id = -1;

            _text = text;
            _isReadOnly = readOnly;
            _focusOnLoaded = focus;
        }

        public int Id { get; set; }

        private FormattedText _text;
        public FormattedText Text
        {
            get => _text;
            set => Set(ref _text, value);
        }

        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => Set(ref _isReadOnly, value);
        }

        private bool _focusOnLoaded;
        public bool FocusOnLoaded
        {
            get => _focusOnLoaded;
            set => Set(ref _focusOnLoaded, value);
        }
    }
}
