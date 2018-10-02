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
using Unigram.Core;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Unigram.Native;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Automation;
using Unigram.Entities;
using Telegram.Td.Api;
using Unigram.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Common;
using Unigram.Collections;
using Template10.Common;
using System.Windows.Input;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls.Primitives;
using Unigram.Controls.Views;

namespace Unigram.Controls
{
    public class FormattedTextBox : RichEditBox
    {
        public FormattedTextBox()
        {
            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            ContextMenuOpening += OnContextMenuOpening;

            if (ApiInfo.CanAddContextRequestedEvent)
            {
                AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(OnContextRequested), true);
            }
            else
            {
                ContextRequested += OnContextRequested;
            }
        }

        #region Context menu

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
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

            var flyout = new MenuFlyout();
            CreateFlyoutItem(flyout.Items, Document.CanUndo(), ContextUndo_Click, "Undo", VirtualKey.Z);
            CreateFlyoutItem(flyout.Items, Document.CanRedo(), ContextRedo_Click, "Redo", VirtualKey.Y);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), ContextCut_Click, "Cut", VirtualKey.X);
            CreateFlyoutItem(flyout.Items, length && Document.CanCopy(), ContextCopy_Click, "Copy", VirtualKey.C);
            CreateFlyoutItem(flyout.Items, Document.CanPaste(), ContextPaste_Click, "Paste", VirtualKey.V);
            CreateFlyoutItem(flyout.Items, length, ContextDelete_Click, "Delete");
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(formatting);
            flyout.Items.Add(new MenuFlyoutSeparator());
            CreateFlyoutItem(flyout.Items, !IsEmpty, ContextSelectAll_Click, "Select All", VirtualKey.A);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions"))
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(this, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                }
                else
                {
                    flyout.ShowAt(this, point);
                }
            }
            else if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(this);
            }
        }

        private void ContextBold_Click()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.Selection.CharacterFormat.Bold = FormatEffect.On;
            Document.ApplyDisplayUpdates();
        }

        private void ContextItalic_Click()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection);
            Document.Selection.CharacterFormat.Italic = FormatEffect.On;
            Document.ApplyDisplayUpdates();
        }

        private void ContextMonospace_Click()
        {
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
                document.LanguageTag == format.LanguageTag &&
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

        private void CreateFlyoutItem(IList<MenuFlyoutItemBase> flyout, bool create, Action command, string text, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = create;
            flyoutItem.Command = new RelayCommand(command);
            flyoutItem.Text = text;

            if (key.HasValue && ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators"))
            {
                var accelerator = new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, ScopeOwner = this };
                accelerator.Invoked += FlyoutAccelerator_Invoked;

                flyoutItem.KeyboardAccelerators.Add(accelerator);
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

        public FormattedText GetFormattedText(IProtoService protoService, bool clear = false)
        {
            Document.BatchDisplayUpdates();

            var entities = new List<TextEntity>();
            var adjust = 0;

            var end = false;
            for (int i = 0; !end; i++)
            {
                var range = Document.GetRange(i, i + 1);
                var expand = range.Expand(TextRangeUnit.CharacterFormat);

                if (range.CharacterFormat.Bold == FormatEffect.On)
                {
                    entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeBold() });
                }
                else if (range.CharacterFormat.Italic == FormatEffect.On)
                {
                    entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeItalic() });
                }
                else if (range.CharacterFormat.Name.Equals("Consolas"))
                {
                    range.GetText(TextGetOptions.NoHidden, out string value);

                    if (value.Contains('\v') || value.Contains('\r'))
                    {
                        entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypePre() });
                    }
                    else
                    {
                        entities.Add(new TextEntity { Offset = range.StartPosition - adjust, Length = Math.Abs(range.Length), Type = new TextEntityTypeCode() });
                    }
                }
                else if (range.Link.Length > 0)
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
                else
                {
                    range.GetText(TextGetOptions.NoHidden, out string value);

                    var sub = Markdown.Parse(protoService, ref value);
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

                end = i >= range.EndPosition;
                i = range.EndPosition;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (clear)
            {
                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }

            Document.ApplyDisplayUpdates();

            return new FormattedText(text, entities);
        }

        public bool IsEmpty
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
            SetText(formattedText.Text, formattedText.Entities);
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
                    foreach (var entity in entities)
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
