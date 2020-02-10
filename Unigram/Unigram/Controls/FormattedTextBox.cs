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
using Unigram.Converters;
using Unigram.Controls.Chats;

namespace Unigram.Controls
{
    public class FormattedTextBox : RichEditBox
    {
        public FormattedTextBox()
        {
            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

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
                CreateKeyboardAccelerator(VirtualKey.U);
                CreateKeyboardAccelerator(VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                CreateKeyboardAccelerator(VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                CreateKeyboardAccelerator(VirtualKey.K);
                CreateKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            }
        }

        public event TypedEventHandler<FormattedTextBox, EventArgs> ShowFormatting;
        public event TypedEventHandler<FormattedTextBox, EventArgs> HideFormatting;

        private bool _isFormattingVisible = false;
        public bool IsFormattingVisible
        {
            get => _isFormattingVisible;
            set
            {
                if (_isFormattingVisible != value)
                {
                    _isFormattingVisible = value;

                    if (value)
                    {
                        ShowFormatting?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        HideFormatting?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        private ChatTextFormatting _formatting;
        public ChatTextFormatting Formatting
        {
            get => _formatting;
            set => _formatting = value;
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
            CreateFlyoutItem(formatting.Items, length && format.Bold == FormatEffect.Off, ContextBold_Click, Strings.Resources.Bold, new FontIcon { Glyph = Icons.Bold }, VirtualKey.B);
            CreateFlyoutItem(formatting.Items, length && format.Italic == FormatEffect.Off, ContextItalic_Click, Strings.Resources.Italic, new FontIcon { Glyph = Icons.Italic }, VirtualKey.I);
            CreateFlyoutItem(formatting.Items, length && format.Underline == UnderlineType.None, ContextUnderline_Click, Strings.Resources.Underline, new FontIcon { Glyph = Icons.Underline }, VirtualKey.U);
            CreateFlyoutItem(formatting.Items, length && format.Strikethrough == FormatEffect.Off, ContextStrikethrough_Click, Strings.Resources.Strike, new FontIcon { Glyph = Icons.Strikethrough, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily }, VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            CreateFlyoutItem(formatting.Items, length && format.Name != "Consolas", ContextMonospace_Click, Strings.Resources.Mono, new FontIcon { Glyph = Icons.Monospace }, VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(formatting.Items, !mention, ContextLink_Click, clone.Link.Length > 0 ? "Edit link" : Strings.Resources.CreateLink, new FontIcon { Glyph = Icons.Link }, VirtualKey.K);
            formatting.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(formatting.Items, length && !IsDefault(format), ContextPlain_Click, Strings.Resources.Regular, null, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(formatting.Items, true, () => IsFormattingVisible = !_isFormattingVisible, _isFormattingVisible ? "Hide formatting" : "Show formatting", new FontIcon { Glyph = "\uE8D2" });

            CreateFlyoutItem(flyout.Items, Document.CanUndo(), ContextUndo_Click, "Undo", new FontIcon { Glyph = Icons.Undo }, VirtualKey.Z);
            CreateFlyoutItem(flyout.Items, Document.CanRedo(), ContextRedo_Click, "Redo", new FontIcon { Glyph = Icons.Redo }, VirtualKey.Y);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), ContextCut_Click, "Cut", new FontIcon { Glyph = Icons.Cut }, VirtualKey.X);
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), ContextCopy_Click, "Copy", new FontIcon { Glyph = Icons.Copy }, VirtualKey.C);
            CreateFlyoutItem(flyout.Items, Document.CanPaste(), ContextPaste_Click, "Paste", new FontIcon { Glyph = Icons.Paste }, VirtualKey.V);
            CreateFlyoutItem(flyout.Items, length, ContextDelete_Click, "Delete");
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(formatting);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, !IsEmpty, ContextSelectAll_Click, "Select All", null, VirtualKey.A);
        }

        public void ToggleBold()
        {
            ContextBold_Click();
        }

        private void ContextBold_Click()
        {
            //if (Math.Abs(Document.Selection.Length) < 1)
            //{
            //    return;
            //}

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _formatting?.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleItalic()
        {
            ContextItalic_Click();
        }

        private void ContextItalic_Click()
        {
            //if (Math.Abs(Document.Selection.Length) < 1)
            //{
            //    return;
            //}

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _formatting?.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleUnderline()
        {
            ContextUnderline_Click();
        }

        private void ContextUnderline_Click()
        {
            //if (Math.Abs(Document.Selection.Length) < 1)
            //{
            //    return;
            //}

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Underline = Document.Selection.CharacterFormat.Underline != UnderlineType.Single ? UnderlineType.Single : UnderlineType.None;
            Document.ApplyDisplayUpdates();

            _formatting?.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleStrikethrough()
        {
            ContextStrikethrough_Click();
        }

        private void ContextStrikethrough_Click()
        {
            //if (Math.Abs(Document.Selection.Length) < 1)
            //{
            //    return;
            //}

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Strikethrough = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _formatting?.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleMonospace()
        {
            ContextMonospace_Click();
        }

        private void ContextMonospace_Click()
        {
            //if (Math.Abs(Document.Selection.Length) < 1)
            //{
            //    return;
            //}

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, true);
            Document.Selection.CharacterFormat.Name = "Consolas";
            Document.ApplyDisplayUpdates();

            _formatting?.Update(Document.Selection.CharacterFormat);
        }

        public void CreateLink()
        {
            ContextLink_Click();
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

            var dialog = new CreateLinkView();
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
            ClearStyle(Document.Selection, true);
            Document.ApplyDisplayUpdates();
        }

        private void ClearStyle(ITextRange range, bool monospace)
        {
            if (monospace)
            {
                var start = Math.Min(range.StartPosition, range.EndPosition);
                var end = Math.Max(range.StartPosition, range.EndPosition);

                range.SetRange(start, end);
                range.CharacterFormat = Document.GetDefaultCharacterFormat();

                range.GetText(TextGetOptions.NoHidden, out string text);
                range.SetText(TextSetOptions.Unlink, text);
                range.SetRange(start, start + text.Length);
            }
            else
            {
                range.CharacterFormat.Name = Document.GetDefaultCharacterFormat().Name;
            }
        }

        protected bool IsDefault(ITextCharacterFormat format)
        {
            return IsEqual(format, Document.GetDefaultCharacterFormat());
        }

        protected bool IsEqual(ITextCharacterFormat format, ITextCharacterFormat document)
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

        protected bool TryGetUserId(ITextRange range, out int userId)
        {
            var link = range.Link.Trim('"');
            if (link.StartsWith("tg-user://") && int.TryParse(link.Substring("tg-user://".Length), out userId))
            {
                return true;
            }

            userId = 0;
            return false;
        }

        protected bool TryGetEntityType(string link, out TextEntityType type)
        {
            link = link.Trim('"');

            if (Uri.TryCreate(link, UriKind.Absolute, out Uri result))
            {
                if (string.Equals(result.Scheme, "tg-user") && int.TryParse(result.Host, out int userId))
                {
                    type = new TextEntityTypeMentionName(userId);
                    return true;
                }

                type = new TextEntityTypeTextUrl(link);
                return true;
            }

            type = null;
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
            OnPaste();
        }

        protected virtual void OnPaste()
        {
            Document.Selection.Paste(0);
        }

        private void ContextDelete_Click()
        {
            Document.Selection.SetText(TextSetOptions.None, string.Empty);
        }

        private void ContextSelectAll_Click()
        {
            Document.Selection.Expand(TextRangeUnit.Paragraph);
        }

        private void CreateFlyoutItem(IList<MenuFlyoutItemBase> flyout, bool create, Action command, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = create;
            flyoutItem.Command = new RelayCommand(command);
            flyoutItem.Text = text;

            if (icon != null)
            {
                flyoutItem.Icon = icon;
            }

            if (key.HasValue && ApiInfo.CanUseAccelerators)
            {
                flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
            }

            flyout.Add(flyoutItem);
        }

        private void CreateKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            if (ApiInfo.CanUseAccelerators)
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
            else if (sender.Key == VirtualKey.U && sender.Modifiers == VirtualKeyModifiers.Control && length && format.Underline == UnderlineType.None)
            {
                ContextUnderline_Click();
            }
            else if (sender.Key == VirtualKey.X && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && format.Strikethrough == FormatEffect.Off)
            {
                ContextStrikethrough_Click();
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

        public FormattedText GetFormattedText(bool clear = false)
        {
            OnGettingFormattedText();

            Document.BatchDisplayUpdates();
            Document.GetText(TextGetOptions.None, out string value);

            value = value.TrimEnd('\v', '\r');

            var builder = new StringBuilder(value);
            var runs = new List<TextStyleRun>();

            var last = default(TextStyleRun);
            var type = default(TextEntityType);

            var hidden = 0;

            for (int i = 0; i < value.Length; i++)
            {
                var range = Document.GetRange(i, i + 1);
                var flags = default(TextStyle);

                if (string.Equals(range.CharacterFormat.Name, "Consolas", StringComparison.OrdinalIgnoreCase))
                {
                    flags = TextStyle.Monospace;
                }
                else
                {
                    if (range.CharacterFormat.Hidden == FormatEffect.On)
                    {
                        builder.Remove(i - hidden, 1);

                        hidden++;
                        continue;
                    }

                    if (range.CharacterFormat.Bold == FormatEffect.On)
                    {
                        flags |= TextStyle.Bold;
                    }
                    if (range.CharacterFormat.Italic == FormatEffect.On)
                    {
                        flags |= TextStyle.Italic;
                    }
                    if (range.CharacterFormat.Strikethrough == FormatEffect.On)
                    {
                        flags |= TextStyle.Strikethrough;
                    }
                    if (range.CharacterFormat.Underline == UnderlineType.Single)
                    {
                        flags |= TextStyle.Underline;
                    }

                    if (range.Link.Length > 0 && TryGetEntityType(range.Link, out TextEntityType linkType))
                    {
                        flags |= TextStyle.Url;
                        flags &= ~TextStyle.Underline;

                        type = linkType;
                    }
                }

                if (last != null && last.Flags == flags)
                {
                    last.End++;
                }
                else
                {
                    if (last != null)
                    {
                        runs.Add(last);
                        last = null;
                    }

                    if (flags != 0)
                    {
                        last = new TextStyleRun { Start = i - hidden, End = i - hidden + 1, Flags = flags, Type = type };
                        type = null;
                    }
                }
            }

            if (last != null)
            {
                runs.Add(last);
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (clear)
            {
                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }

            Document.ApplyDisplayUpdates();

            var entities = TextStyleRun.GetEntities(text, runs);
            if (entities.Count < 1)
            {
                entities = Markdown.Parse(ref text);
            }

            return new FormattedText(text.Replace('\v', '\n').Replace('\r', '\n'), entities);
        }

        protected virtual void OnGettingFormattedText()
        {

        }

        public virtual bool IsEmpty
        {
            get
            {
                Document.GetText(TextGetOptions.NoHidden, out string text);

                var isEmpty = string.IsNullOrWhiteSpace(text);
                if (isEmpty)
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }

                return isEmpty;
            }
        }

        public void SetText(FormattedText formattedText)
        {
            if (formattedText != null)
            {
                SetText(formattedText.Text, formattedText.Entities);
            }
            else
            {
                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }
        }

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
                        else if (entity.Type is TextEntityTypeUnderline)
                        {
                            range.CharacterFormat.Underline = UnderlineType.Single;
                        }
                        else if (entity.Type is TextEntityTypeStrikethrough)
                        {
                            range.CharacterFormat.Strikethrough = FormatEffect.On;
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

        public void InsertText(string text, IList<TextEntity> entities)
        {
            Document.BatchDisplayUpdates();

            if (!string.IsNullOrEmpty(text))
            {
                var index = Math.Min(Document.Selection.StartPosition, Document.Selection.EndPosition);

                Document.Selection.SetText(TextSetOptions.None, text);

                if (entities != null && entities.Count > 0)
                {
                    // We want to enumerate entities from last to first to not
                    // fuck up ranges due to hidden texts when formatting a link
                    foreach (var entity in entities.Reverse())
                    {
                        var range = Document.GetRange(index + entity.Offset, index + entity.Offset + entity.Length);

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

                Document.Selection.SetRange(Document.Selection.EndPosition, Document.Selection.EndPosition);
            }

            Document.ApplyDisplayUpdates();
        }
    }
}
