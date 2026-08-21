//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Host
{
    public sealed partial class RichTextWindow : WindowContent
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly long _messageId;

        private readonly RichMessage _message;

        // Reply + send options for the outgoing message (a new send; ignored when editing, i.e. _messageId != 0).
        private readonly InputMessageReplyTo _replyTo;
        private readonly MessageSendOptions _sendOptions;

        private bool _closedExpected;

        // The popup's own window, not the chat's: focus has to be taken back when it activates.
        private bool _ready;

        private string _translateToLanguage;

        public RichTextWindow(WindowContext window, IClientService clientService, INavigationService navigationService, long chatId, MessageTopic topic, long messageId, RichMessage message, InputMessageReplyTo replyTo = null, MessageSendOptions sendOptions = null)
            : base(window)
        {
            InitializeComponent();

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            _clientService = clientService;
            // NavigationService is window-specific: `navigationService` belongs to the originating chat
            // window, but this popup runs in its own window. Wrap it in a SecondaryNavigationService bound
            // to THIS window (forwards navigations — e.g. the premium promo — back to the source window),
            // like WebAppWindow.
            _navigationService = new SecondaryNavigationService(clientService.Session, navigationService, Window);
            _chatId = chatId;
            _topic = topic;
            _messageId = messageId;
            _message = message;
            _replyTo = replyTo;
            _sendOptions = sendOptions;

            if (ApiInfo.CanCreateThemeShadow)
            {
                var shadow = new ThemeShadow();
                var translation = new Vector3(0, 0, Constants.BubbleElevation * 2);

                HistoryShadow.Shadow = shadow;
                HistoryShadow.Translation = translation;

                BlockShadow.Shadow = shadow;
                BlockShadow.Translation = translation;

                AttachShadow.Shadow = shadow;
                AttachShadow.Translation = translation;

                SendShadow.Shadow = shadow;
                SendShadow.Translation = translation;

                StyleShadow.Shadow = shadow;
                StyleShadow.Translation = translation;

                EntityShadow.Shadow = shadow;
                EntityShadow.Translation = translation;
            }

            Initialize(message);
        }

        public bool AreTheSame(long chatId, long messageId)
        {
            return chatId == _chatId && messageId == _messageId;
        }

        private async void Initialize(RichMessage message)
        {
            await View.EnsureCoreWebView2Async();

            _state = new RichEditorState();
            _commands = new RichEditorCommands(View.CoreWebView2);

            var assets = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets");
            View.CoreWebView2.SetVirtualHostNameToFolderMapping("editor.unigram", assets, CoreWebView2HostResourceAccessKind.Allow);

            View.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            View.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            View.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            View.CoreWebView2.Navigate("https://editor.unigram/editor.html");

            ActualThemeChanged += OnActualThemeChanged;

            // Premium can change at runtime (UpdateOption "is_premium"); re-evaluate the send-button lock.
            _aggregator = _clientService.Session.Resolve<IEventAggregator>();
            _aggregator.Subscribe<UpdateOption>(this, Handle);

            Unloaded += OnUnloaded;
        }

        private IEventAggregator _aggregator;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            _aggregator?.Unsubscribe(this);
        }

        // Closing the editor window offers to save the current text as a chat draft. Skipped when we're
        // closing after an explicit send (_closedExpected), when editing an existing message (_messageId
        // != 0 — nothing to draft), or when the document is empty.
        protected override async void OnWindowCloseRequested(WindowCloseRequestedEventArgs e)
        {
            if (_closedExpected || _messageId != 0 || (_state?.IsEmpty ?? true))
            {
                return;
            }

            var deferral = e.GetDeferral();

            var confirm = await MessagePopup.ShowAsync(XamlRoot, "Save this message as a draft?", "Draft", "Save", "Discard");
            if (confirm == ContentDialogResult.Primary)
            {
                await SaveDraftAsync();
            }
            else if (confirm != ContentDialogResult.Secondary)
            {
                // Dismissed — cancel the close and keep editing.
                e.Handled = true;
            }

            deferral.Complete();
        }

        private async Task SaveDraftAsync()
        {
            var richMessage = await _commands.GetInputModelAsync();
            if (richMessage == null)
            {
                return;
            }

            var draft = new DraftMessage(_replyTo, 0, new DraftMessageContentInputRichMessage(richMessage), 0, null);
            _clientService.Send(new SetChatDraftMessage(_chatId, _topic, draft));
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.IsPremium)
            {
                this.BeginOnUIThread(UpdateSendLock);
            }
        }

        // Free users see a lock over the send button when the document contains rich content that
        // can't be sent as a plain message FormattedText (RichEditorState.HasRichContent).
        private void UpdateSendLock()
        {
            var locked = _state != null && _state.HasRichContent && !_clientService.IsPremium;
            SendLock.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            Color? background = null;
            if (Scrim.TopColor is SolidColorBrush color)
            {
                background = color.Color;
            }

            _commands?.SetTheme(ActualTheme == ElementTheme.Light ? Theme.AccentLight.Dark1 : Theme.AccentDark.Light2, ActualTheme == ElementTheme.Dark, background);
        }

        private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            //var json = _message.ToJson();
            //var result = await View.CoreWebView2.ExecuteScriptWithResultAsync(string.Format("UnigramEditor.exec('setModel', {0})", json));
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private async void View_CoreWebView2Initialized(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.UI.Xaml.Controls.CoreWebView2InitializedEventArgs args)
        {
        }

        // The window is shown after its content exists, and activating it moves focus to the
        // first focusable element in the tree - a toolbar button. Focusing the editor when it
        // reports ready is undone by that, so take focus on activation too.
        protected override void OnWindowActivated(bool active)
        {
            if (active)
            {
                // Low priority: XAML assigns its own initial focus while activating, and
                // whichever runs last wins.
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, TryFocus);
            }
        }

        private void TryFocus()
        {
            if (!_ready)
            {
                return;
            }

            // The window is activated as soon as its content exists (ViewService.OpenAsync),
            // which is long before CoreWebView2 does: XAML's initial focus lands on the
            // WebView2 while it has no controller to forward it to, and that GotFocus goes
            // nowhere. Focusing it from here is then a no-op — it already has focus — so the
            // caret only ever appeared after re-activating the window, which raises GotFocus
            // again. Moving focus off and back does the same thing on a cold open.
            if (ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), View))
            {
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
            }

            // Both halves, in this order. Focusing the control is what routes the keyboard
            // into the web content; only then does focusing the document leave a caret
            // behind. The other way round sets document.activeElement and nothing else.
            if (View.Focus(FocusState.Programmatic))
            {
                _commands.Focus();
            }
        }

        private RichEditorState _state;
        private RichEditorCommands _commands;

        private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (JsonObject.TryParse(args.WebMessageAsJson, out JsonObject data))
            {
                // {"type":"state","marks":{"bold":false,"italic":false,"underline":false,"strike":false,"code":false,"spoiler":false,"marked":false,"subscript":false,"superscript":false,"link":false},"block":"paragraph","inTable":false,"can":{"undo":false,"redo":false},
                // "selection":{"empty":true,"hasText":false,"isNode":false,"from":133,"to":133}}
                var type = data.GetNamedString("type", string.Empty);
                if (type == "ready")
                {
                    //var payload = $"{{\"command\":\"setModel\",\"id\":1,\"args\":{_message.ToJson()}}}";
                    //View.CoreWebView2.PostWebMessageAsJson(payload);

                    UpdateTheme();

                    _commands.SetConfig(
                        _clientService.Options.RichMessageTextLengthMax,
                        _clientService.Options.RichMessageBlockCountMax,
                        _clientService.Options.RichMessageDepthMax,
                        _clientService.Options.RichMessageMediaCountMax,
                        _clientService.Options.RichMessageTableColumnCountMax);
                    _commands.SetModel(_message);

                    //PostEvent("setTheme", "accent", "#ff0000", "dark", false);
                    //PostEvent("setModel", _message.ToJson());

                    // There is nothing to focus until the document exists, so the activation
                    // handler waits on this — and this covers the window already being active.
                    _ready = true;
                    TryFocus();
                }
                else if (type == "result")
                {
                    Debugger.Break();
                }
                else if (type == "state")
                {
                    _state.Update(data);
                    UpdateSendLock();

                    UndoButton.IsEnabled = _state.CanUndo;
                    RedoButton.IsEnabled = _state.CanRedo;

                    ParagraphButton.IsChecked = _state.BlockType is RichEditorBlockType.Heading or RichEditorBlockType.Paragraph or RichEditorBlockType.Pullquote or RichEditorBlockType.Preformatted or RichEditorBlockType.Footer;
                    ParagraphButton.Glyph = _state.BlockType switch
                    {
                        RichEditorBlockType.Heading => _state.HeadingSize switch
                        {
                            1 => Icons.TextHeader1,
                            2 => Icons.TextHeader2,
                            3 => Icons.TextHeader3,
                            4 => Icons.TextHeader4,
                            5 => Icons.TextHeader5,
                            _ => Icons.TextHeader6
                        },
                        RichEditorBlockType.Paragraph => Icons.TextParagraph,
                        RichEditorBlockType.Pullquote => "?",
                        RichEditorBlockType.Preformatted => Icons.Code,
                        _ => Icons.TextParagraph,
                    };

                    QuoteButton.IsChecked = _state.BlockType == RichEditorBlockType.Blockquote;

                    ListButton.IsChecked = _state.BlockType is RichEditorBlockType.List or RichEditorBlockType.Details;
                    ListButton.Glyph = _state.BlockType switch
                    {
                        RichEditorBlockType.List => _state.ListType switch
                        {
                            RichEditorListType.Bullet => Icons.TextBulletList,
                            RichEditorListType.Ordered => Icons.TextNumberList,
                            RichEditorListType.Checkbox => "?",
                            _ => Icons.TextBulletList,
                        },
                        RichEditorBlockType.Details => "?",
                        _ => Icons.TextBulletList
                    };

                    TableButton.IsChecked = _state.BlockType == RichEditorBlockType.Table;
                    FormulaButton.IsChecked = _state.BlockType == RichEditorBlockType.Math;

                    DocumentRoot.Visibility = _state.BlockType is RichEditorBlockType.Preformatted || _state.SelectionIsEmpty || !_state.SelectionHasText ? Visibility.Visible : Visibility.Collapsed;
                    SelectionRoot.Visibility = _state.BlockType is RichEditorBlockType.Preformatted || _state.SelectionIsEmpty || !_state.SelectionHasText ? Visibility.Collapsed : Visibility.Visible;

                    BoldButton.IsChecked = _state.Bold;
                    ItalicButton.IsChecked = _state.Italic;
                    UnderlineButton.IsChecked = _state.Underline;
                    StrikethroughButton.IsChecked = _state.Strikethrough;
                    SpoilerButton.IsChecked = _state.Spoiler;
                    MonospaceButton.IsChecked = _state.Code;
                    SubscriptButton.IsChecked = _state.Subscript;
                    SuperscriptButton.IsChecked = _state.Superscript;
                    LinkButton.IsChecked = _state.Link;
                    DateButton.IsChecked = _state.DateTime;

                    //UpdateModel();
                }
                else if (type == "customEmoji")
                {
                    // {"type":"customEmoji","dpr":1.5,"emojis":[{"id":"5208541126583136130","x":12,"y":189.7291717529297,"w":19.229167938232422,"h":18.197917938232422},{"id":"5384182985224374928","x":31.229167938232422,"y":189.7291717529297,"w":19.229167938232422,"h":18.197917938232422},{"id":"6052851174929860280","x":50.458335876464844,"y":189.7291717529297,"w":19.229167938232422,"h":18.197917938232422}]}
                    //Logger.Info(args.WebMessageAsJson);

                    var emojis = data.GetNamedArray("emojis");
                    var moving = data.GetNamedBoolean("moving");

                    var positions = new List<EmojiPosition>();

                    foreach (var item in emojis)
                    {
                        var obj = item.GetObject();
                        positions.Add(new EmojiPosition
                        {
                            CustomEmojiId = obj.GetNamedInt64("id", 0),
                            X = obj.GetNamedInt32("x", 0),
                            Y = obj.GetNamedInt32("y", 0),
                            FontSize = 14
                        });
                    }

                    Canvas.UpdateEntities(_clientService, positions);
                    //Canvas.Visibility = moving
                    //    ? Visibility.Collapsed
                    //    : Visibility.Visible;
                }
                else if (type == "preformattedLanguage")
                {
                    // {"type":"preformattedLanguage","language":"js","dpr":1.5,"rect":{"x":42,"y":367.5,"width":9.8854169845581055,"height":13.333333969116211}}
                    var language = data.GetNamedString("language");
                    var rect = data.GetNamedObject("rect");
                    var x = rect.GetNamedNumber("x");
                    var y = rect.GetNamedNumber("y");
                    var width = rect.GetNamedNumber("width");
                    var height = rect.GetNamedNumber("height");

                    OnPreformattedLanguage(language, x, y, width, height);
                }
                else if (type == "mathExpression")
                {
                    // {"type":"mathExpression","latex":"e^{i\\pi}+1=0","block":false,"dpr":1.5,"rect":{...}}
                    OnMathExpression(data.GetNamedString("latex"));
                }
                else
                {
                    Logger.Info(data);
                }
            }
        }

        private void OnPreformattedLanguage(string language, double x, double y, double width, double height)
        {
            var command = new RelayCommand<string>(_commands.SetLanguage);

            var flyout = new MenuFlyout();

            flyout.Items.Add(new ToggleMenuFlyoutItem
            {
                Text = "None",
                IsChecked = language == "none",
                CommandParameter = null,
                Command = command
            });

            if (!string.IsNullOrEmpty(language))
            {
                flyout.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = SyntaxToken.GetLanguageName(language),
                    IsChecked = true,
                    CommandParameter = null,
                    Command = command
                });
            }

            flyout.CreateFlyoutSeparator();

            foreach (var lang in SyntaxToken.Languages)
            {
                if (lang == language)
                {
                    continue;
                }

                var item = new ToggleMenuFlyoutItem
                {
                    Text = SyntaxToken.GetLanguageName(lang),
                    CommandParameter = lang,
                    Command = command
                };

                flyout.Items.Add(item);
            }

            flyout.ShowAt(View, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Transient,
                Position = new Point(x, y + height),
                ExclusionRect = new Rect(x, y, width, height)
            });
        }

        // Double-clicking a formula in the editor now fires a "mathExpression" event (instead
        // of an in-web-view prompt); we edit the LaTeX natively and write it back via
        // setMathExpression, which targets the math node the editor selected on double-click.
        private async void OnMathExpression(string latex)
        {
            var popup = new FormulaPopup(latex);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _commands.SetMathExpression(popup.Source);
            }
        }

        private void OnContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            args.Handled = true;

            var flyout = new MenuFlyout();

            var canPaste = ClipboardEx.TryGetContent().Contains(StandardDataFormats.Text);

            flyout.CreateFlyoutItem(_state.CanUndo, _commands.Undo, Strings.TextUndo, Icons.ArrowUndo, VirtualKey.Z);
            flyout.CreateFlyoutItem(_state.CanRedo, _commands.Redo, Strings.Redo, Icons.ArrowRedo, VirtualKey.Y);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(_state.CanCopy, _commands.Cut, Strings.Cut, Icons.Cut, VirtualKey.X);
            flyout.CreateFlyoutItem(_state.CanCopy, _commands.Copy, Strings.Copy, Icons.Copy, VirtualKey.C);
            flyout.CreateFlyoutItem(_state.CanPaste && canPaste, _commands.Paste, Strings.Paste, Icons.ClipboardPaste, VirtualKey.V);
            flyout.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.Delete, Strings.Delete);
            flyout.CreateFlyoutSeparator();

            if (_state.BlockType is RichEditorBlockType.Heading or RichEditorBlockType.Paragraph or RichEditorBlockType.Pullquote or RichEditorBlockType.Preformatted or RichEditorBlockType.Footer)
            {
                var paragraph = new MenuFlyoutSubItem
                {
                    Text = "Paragraph",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextParagraph)
                };

                PopulateParagraphFlyout(paragraph.Items);

                flyout.Items.Add(paragraph);
            }
            else if (_state.BlockType is RichEditorBlockType.List or RichEditorBlockType.Details)
            {
                var paragraph = new MenuFlyoutSubItem
                {
                    Text = "List",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextBulletList)
                };

                PopulateListFlyout(paragraph.Items);

                flyout.Items.Add(paragraph);
            }
            else if (_state.BlockType == RichEditorBlockType.Table)
            {
                var table = new MenuFlyoutSubItem
                {
                    Text = "Table",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Table)
                };

                PopulateTableFlyout(table.Items);

                flyout.Items.Add(table);
            }

            var formatting = new MenuFlyoutSubItem
            {
                Text = Strings.Formatting,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextFont)
            };

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleBold, Strings.Bold, Icons.TextBold, VirtualKey.B);

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleItalic, Strings.Italic, Icons.TextItalic, VirtualKey.I);

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleUnderline, Strings.Underline, Icons.TextUnderline, VirtualKey.U);

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleStrikethrough, Strings.Strike, Icons.TextStrikethrough, VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            //if ((entities & FormattedTextEntity.Quote) != 0)
            //{
            //    _formattingFlyout.CreateFlyoutItem(length, ToggleQuote, Strings.Quote, Icons.QuoteBlock, (VirtualKey)190, VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift);
            //}

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleCode, Strings.Mono, Icons.Code, VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, _commands.ToggleSpoiler, Strings.Spoiler, Icons.Spoiler, VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            //formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, CreateDate, Strings.FormattedDate, Icons.Calendar);

            //formatting.CreateFlyoutSeparator();

            //formatting.CreateFlyoutItem(!_state.SelectionIsEmpty, CreateLink, clone.Link.Length > 0 ? Strings.EditLink : Strings.CreateLink, Icons.Link, VirtualKey.K);

            //formatting.CreateFlyoutSeparator();
            //formatting.CreateFlyoutItem(length && !IsDefaultFormat(selection), ToggleRegular, Strings.Regular, null, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            flyout.Items.Add(formatting);

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(!_state.IsEmpty, _commands.SelectAll, Strings.SelectAll, null, VirtualKey.A);

            flyout.ShowAt(View, new FlyoutShowOptions
            {
                Position = args.Location,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        //private async void UpdateModel()
        //{
        //    var model = await _commands.GetModelAsync();
        //    if (model != null)
        //    {
        //        Content.UpdateView(_clientService, model.Blocks, false);
        //    }
        //}

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleBold();
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleItalic();
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleUnderline();
        }

        private void Strikethrough_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleStrikethrough();
        }

        private void Spoiler_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleSpoiler();
        }

        private void Monospace_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleCode();
        }

        private void Subscript_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleSubscript();
        }

        private void Superscript_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleSuperscript();
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Date_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            _commands.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            _commands.Redo();
        }

        //private void PostEvent(string eventName, params object[] eventData)
        //{
        //    if (eventData.Length % 2 == 0)
        //    {
        //        var data = new JsonObject();

        //        for (int i = 0; i < eventData.Length; i += 2)
        //        {
        //            if (eventData[i] is string key)
        //            {
        //                data[key] = eventData[i + 1] switch
        //                {
        //                    string stringValue => CreateStringValue(stringValue),
        //                    double numberValue => Windows.Data.Json.JsonValue.CreateNumberValue(numberValue),
        //                    bool booleanValue => Windows.Data.Json.JsonValue.CreateBooleanValue(booleanValue),
        //                    _ => Windows.Data.Json.JsonValue.CreateNullValue(),
        //                };

        //                static Windows.Data.Json.JsonValue CreateStringValue(string stringValue)
        //                {
        //                    try
        //                    {
        //                        if (Windows.Data.Json.JsonValue.TryParse(stringValue, out Windows.Data.Json.JsonValue obj))
        //                        {
        //                            return obj;
        //                        }
        //                    }
        //                    catch
        //                    {
        //                        Logger.Debug("Unable to parse JSON string: " + stringValue);
        //                    }

        //                    return Windows.Data.Json.JsonValue.CreateStringValue(stringValue);
        //                }
        //            }
        //        }

        //        PostEventImpl(eventName, data.Stringify());
        //    }
        //    else if (eventData.Length > 0)
        //    {
        //        PostEventImpl(eventName, string.Join(' ', eventData));
        //    }
        //    else
        //    {
        //        PostEventImpl(eventName, "null");
        //    }
        //}

        //private void PostEventImpl(string eventName, string eventData = "null")
        //{
        //    Logger.Info(string.Format("{0}: {1}", eventName, eventData));
        //    _ = View.CoreWebView2.ExecuteScriptWithResultAsync($"UnigramEditor.exec('{eventName}', {eventData});");
        //}

        private void Paragraph_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateParagraphFlyout(flyout.Items);

            flyout.ShowAt(ParagraphButton, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Top,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        private void PopulateParagraphFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var heading = new MenuFlyoutSubItem
            {
                Text = Strings.ArticleHeading
            };

            {
                var command = new RelayCommand<int>(_commands.SetHeading);

                for (int i = 1; i < 7; i++)
                {
                    var child = new ToggleMenuFlyoutItem
                    {
                        Text = i == 1 ? Strings.ArticleHeading1 : i == 2 ? Strings.ArticleHeading2 : i == 3 ? Strings.ArticleHeading3 : i == 4 ? Strings.ArticleHeading4 : i == 5 ? Strings.ArticleHeading5 : Strings.ArticleHeading6,
                        FontFamily = new FontFamily("Times New Roman"),
                        FontSize = 24 - ((i - 1) * 2),
                        FontWeight = FontWeights.SemiBold,
                        Icon = MenuFlyoutHelper.CreateIcon(i == 1 ? Icons.TextHeader1 : i == 2 ? Icons.TextHeader2 : i == 3 ? Icons.TextHeader3 : i == 4 ? Icons.TextHeader4 : i == 5 ? Icons.TextHeader5 : Icons.TextHeader6),
                        IsChecked = _state.BlockType == RichEditorBlockType.Heading && _state.HeadingSize == i,
                        CommandParameter = i,
                        Command = command
                    };

                    heading.Items.Add(child);
                }
            }

            var text = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleText,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextT),
                IsChecked = _state.BlockType == RichEditorBlockType.Paragraph,
                Command = new RelayCommand(_commands.SetParagraph)
            };

            var pullquote = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticlePullquote,
                IsChecked = _state.BlockType == RichEditorBlockType.Pullquote,
                Command = new RelayCommand(_commands.TogglePullquote)
            };

            var preformatted = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleCode,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.Code),
                IsChecked = _state.BlockType == RichEditorBlockType.Preformatted,
                Command = new RelayCommand(_commands.SetPreformatted)
            };

            var footer = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleFooter,
                IsChecked = _state.BlockType == RichEditorBlockType.Footer,
                Command = new RelayCommand(_commands.SetFooter)
            };

            flyout.Add(heading);
            flyout.Add(text);
            flyout.Add(pullquote);
            flyout.Add(preformatted);
            flyout.Add(footer);
        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleBlockquote();
        }

        private void List_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateListFlyout(flyout.Items);

            flyout.ShowAt(ListButton, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Top,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        private void PopulateListFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var command = new RelayCommand<RichEditorListType>(_commands.ToggleList);

            var none = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleNone,
                IsChecked = _state.ListType == RichEditorListType.None,
                CommandParameter = RichEditorListType.None,
                Command = command
            };

            var bulleted = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleListBulleted,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextBulletList),
                IsChecked = _state.ListType == RichEditorListType.Bullet,
                CommandParameter = RichEditorListType.Bullet,
                Command = command
            };

            var numbered = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleListNumbered,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextNumberList),
                IsChecked = _state.ListType == RichEditorListType.Ordered,
                CommandParameter = RichEditorListType.Ordered,
                Command = command
            };

            var checklist = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleListTodo,
                IsChecked = _state.ListType == RichEditorListType.Checkbox,
                CommandParameter = RichEditorListType.Checkbox,
                Command = command
            };

            var toggle = new ToggleMenuFlyoutItem
            {
                Text = Strings.ArticleToggleBlock,
                IsChecked = _state.BlockType == RichEditorBlockType.Details,
                Command = new RelayCommand(_commands.InsertDetails)
            };

            flyout.Add(none);
            flyout.Add(bulleted);
            flyout.Add(numbered);
            flyout.Add(checklist);
            flyout.Add(toggle);
        }

        private void Table_Click(object sender, RoutedEventArgs e)
        {
            if (_state.BlockType != RichEditorBlockType.Table)
            {
                _commands.InsertTable();
                return;
            }

            var flyout = new MenuFlyout();

            PopulateTableFlyout(flyout.Items);

            flyout.ShowAt(TableButton, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Top,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        private void PopulateTableFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var alignment = new MenuFlyoutSubItem
            {
                Text = "Alignment",
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignCenter),
            };

            {
                var command = new RelayCommand<RichEditorCellAlignment>(_commands.SetCellAlignment);

                var left = new ToggleMenuFlyoutItem
                {
                    Text = "Left",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignLeft),
                    IsChecked = _state.CellAlignment == RichEditorCellAlignment.Left,
                    CommandParameter = RichEditorCellAlignment.Left,
                    Command = command
                };
                var center = new ToggleMenuFlyoutItem
                {
                    Text = "Center",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignCenter),
                    IsChecked = _state.CellAlignment == RichEditorCellAlignment.Center,
                    CommandParameter = RichEditorCellAlignment.Center,
                    Command = command
                };
                var right = new ToggleMenuFlyoutItem
                {
                    Text = "Right",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignRight),
                    IsChecked = _state.CellAlignment == RichEditorCellAlignment.Right,
                    CommandParameter = RichEditorCellAlignment.Right,
                    Command = command
                };

                alignment.Items.Add(left);
                alignment.Items.Add(center);
                alignment.Items.Add(right);
            }

            alignment.CreateFlyoutSeparator();

            {
                var command = new RelayCommand<RichEditorCellVerticalAlignment>(_commands.SetCellVerticalAlignment);

                var top = new ToggleMenuFlyoutItem
                {
                    Text = "Top",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignTop),
                    IsChecked = _state.CellVerticalAlignment == RichEditorCellVerticalAlignment.Top,
                    CommandParameter = RichEditorCellVerticalAlignment.Top,
                    Command = command
                };
                var middle = new ToggleMenuFlyoutItem
                {
                    Text = "Middle",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignMiddle),
                    IsChecked = _state.CellVerticalAlignment == RichEditorCellVerticalAlignment.Middle,
                    CommandParameter = RichEditorCellVerticalAlignment.Middle,
                    Command = command
                };
                var bottom = new ToggleMenuFlyoutItem
                {
                    Text = "Bottom",
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignBottom),
                    IsChecked = _state.CellVerticalAlignment == RichEditorCellVerticalAlignment.Bottom,
                    CommandParameter = RichEditorCellVerticalAlignment.Bottom,
                    Command = command
                };

                alignment.Items.Add(top);
                alignment.Items.Add(middle);
                alignment.Items.Add(bottom);
            }

            flyout.Add(alignment);
            flyout.CreateFlyoutItem(_commands.TableToggleHeader, _state.CellIsHeader is true ? Strings.ArticleRemoveHighlight : Strings.ArticleHighlightCell, Icons.TabInPrivate);

            if (_state.CanMergeCells)
            {
                flyout.CreateFlyoutItem(_commands.TableMergeCells, Strings.ArticleMergeCells, Icons.TableCellMerge);
            }

            if (_state.CanUnmergeCells)
            {
                flyout.CreateFlyoutItem(_commands.TableSplitCell, Strings.ArticleSplitCells, Icons.TableCellSplit);
            }

            if (_state.CanAddRow)
            {
                flyout.CreateFlyoutItem(_commands.TableAddRowBefore, Strings.ArticleInsertAbove, Icons.TableInsertRow);
                flyout.CreateFlyoutItem(_commands.TableAddRowAfter, Strings.ArticleInsertBelow, Icons.TableInsertRow);
            }

            if (_state.CanDeleteRow)
            {
                flyout.CreateFlyoutItem(_commands.TableDeleteRow, Strings.ArticleDeleteRow, Icons.TableDeleteRow, destructive: true);
            }

            if (_state.CanAddColumn)
            {
                flyout.CreateFlyoutItem(_commands.TableAddColumnBefore, Strings.ArticleInsertLeft, Icons.TableInsertColumn);
                flyout.CreateFlyoutItem(_commands.TableAddColumnAfter, Strings.ArticleInsertRight, Icons.TableInsertColumn);
            }

            if (_state.CanDeleteColumn)
            {
                flyout.CreateFlyoutItem(_commands.TableDeleteColumn, Strings.ArticleDeleteColumn, Icons.TableDeleteColumn, destructive: true);
            }
        }

        private async void Formula_Click(object sender, RoutedEventArgs e)
        {
            var popup = new FormulaPopup();

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                if (_state.BlockType == RichEditorBlockType.Table)
                {
                    _commands.InsertMathInline(popup.Source);
                }
                else
                {
                    _commands.InsertMathBlock(popup.Source);
                }
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Transient,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
            });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                //TitleField.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                //TitleField.InsertEmoji(sticker);
                _commands.InsertEmoji(customEmoji.CustomEmojiId, sticker.Emoji);
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var richMessage = await _commands.GetInputModelAsync();
            if (richMessage == null)
            {
                return;
            }

            // Editing an existing rich message.
            if (_messageId != 0)
            {
                await _clientService.SendAsync(new EditMessageMedia(_chatId, _messageId, new InputMessageRichMessage(richMessage, false)));
                Close();
                return;
            }

            // Sending a new one — mirrors ComposeViewModel.SendRichMessage: a free user sends the plain
            // FormattedText when the content is representable, otherwise gets the Premium feature promo;
            // a Premium user sends the full rich message.
            if (_clientService.IsPremiumAvailable && !_clientService.IsPremium)
            {
                if (PageBlockHelper.TryGetFormattedText(richMessage, out FormattedText formatted))
                {
                    await SendAsync(new InputMessageText(formatted, null, false));
                    Close();
                }
                else
                {
                    ToastPopup.ShowFeaturePromo(_navigationService, new PremiumFeatureRichMessages());
                }

                return;
            }

            await SendAsync(new InputMessageRichMessage(richMessage, true));
            Close();
        }

        private async Task SendAsync(InputMessageContent content)
        {
            var options = _sendOptions ?? new MessageSendOptions();
            options.SendingId = Math.Max(options.SendingId, 1);
            await _clientService.SendAsync(new SendMessage(_chatId, _topic, _replyTo, options, content));
        }

        private void Close()
        {
            _closedExpected = true;

            if (Window != null)
            {
                _ = Window.ConsolidateAsync();
            }
            else
            {
                _ = ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }
    }

    public static class Test
    {
        public static string ToJson(this BaseObject obj)
        {
            using var buffer = new ArrayPoolBufferWriter();
            using var writer = new Utf8JsonWriter(buffer, new()
            {
#if ARM64
                Encoder = new Arm64SafeEncoder(),
#else
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
#endif
                SkipValidation = true
            });

            writer.WriteStartObject();
            try
            {
                obj.ToJson(writer);
            }
            catch { }
            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
