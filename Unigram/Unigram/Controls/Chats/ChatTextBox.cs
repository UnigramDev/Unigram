using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Unigram.Native;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Automation;
using Unigram.Entities;
using Telegram.Td.Api;
using Unigram.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Collections;
using Template10.Common;
using System.Windows.Input;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls.Primitives;
using Unigram.Controls.Views;

namespace Unigram.Controls.Chats
{
    public class ChatTextBox : RichEditBox
    {
        private ContentControl InlinePlaceholderTextContentPresenter;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private readonly IDisposable _textChangedSubscription;

        public ChatTextBox()
        {
            DefaultStyleKey = typeof(ChatTextBox);

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            Paste += OnPaste;
            //Clipboard.ContentChanged += Clipboard_ContentChanged;

            SelectionChanged += OnSelectionChanged;
            TextChanged += OnTextChanged;

            var textChangedEvents = Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                keh => { TextChanged += keh; },
                keh => { TextChanged -= keh; });

            _textChangedSubscription = textChangedEvents
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Subscribe(e => this.BeginOnUIThread(() => UpdateInlineBot(true)));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            ContextFlyout = new MenuFlyout();
            ContextFlyout.Opening += OnContextFlyoutOpening;

            //ContextMenuOpening += OnContextMenuOpening;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.RichEditBox", "DisabledFormattingAccelerators"))
            {
                DisabledFormattingAccelerators = DisabledFormattingAccelerators.All;
            }

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAcceleratorPlacementMode"))
            {
                KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

                CreateKeyboardAccelerator(VirtualKey.B);
                CreateKeyboardAccelerator(VirtualKey.I);
                CreateKeyboardAccelerator(VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                CreateKeyboardAccelerator(VirtualKey.K);
                CreateKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            }
        }

        #region Context menu

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void OnContextFlyoutOpening(object sender, object e)
        {
            var flyout = ContextFlyout as MenuFlyout;
            if (flyout == null)
            {
                return;
            }

            flyout.Items.Clear();

            //if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.RichEditBox", "ProofingMenuFlyout") && ProofingMenuFlyout is MenuFlyout proofing && proofing.Items.Count > 0)
            //{
            //    var sub = new MenuFlyoutItem();
            //    sub.Text = "Proofing";
            //    sub.Click += (s, args) =>
            //    {
            //        proofing.ShowAt(this);
            //    };

            //    flyout.Items.Add(sub);
            //    flyout.Items.Add(new MenuFlyoutSeparator());
            //}

            var selection = Document.Selection;
            var format = Document.Selection.CharacterFormat;

            var length = Math.Abs(selection.Length) > 0;

            var clipboard = Clipboard.GetContent();

            var clone = Document.Selection.GetClone();
            clone.StartOf(TextRangeUnit.Link, true);
            var mention = TryGetUserId(clone, out int userId);

            var formatting = new MenuFlyoutSubItem { Text = "Formatting" };
            CreateFlyoutItem(formatting.Items, length && format.Bold == FormatEffect.Off, ContextBold_Click, "Bold", VirtualKey.B);
            CreateFlyoutItem(formatting.Items, length && format.Italic == FormatEffect.Off, ContextItalic_Click, "Italic", VirtualKey.I);
            CreateFlyoutItem(formatting.Items, length && format.Name != "Consolas", ContextMonospace_Click, "Monospace", VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(formatting.Items, !mention, ContextLink_Click, clone.Link.Length > 0 ? "Edit link" : "Create link", VirtualKey.K);
            formatting.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(formatting.Items, length && !IsDefault(format), ContextPlain_Click, "Plain text", VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            CreateFlyoutItem(flyout.Items, Document.CanUndo(), StandardUICommandKind.Undo, "Undo", VirtualKey.Z);
            CreateFlyoutItem(flyout.Items, Document.CanRedo(), StandardUICommandKind.Redo, "Redo", VirtualKey.Y);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), StandardUICommandKind.Cut, "Cut", VirtualKey.X);
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), StandardUICommandKind.Copy, "Copy", VirtualKey.C);
            CreateFlyoutItem(flyout.Items, Document.CanPaste(), StandardUICommandKind.Paste, "Paste", VirtualKey.V);
            CreateFlyoutItem(flyout.Items, length, StandardUICommandKind.Delete, "Delete");
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(formatting);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, !IsEmpty, StandardUICommandKind.SelectAll, "Select All", VirtualKey.A);
        }

        private void ContextBold_Click()
        {
            if (Math.Abs(Document.Selection.Length) < 1)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.Selection.CharacterFormat.Bold = FormatEffect.On;
            Document.ApplyDisplayUpdates();
        }

        private void ContextItalic_Click()
        {
            if (Math.Abs(Document.Selection.Length) < 1)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.Selection.CharacterFormat.Italic = FormatEffect.On;
            Document.ApplyDisplayUpdates();
        }

        private void ContextMonospace_Click()
        {
            if (Math.Abs(Document.Selection.Length) < 1)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.Selection.CharacterFormat.Name = "Consolas";
            Document.ApplyDisplayUpdates();
        }

        private async void ContextLink_Click()
        {
            var range = Document.Selection.GetClone();
            var clone = Document.Selection.GetClone();
            clone.StartOf(TextRangeUnit.Link, true);

            if (clone.Link.Length > 0)
            {
                range.Expand(TextRangeUnit.Link);
            }

            range.GetText(TextGetOptions.NoHidden, out string text);

            var start = Math.Min(range.StartPosition, range.EndPosition);
            var end = Math.Max(range.StartPosition, range.EndPosition);

            var dialog = new CreateLinkView(ViewModel.ProtoService);
            dialog.Text = text;
            dialog.Link = range.Link.Trim('"');

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            range.SetRange(start, end);
            range.CharacterFormat = Document.GetDefaultCharacterFormat();

            range.SetText(end > start ? TextSetOptions.Unlink : TextSetOptions.None, dialog.Text);
            range.SetRange(start, start + dialog.Text.Length);
            range.Link = $"\"{dialog.Link}\"";

            Document.Selection.SetRange(range.EndPosition, range.EndPosition);
            Document.ApplyDisplayUpdates();
        }

        private void ContextPlain_Click()
        {
            if (Math.Abs(Document.Selection.Length) < 1)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.ApplyDisplayUpdates();
        }

        private void ClearStyle(ITextRange range)
        {
            var start = Math.Min(range.StartPosition, range.EndPosition);
            var end = Math.Max(range.StartPosition, range.EndPosition);

            range.SetRange(start, end);
            range.CharacterFormat = Document.GetDefaultCharacterFormat();

            range.GetText(TextGetOptions.NoHidden, out string text);
            range.SetText(TextSetOptions.Unlink, text);
            range.SetRange(start, start + text.Length);
        }

        private bool IsDefault(ITextCharacterFormat format)
        {
            return IsEqual(format, Document.GetDefaultCharacterFormat());
        }

        private bool IsEqual(ITextCharacterFormat format, ITextCharacterFormat document)
        {
            return document.AllCaps == format.AllCaps &&
                document.BackgroundColor == format.BackgroundColor &&
                document.Bold == format.Bold &&
                document.FontStretch == format.FontStretch &&
                document.FontStyle == format.FontStyle &&
                document.ForegroundColor == format.ForegroundColor &&
                document.Hidden == format.Hidden &&
                document.Italic == format.Italic &&
                document.Kerning == format.Kerning &&
                //document.LanguageTag == format.LanguageTag &&
                document.LinkType == format.LinkType &&
                document.Name == format.Name &&
                document.Outline == format.Outline &&
                document.Position == format.Position &&
                document.ProtectedText == format.ProtectedText &&
                document.Size == format.Size &&
                document.SmallCaps == format.SmallCaps &&
                document.Spacing == format.Spacing &&
                document.Strikethrough == format.Strikethrough &&
                document.Subscript == format.Subscript &&
                document.Superscript == format.Superscript &&
                //document.TextScript == format.TextScript &&
                document.Underline == format.Underline &&
                document.Weight == format.Weight;
        }

        private bool TryGetUserId(ITextRange range, out int userId)
        {
            var link = range.Link.Trim('"');
            if (link.StartsWith("tg-user://") && int.TryParse(link.Substring("tg-user://".Length), out userId))
            {
                return true;
            }

            userId = 0;
            return false;
        }

        private void ContextUndo_Click()
        {
            Document.Undo();
        }

        private void ContextRedo_Click()
        {
            Document.Redo();
        }

        private void ContextCut_Click()
        {
            Document.Selection.Cut();
        }

        private void ContextCopy_Click()
        {
            Document.Selection.Copy();
        }

        private void ContextPaste_Click()
        {
            //Document.Selection.Paste(0);
            OnPaste(this, null);
        }

        private void ContextDelete_Click()
        {
            Document.Selection.SetText(TextSetOptions.None, string.Empty);
        }

        private void ContextSelectAll_Click()
        {
            Document.Selection.Expand(TextRangeUnit.Paragraph);
        }

        private void CreateFlyoutItem(IList<MenuFlyoutItemBase> flyout, bool create, Action command, string text, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = create;
            flyoutItem.Command = new RelayCommand(command);
            flyoutItem.Text = text;

            if (key.HasValue && ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators"))
            {
                flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
            }

            flyout.Add(flyoutItem);
        }

        private void CreateFlyoutItem(IList<MenuFlyoutItemBase> flyout, bool create, StandardUICommandKind kind, string text, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = create;

            RelayCommand command = null;
            switch (kind)
            {
                case StandardUICommandKind.Undo:
                    command = new RelayCommand(ContextUndo_Click);
                    break;
                case StandardUICommandKind.Redo:
                    command = new RelayCommand(ContextRedo_Click);
                    break;
                case StandardUICommandKind.Cut:
                    command = new RelayCommand(ContextCut_Click);
                    break;
                case StandardUICommandKind.Copy:
                    command = new RelayCommand(ContextCopy_Click);
                    break;
                case StandardUICommandKind.Paste:
                    command = new RelayCommand(ContextPaste_Click);
                    break;
                case StandardUICommandKind.Delete:
                    command = new RelayCommand(ContextDelete_Click);
                    break;
                case StandardUICommandKind.SelectAll:
                    command = new RelayCommand(ContextSelectAll_Click);
                    break;
            }

            //if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Input.StandardUICommand"))
            //{
            //    var standard = new StandardUICommand(kind) { Command = command, IconSource = null };
            //    standard.KeyboardAccelerators.Clear();

            //    flyoutItem.Command = standard;
            //}
            //else
            {
                flyoutItem.Command = command;
                flyoutItem.Text = text;
            }

            if (key.HasValue && ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators"))
            {
                flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
            }

            flyout.Add(flyoutItem);
        }

        private void CreateKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators"))
            {
                var accelerator = new KeyboardAccelerator { Modifiers = modifiers, Key = key, ScopeOwner = this };
                accelerator.Invoked += FlyoutAccelerator_Invoked;

                KeyboardAccelerators.Add(accelerator);
            }
        }

        private void FlyoutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;

            var selection = Document.Selection;
            var format = Document.Selection.CharacterFormat;

            var length = Math.Abs(selection.Length) > 0;

            if (sender.Key == VirtualKey.B && sender.Modifiers == VirtualKeyModifiers.Control)
            {
                ContextBold_Click();
            }
            else if (sender.Key == VirtualKey.I && sender.Modifiers == VirtualKeyModifiers.Control && length && format.Italic == FormatEffect.Off)
            {
                ContextItalic_Click();
            }
            else if (sender.Key == VirtualKey.M && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && format.Name != "Consolas")
            {
                ContextMonospace_Click();
            }
            else if (sender.Key == VirtualKey.K && sender.Modifiers == VirtualKeyModifiers.Control)
            {
                ContextLink_Click();
            }
            else if (sender.Key == VirtualKey.N && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && !IsDefault(format))
            {
                ContextPlain_Click();
            }
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (ContentControl)GetTemplateChild("InlinePlaceholderTextContentPresenter");

            base.OnApplyTemplate();
        }

        public void InsertText(string text, bool allowPreceding = true, bool allowTrailing = true)
        {
            var start = Document.Selection.StartPosition;
            var end = Document.Selection.EndPosition;

            var preceding = start > 0 && !char.IsWhiteSpace(Document.GetRange(start - 1, start).Character);
            var trailing = !char.IsWhiteSpace(Document.GetRange(end, end + 1).Character) || Document.GetRange(end, end + 1).Character == '\r';

            var block = string.Format("{0}{1}{2}",
                preceding && allowPreceding ? " " : "",
                text,
                trailing && allowTrailing ? " " : "");

            Document.Selection.SetText(TextSetOptions.None, block);
            Document.Selection.StartPosition = Document.Selection.EndPosition;
        }

        public event EventHandler<TappedRoutedEventArgs> Capture;

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            Capture?.Invoke(this, e);
            base.OnTapped(e);
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
            // we have to handle the pasting operation manually to allow plaintext only.
            var package = Clipboard.GetContent();
            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);

                    var photo = await StoragePhoto.CreateAsync(cache, true) as StorageMedia;
                    if (photo == null)
                    {
                        photo = await StorageVideo.CreateAsync(cache, true);
                    }

                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = new FormattedText(await package.GetTextAsync(), new TextEntity[0]);
                }

                ViewModel.SendMediaExecute(media, media[0]);
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.WebLink))
            {

            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (StorageFile file in items)
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        var photo = await StoragePhoto.CreateAsync(file, true);
                        if (photo != null)
                        {
                            media.Add(photo);
                        }
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        var video = await StorageVideo.CreateAsync(file, true);
                        if (video != null)
                        {
                            media.Add(video);
                        }
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    ViewModel.SendMediaExecute(media, media[0]);
                }
                else if (files.Count > 0)
                {
                    ViewModel.SendFileExecute(files);
                }
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-tl-field-tags"))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                // This is our field format
                var text = await package.GetTextAsync();
                var data = await package.GetDataAsync("application/x-tl-field-tags") as IRandomAccessStream;
                var reader = new DataReader(data.GetInputStreamAt(0));
                var length = await reader.LoadAsync((uint)data.Size);

                var count = reader.ReadInt32();
                var entities = new List<TextEntity>(count);

                for (int i = 0; i < count; i++)
                {
                    var entity = new TextEntity { Offset = reader.ReadInt32(), Length = reader.ReadInt32() };
                    var type = reader.ReadByte();

                    switch (type)
                    {
                        case 1:
                            entity.Type = new TextEntityTypeBold();
                            break;
                        case 2:
                            entity.Type = new TextEntityTypeItalic();
                            break;
                        case 3:
                            entity.Type = new TextEntityTypePreCode();
                            break;
                        case 4:
                            entity.Type = new TextEntityTypeTextUrl { Url = reader.ReadString(reader.ReadUInt32()) };
                            break;
                        case 5:
                            entity.Type = new TextEntityTypeMentionName { UserId = reader.ReadInt32() };
                            break;
                    }

                    entities.Add(entity);
                }

                SetText(text, entities);
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-td-field-tags"))
            {
                // This is Telegram Desktop mentions format
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) /*&& package.Contains("Rich Text Format")*/)
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var text = await package.GetTextAsync();
                var start = Document.Selection.StartPosition;

                var result = Emoticon.Pattern.Replace(text, (match) =>
                {
                    var emoticon = match.Groups[1].Value;
                    var emoji = Emoticon.Replace(emoticon);
                    if (match.Value.StartsWith(" "))
                    {
                        emoji = $" {emoji}";
                    }

                    return emoji;
                });

                Document.Selection.SetText(TextSetOptions.None, result);
                Document.Selection.SetRange(start + result.Length, start + result.Length);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            //if (Document.Selection.Length != 0)
            //{
            //    Document.Selection.GetRect(PointOptions.ClientCoordinates, out Rect rect, out int hit);
            //    _flyout.ShowAt(this, new Point(rect.X + 12, rect.Y - _presenter?.ActualHeight ?? 0));
            //}
            //else
            //{
            //    _flyout.Hide();
            //}
        }

        //protected override async void OnKeyDown(KeyRoutedEventArgs e)
        //{
        //    if (e.Key == VirtualKey.Enter)
        //    {
        //        // Check if CTRL or Shift is also pressed in addition to Enter key.
        //        var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
        //        var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

        //        // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
        //        if (!ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down) && !IsEmpty)
        //        {
        //            e.Handled = true;
        //            await SendAsync();
        //        }
        //    }

        //    base.OnKeyDown(e);
        //}

        private async void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if ((args.VirtualKey == VirtualKey.Enter || args.VirtualKey == VirtualKey.Tab) && args.EventType == CoreAcceleratorKeyEventType.KeyDown && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                var key = Window.Current.CoreWindow.GetKeyState(VirtualKey.Enter);

                if (Autocomplete != null && ViewModel.Autocomplete != null && Autocomplete.Items.Count > 0)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send || args.VirtualKey == VirtualKey.Tab)
                    {
                        AcceptsReturn = false;
                        var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as ListViewItem;
                        if (container != null)
                        {
                            var peer = new ListViewItemAutomationPeer(container);
                            var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            provider.Invoke();
                        }
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }

                    return;
                }

                // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                if (ViewModel.Settings.IsSendByEnterEnabled)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        await SendAsync();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
                else
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        await SendAsync();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
            }
        }

        public ListView Messages { get; set; }
        public ListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                if (Document.Selection.Length > 0)
                {
                    return;
                }

                FormatText();

                Document.GetText(TextGetOptions.NoHidden, out string text);

                if (MessageHelper.IsValidUsername(text))
                {
                    ViewModel.ResolveInlineBot(text);
                }

                var clone = Document.Selection.GetClone();
                var end = clone.EndOf(TextRangeUnit.CharacterFormat, true);

                if (clone.EndPosition > Document.Selection.EndPosition && IsEqual(clone.CharacterFormat, Document.Selection.CharacterFormat))
                {

                }
                else
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }
            }
            else if ((e.Key == VirtualKey.Up || e.Key == VirtualKey.Down || e.Key == VirtualKey.PageUp || e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Tab))
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (e.Key == VirtualKey.Up && !alt && !ctrl && !shift && IsEmpty)
                {
                    ViewModel.MessageEditLastCommand.Execute();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Up && ctrl)
                {
                    ViewModel.MessageReplyPreviousCommand.Execute();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Down && ctrl)
                {
                    ViewModel.MessageReplyNextCommand.Execute();
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.Up && alt) || (e.Key == VirtualKey.PageUp && ctrl) || (e.Key == VirtualKey.Tab && ctrl && shift))
                {
                    //ViewModel.Aggregator.Publish("move_up");
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.Down && alt) || (e.Key == VirtualKey.PageDown && ctrl) || (e.Key == VirtualKey.Tab && ctrl && !shift))
                {
                    //ViewModel.Aggregator.Publish("move_down");
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.PageUp || e.Key == VirtualKey.Up) && Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
                {
                    var peer = new ListViewAutomationPeer(Messages);
                    var provider = peer.GetPattern(PatternInterface.Scroll) as IScrollProvider;
                    if (provider.VerticallyScrollable)
                    {
                        provider.Scroll(ScrollAmount.NoAmount, e.Key == VirtualKey.Up ? ScrollAmount.SmallDecrement : ScrollAmount.LargeDecrement);

                        e.Handled = true;
                    }
                }
                else if ((e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Down) && Document.Selection.StartPosition == Text.TrimEnd('\r', '\v').Length && ViewModel.Autocomplete == null)
                {
                    var peer = new ListViewAutomationPeer(Messages);
                    var provider = peer.GetPattern(PatternInterface.Scroll) as IScrollProvider;
                    if (provider.VerticallyScrollable)
                    {
                        provider.Scroll(ScrollAmount.NoAmount, e.Key == VirtualKey.Down ? ScrollAmount.SmallIncrement : ScrollAmount.LargeIncrement);

                        e.Handled = true;
                    }
                }
                else if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
                {
                    if (Autocomplete != null && ViewModel.Autocomplete != null)
                    {
                        Autocomplete.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Up ? -1 : 1;
                        var next = Autocomplete.SelectedIndex + index;
                        if (next >= 0 && next < ViewModel.Autocomplete.Count)
                        {
                            Autocomplete.SelectedIndex = next;
                            Autocomplete.ScrollIntoView(Autocomplete.SelectedItem);
                        }

                        e.Handled = true;
                    }
                }
                else if (e.Key == VirtualKey.Tab && Autocomplete != null && ViewModel.Autocomplete != null)
                {
                    e.Handled = true;
                }
            }
            //else if (e.Key == VirtualKey.Escape && ViewModel.Reply is TLMessagesContainter container && container.EditMessage != null)
            //{
            //    ViewModel.ClearReplyCommand.Execute();
            //    e.Handled = true;
            //}

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            AcceptsReturn = false;
            UpdateText();
            UpdateInlineBot(false);

            //string result;
            //if (SearchByStickers(this.Text, out result))
            //{
            //    this.GetStickerHints(result);
            //}
            //else
            //{
            //    this.ClearStickerHints();
            //}

            //if (SearchInlineBotResults(this.Text, out result))
            //{
            //    this.GetInlineBotResults(result);
            //}
            //else
            //{
            //    this.ClearInlineBotResults();
            //}

            //if (SearchByUsernames(this.Text, out result))
            //{
            //    this.GetUsernameHints(result);
            //}
            //else
            //{
            //    this.ClearUsernameHints();
            //}

            //if (SearchByCommands(this.Text, out result))
            //{
            //    this.GetCommandHints(result);
            //}
            //else
            //{
            //    this.ClearCommandHints();
            //}

            if (IsEmpty == false)
            {
                ViewModel.ChatActionManager.SetTyping(new ChatActionTyping());
            }
        }

        private void UpdateInlineBot(bool fast)
        {
            //var text = Text.Substring(0, Math.Max(Document.Selection.StartPosition, Document.Selection.EndPosition));
            var text = Text;
            var query = string.Empty;
            var inline = SearchInlineBotResults(text, out query);
            if (inline && fast)
            {
                ViewModel.Autocomplete = null;
                ViewModel.GetInlineBotResults(query);
            }
            else if (!inline)
            {
                ViewModel.CurrentInlineBot = null;
                ViewModel.InlineBotResults = null;
                InlinePlaceholderText = string.Empty;

                if (fast)
                {
                    if (Emoji.ContainsSingleEmoji(text) && !string.IsNullOrWhiteSpace(text) && ViewModel.EditedMessage == null)
                    {
                        ViewModel.StickerPack = new SearchStickersCollection(ViewModel.ProtoService, ViewModel.Settings, text.Trim());
                    }
                    else
                    {
                        ViewModel.StickerPack = null;
                    }
                }
                else
                {
                    ViewModel.StickerPack = null;

                    if (SearchByUsername(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string username, out int index))
                    {
                        var chat = ViewModel.Chat;
                        if (chat == null)
                        {
                            return;
                        }

                        var members = true;
                        if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret || chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                        {
                            members = false;
                        }

                        ViewModel.Autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Chat.Id, username, index == 0, members);
                    }
                    else if (SearchByHashtag(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string hashtag, out int index2))
                    {
                        ViewModel.Autocomplete = new SearchHashtagsCollection(ViewModel.ProtoService, hashtag);
                    }
                    else if (SearchByEmoji(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string replacement) && replacement.Length > 0)
                    {
                        ViewModel.Autocomplete = EmojiSuggestion.GetSuggestions(replacement.Length < 2 ? replacement : replacement.ToLower());
                    }
                    else if (text.Length > 0 && text[0] == '/' && SearchByCommand(text, out string command))
                    {
                        ViewModel.Autocomplete = GetCommands(command.ToLower());
                    }
                    else
                    {
                        ViewModel.Autocomplete = null;
                    }
                }
            }
        }

        private List<UserCommand> GetCommands(string command)
        {
            var all = ViewModel.BotCommands;
            if (all != null)
            {
                var results = all.Where(x => x.Item.Command.ToLower().StartsWith(command, StringComparison.OrdinalIgnoreCase)).ToList();
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return null;
        }

        public class UsernameCollection : MvxObservableCollection<Telegram.Td.Api.User>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly long _chatId;
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _members;

            private bool _hasMore = true;

            public UsernameCollection(IProtoService protoService, long chatId, string query, bool bots, bool members)
            {
                _protoService = protoService;
                _chatId = chatId;
                _query = query;

                _bots = bots;
                _members = members;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    count = 0;
                    _hasMore = false;

                    if (_bots)
                    {
                        var response = await _protoService.SendAsync(new GetTopChats(new TopChatCategoryInlineBots(), 10));
                        if (response is Telegram.Td.Api.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var user = _protoService.GetUser(_protoService.GetChat(id));
                                if (user != null && user.Username.StartsWith(_query, StringComparison.OrdinalIgnoreCase))
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    if (_members)
                    {
                        var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 20, null));
                        if (response is ChatMembers members)
                        {
                            foreach (var member in members.Members)
                            {
                                var user = _protoService.GetUser(member.UserId);
                                if (user != null)
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;
        }

        public class SearchHashtagsCollection : MvxObservableCollection<string>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;

            private bool _hasMore = true;

            public SearchHashtagsCollection(IProtoService protoService, string query)
            {
                _protoService = protoService;
                _query = query;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    count = 0;
                    _hasMore = false;

                    var response = await _protoService.SendAsync(new SearchHashtags(_query, 20));
                    if (response is Hashtags hashtags)
                    {
                        foreach (var value in hashtags.HashtagsValue)
                        {
                            Add("#" + value);
                            count++;
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;
        }

        public static bool SearchByCommand(string text, out string searchText)
        {
            searchText = string.Empty;

            var c = '/';
            var flag = true;
            var index = -1;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == c)
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }
                    flag = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidCommandSymbol(text[i]))
                    {
                        flag = false;
                        break;
                    }
                    i--;
                }
            }
            if (flag)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart(c);
            }

            return flag;
        }

        public static bool SearchByEmoji(string text, out string searchText)
        {
            searchText = string.Empty;

            var c = ':';
            var flag = true;
            var index = -1;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == c)
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }
                    flag = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidCommandSymbol(text[i]))
                    {
                        flag = false;
                        break;
                    }
                    i--;
                }
            }
            if (flag)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart(c);
            }

            return flag;
        }

        private void UpdateText()
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);
            Text = text;
        }

        private void FormatText()
        {
            if (!ViewModel.Settings.IsReplaceEmojiEnabled)
            {
                return;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            var caretPosition = Document.Selection.StartPosition;
            var result = Emoticon.Pattern.Matches(text);

            Document.BatchDisplayUpdates();

            foreach (Match match in result)
            {
                var emoticon = match.Groups[1].Value;
                var emoji = Emoticon.Replace(emoticon);
                if (match.Index + match.Length < caretPosition)
                {
                    caretPosition += emoji.Length - emoticon.Length;
                }
                if (match.Value.StartsWith(" "))
                {
                    emoji = $" {emoji}";
                }

                Document.GetRange(match.Index, match.Index + match.Length).SetText(TextSetOptions.None, emoji);
            }

            Document.ApplyDisplayUpdates();
            Document.Selection.SetRange(caretPosition, caretPosition);
        }

        public async Task SendAsync()
        {
            await ViewModel.SendMessageAsync(GetFormattedText(true));
        }

        public FormattedText GetFormattedText(bool clear = false)
        {
            FormatText();

            Document.BatchDisplayUpdates();

            var entities = new List<TextEntity>();
            var adjust = 0;

            var end = false;
            for (int i = 0; !end; i++)
            {
                var range = Document.GetRange(i, i + 1);
                if (range.Expand(TextRangeUnit.Bold) > 0)
                {
                    entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeBold() });
                }
                else if (range.Expand(TextRangeUnit.Italic) > 0)
                {
                    entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeItalic() });
                }
                else if (range.Expand(TextRangeUnit.Link) > 0)
                {
                    range.GetText(TextGetOptions.NoHidden, out string value);

                    if (TryGetUserId(range, out int userId))
                    {
                        entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = value.Length, Type = new TextEntityTypeMentionName { UserId = userId } });
                    }
                    else
                    {
                        entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = value.Length, Type = new TextEntityTypeTextUrl { Url = range.Link.Trim('"') } });
                    }

                    adjust += Math.Abs(range.Length) - value.Length;
                }
                else if (range.Expand(TextRangeUnit.CharacterFormat) > 0)
                {
                    range.GetText(TextGetOptions.NoHidden, out string value);

                    if (range.CharacterFormat.Name.Equals("Consolas"))
                    {
                        if (value.Contains('\v') || value.Contains('\r'))
                        {
                            entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypePre() });
                        }
                        else
                        {
                            entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeCode() });
                        }
                    }
                    else if (value.Length > 0)
                    {
                        var sub = Markdown.Parse(ViewModel.ProtoService, ref value);
                        if (sub != null && sub.Count > 0)
                        {
                            range.SetText(TextSetOptions.None, value);

                            foreach (var entity in sub)
                            {
                                entity.Offset = range.StartPosition + entity.Offset;
                                entities.Add(entity);
                            }
                        }
                    }
                }

                end = i >= range.EndPosition;
                i = range.EndPosition;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (clear)
            {
                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }

            Document.ApplyDisplayUpdates();

            return new FormattedText(text.Replace('\v', '\n').Replace('\r', '\n'), entities);
        }

        public bool IsEmpty
        {
            get
            {
                var isEmpty = string.IsNullOrWhiteSpace(Text);
                if (isEmpty)
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }

                return isEmpty;
            }
        }

        #region Username

        public static bool SearchByUsername(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == '@')
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i--;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart('@');
            }

            return found;
        }

        public static bool SearchByHashtag(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == '#')
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i--;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart('#');
            }

            return found;
        }

        #endregion

        #region Inline bots

        private bool SearchInlineBotResults(string text, out string searchText)
        {
            var flag = false;
            searchText = string.Empty;

            if (ViewModel.CurrentInlineBot != null)
            {
                var username = ViewModel.CurrentInlineBot.Username;
                if (text != null && text.TrimStart().StartsWith("@" + username, StringComparison.OrdinalIgnoreCase))
                {
                    searchText = ReplaceFirst(text.TrimStart(), "@" + username, string.Empty);
                    if (searchText.StartsWith(" "))
                    {
                        searchText = ReplaceFirst(searchText, " ", string.Empty);
                        flag = true;
                    }

                    if (!flag)
                    {
                        if (string.Equals(text.TrimStart(), "@" + username, StringComparison.OrdinalIgnoreCase))
                        {
                            ViewModel.CurrentInlineBot = null;
                            ViewModel.InlineBotResults = null;
                            InlinePlaceholderText = string.Empty;
                        }
                        else
                        {
                            var user = ViewModel.CurrentInlineBot;
                            if (user != null && user.Type is UserTypeBot bot)
                            {
                                InlinePlaceholderText = bot.InlineQueryPlaceholder;
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(searchText))
                    {
                        var user = ViewModel.CurrentInlineBot;
                        if (user != null && user.Type is UserTypeBot bot)
                        {
                            InlinePlaceholderText = bot.InlineQueryPlaceholder;
                        }
                    }
                    else
                    {
                        InlinePlaceholderText = string.Empty;
                    }
                }
                else
                {
                    ViewModel.CurrentInlineBot = null;
                    ViewModel.InlineBotResults = null;
                    InlinePlaceholderText = string.Empty;
                }
            }

            return flag;
        }

        public string ReplaceFirst(string text, string search, string replace)
        {
            var index = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return text;
            }

            return text.Substring(0, index) + replace + text.Substring(index + search.Length);
        }

        private void UpdateInlinePlaceholder()
        {
            if (InlinePlaceholderTextContentPresenter != null)
            {
                var placeholder = Text;
                if (!placeholder.EndsWith(" "))
                {
                    placeholder += " ";
                }

                var range = Document.GetRange(Text.Length, Text.Length);
                range.GetRect(PointOptions.ClientCoordinates, out Rect rect, out int hit);

                var translateTransform = new TranslateTransform();
                translateTransform.X = rect.X;
                InlinePlaceholderTextContentPresenter.RenderTransform = translateTransform;
            }
        }

        #endregion

        public string Text { get; private set; }

        #region InlinePlaceholderText

        public string InlinePlaceholderText
        {
            get { return (string)GetValue(InlinePlaceholderTextProperty); }
            set { SetValue(InlinePlaceholderTextProperty, value); }
        }

        public static readonly DependencyProperty InlinePlaceholderTextProperty =
            DependencyProperty.Register("InlinePlaceholderText", typeof(string), typeof(ChatTextBox), new PropertyMetadata(null, OnInlinePlaceholderTextChanged));

        private static void OnInlinePlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatTextBox)d).UpdateInlinePlaceholder();
        }

        #endregion

        #region Reply

        public object Reply
        {
            get { return (object)GetValue(ReplyProperty); }
            set { SetValue(ReplyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Reply.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ReplyProperty =
            DependencyProperty.Register("Reply", typeof(object), typeof(ChatTextBox), new PropertyMetadata(null, OnReplyChanged));

        private static void OnReplyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatTextBox)d).OnReplyChanged((object)e.NewValue, (object)e.OldValue);
        }

        private async void OnReplyChanged(object newValue, object oldValue)
        {
            if (newValue != null)
            {
                await Task.Delay(200);
                Focus(FocusState.Keyboard);
            }
        }

        #endregion

        public void SetText(string text, IList<TextEntity> entities)
        {
            Document.BatchDisplayUpdates();
            Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());

            if (!string.IsNullOrEmpty(text))
            {
                Document.SetText(TextSetOptions.None, text);

                if (entities != null && entities.Count > 0)
                {
                    // We want to enumerate entities from last to first to not
                    // fuck up ranges due to hidden texts when formatting a link
                    foreach (var entity in entities.Reverse())
                    {
                        var range = Document.GetRange(entity.Offset, entity.Offset + entity.Length);

                        if (entity.Type is TextEntityTypeBold)
                        {
                            range.CharacterFormat.Bold = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeItalic)
                        {
                            range.CharacterFormat.Italic = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeCode || entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                        {
                            range.CharacterFormat.Name = "Consolas";
                        }
                        else if (entity.Type is TextEntityTypeTextUrl textUrl)
                        {
                            range.Link = $"\"{textUrl.Url}\"";
                        }
                        else if (entity.Type is TextEntityTypeMentionName mentionName)
                        {
                            range.Link = $"\"tg-user://{mentionName.UserId}\"";
                        }
                    }
                }

                // We need to get full text as hidden content has been added and we don't want to hardcode lengths
                Document.GetText(TextGetOptions.None, out string result);
                Document.Selection.SetRange(result.Length, result.Length);
            }

            Document.ApplyDisplayUpdates();
        }
    }
}
