using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Rtf;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class BubbleTextBox : RichEditBox
    {
        // TODO: TEMP!!!
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private MenuFlyout _flyout;
        private MenuFlyoutPresenter _presenter;

        // True when the RichEdithBox MIGHT contains formatting (bold, italic, hyperlinks) 
        private bool _isDirty;

        public BubbleTextBox()
        {
            _flyout = new MenuFlyout();
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Bold" });
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Italic" });
            _flyout.AllowFocusOnInteraction = false;
            _flyout.AllowFocusWhenDisabled = false;

            ((MenuFlyoutItem)_flyout.Items[0]).Click += Bold_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Click += Italic_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Loaded += Italic_Loaded;

#if DEBUG
            // To test hyperlinks (Used for mention name => to tag people that has no username)
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Hyperlink" });
            ((MenuFlyoutItem)_flyout.Items[2]).Click += Hyperlink_Click;
#endif

#if !DEBUG
            // We need the ability to paste RTF content for debug pourposes
            Paste += OnPaste;
#endif
            SelectionChanged += OnSelectionChanged;

            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Off;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }

            UpdateIsDirty(Document.Selection);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Off;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }

            UpdateIsDirty(Document.Selection);
        }

#if DEBUG
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Off;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Off;
            Document.Selection.Link = "\"33303409\""; // Daniel user ID lol
            Document.Selection.CharacterFormat.Underline = UnderlineType.Dash;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            UpdateIsDirty(Document.Selection);
        }
#endif

        private void UpdateIsDirty(ITextRange range)
        {
            var link = string.IsNullOrEmpty(range.Link) == false;
            var bold = range.CharacterFormat.Bold == FormatEffect.On;
            var italic = range.CharacterFormat.Italic == FormatEffect.On;

            _isDirty |= link || bold || italic;
        }

        private void Italic_Loaded(object sender, RoutedEventArgs e)
        {
            _presenter = (MenuFlyoutPresenter)_flyout.Items[1].Ancestors<MenuFlyoutPresenter>().FirstOrDefault();
            OnSelectionChanged();
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
            // we have to handle the pasting operation manually to allow plaintext only.
            var package = Clipboard.GetContent();
            if (package.Contains(StandardDataFormats.Text) && package.Contains("Rich Text Format"))
            {
                e.Handled = true;

                var formats = package.AvailableFormats.ToList();
                var text = await package.GetTextAsync();

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
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (Document.Selection.Length != 0)
            {
                int hit;
                Rect rect;
                Document.Selection.GetRect(PointOptions.ClientCoordinates, out rect, out hit);
                _flyout.ShowAt(this, new Point(rect.X + 12, rect.Y - _presenter?.ActualHeight ?? 0));
            }
            else
            {
                _flyout.Hide();
            }
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
            if (args.VirtualKey == VirtualKey.Enter && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                if (!ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down) && !IsEmpty)
                {
                    args.Handled = true;
                    await SendAsync();
                }
            }
        }

        protected override void OnKeyUp(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                FormatText();
            }

            base.OnKeyUp(e);
        }

        private void FormatText()
        {
            string text;
            Document.GetText(TextGetOptions.FormatRtf, out text);

            var caretPosition = Document.Selection.StartPosition;
            var result = Emoticon.Pattern.Replace(text, (match) =>
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

                return emoji;
            });

            Document.SetText(TextSetOptions.FormatRtf, result.TrimEnd("\\par\r\n}\r\n\0") + "}\r\n\0");
            Document.Selection.SetRange(caretPosition, caretPosition);
        }

        public async Task SendAsync()
        {
            FormatText();

            bool isDirty = _isDirty;

            string text;
            string planText;
            Document.GetText(TextGetOptions.FormatRtf, out text);
            Document.GetText(TextGetOptions.NoHidden, out planText);

            Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\colortbl ;\red0\green0\blue0;}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");

            planText = planText.Trim();
            ViewModel.SendTextHolder = planText;

            if (isDirty)
            {
                var parser = new RtfToTLParser();
                var reader = new RtfReader(parser);
                reader.LoadRtfText(text);
                reader.Parse();

                await ViewModel.SendMessageAsync(parser.Entities, false);
            }
            else
            {
                ViewModel.SendCommand.Execute(null);
            }
        }

        public bool IsEmpty
        {
            get
            {
                // TODO: a better implementation?

                string text;
                Document.GetText(TextGetOptions.None, out text);

                var isEmpty = string.IsNullOrWhiteSpace(text);
                if (isEmpty)
                {
                    // If the text area is empty it cannot contains markup
                    _isDirty = false;
                }

                return isEmpty;
            }
        }
    }

    public class RtfToTLParser : RtfSarParser
    {
        private bool _bold;
        private bool _italic;
        private bool _firstPard;

        private string _groupText;
        private string _lastKey;
        private int? _field;

        private int _length;

        private Stack<TLMessageEntityBase> _entities;

        public List<TLMessageEntityBase> Entities { get; private set; }

        public override void StartRtfDocument()
        {
            _entities = new Stack<TLMessageEntityBase>();
            Entities = null;

            _bold = false;
            _italic = false;
            _firstPard = false;

            _groupText = null;
            _lastKey = null;
            _field = null;

            _length = 0;
        }

        public override void StartRtfGroup()
        {
            if (_firstPard)
            {
                if (_field.HasValue)
                    _field++;
            }
        }

        public override void RtfControl(string key, bool hasParameter, int parameter)
        {
            if (_firstPard && key == "'" && hasParameter)
            {
                if (_field.HasValue && _lastKey == "fldinst")
                {
                    _groupText += (char)parameter;
                }
                else if (_field.HasValue && _lastKey.Equals("fldrslt"))
                {
                    _groupText += (char)parameter;
                }
                else if (_bold || _italic)
                {
                    _groupText += (char)parameter;
                }
                else
                {
                    _groupText += (char)parameter;
                    HandleBasicText();
                    _groupText = string.Empty;
                }
            }
        }

        public override void RtfKeyword(string key, bool hasParameter, int parameter)
        {
            if (key.Equals("pard"))
            {
                _firstPard = true;
            }
            else if (key.Equals("field"))
            {
                _field = !hasParameter || (hasParameter && parameter == 1) ? new int?(0) : null;
            }
            else if (key.Equals("b"))
            {
                if (!hasParameter || (hasParameter && parameter == 1))
                {
                    _groupText = string.Empty;
                    _bold = true;
                }
                else
                {
                    HandleBoldText();
                    _groupText = string.Empty;
                    _bold = false;
                }
            }
            else if (key.Equals("i"))
            {
                if (!hasParameter || (hasParameter && parameter == 1))
                {
                    _groupText = string.Empty;
                    _italic = true;
                }
                else
                {
                    HandleItalicText();
                    _groupText = string.Empty;
                    _italic = false;
                }
            }
            else if (key.Equals("fldinst") || key.Equals("fldrslt"))
            {
                _lastKey = key;
            }
        }

        public override void RtfText(string text)
        {
            if (_firstPard)
            {
                if (_field.HasValue && _lastKey == "fldinst")
                {
                    if (text.IndexOf("HYPERLINK") == 0)
                        _groupText += text.Substring("HYPERLINK ".Length);
                    else
                        _groupText += text;
                }
                else if (_field.HasValue && _lastKey.Equals("fldrslt"))
                {
                    _groupText += text;
                }
                else if (_bold || _italic)
                {
                    _groupText += text;
                }
                else
                {
                    _groupText += text;
                    HandleBasicText();
                    _groupText = string.Empty;
                }
            }
        }

        public override void EndRtfGroup()
        {
            if (_firstPard)
            {
                if (_bold) HandleBoldText();
                else if (_italic) HandleItalicText();
                else if (_field.HasValue && _field == 2 && _lastKey.Equals("fldinst")) HandleHyperlinkUrl();
                else if (_field.HasValue && _field == 2 && _lastKey.Equals("fldrslt")) HandleHyperlinkText();
                else if (string.IsNullOrEmpty(_groupText) == false) HandleBasicText();

                _bold = false;
                _italic = false;
                _lastKey = string.Empty;
                _groupText = string.Empty;

                if (_field.HasValue)
                    _field--;

                if (_field.HasValue && _field < 0)
                    _field = null;
            }
        }

        private void HandleBoldText()
        {
            _entities.Push(new TLMessageEntityBold { Offset = _length, Length = _groupText.Length });
            _length += _groupText.Length;
        }

        private void HandleItalicText()
        {
            _entities.Push(new TLMessageEntityItalic { Offset = _length, Length = _groupText.Length });
            _length += _groupText.Length;
        }

        private void HandleHyperlinkUrl()
        {
            var userId = int.Parse(_groupText.Trim().Trim('"'));
            var user = InMemoryCacheService.Current.GetUser(userId) as TLUser;
            if (user != null && user.HasAccessHash)
            {
                _entities.Push(new TLInputMessageEntityMentionName { UserId = new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash.Value } });
            }
        }

        private void HandleHyperlinkText()
        {
            var mention = _entities.Peek() as TLInputMessageEntityMentionName;
            if (mention != null)
            {
                mention.Offset = _length;
                mention.Length = _groupText.Length;
            }

            _length += _groupText.Length;
        }

        private void HandleBasicText()
        {
            _length += _groupText.Length;
        }

        public override void EndRtfDocument()
        {
            if (Entities == null)
            {
                Entities = new List<TLMessageEntityBase>(_entities);
            }
        }
    }
}
