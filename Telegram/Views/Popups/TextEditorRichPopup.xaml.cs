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
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Popups
{
    public sealed partial class TextEditorRichPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly FormattedText _text;

        private readonly RichMessage _message;

        private readonly TaskCompletionSource<FormattedText> _tcs;
        private bool _closedExpected;

        private string _translateToLanguage;

        public TextEditorRichPopup(IClientService clientService, INavigationService navigationService, RichMessage message)
        {
            InitializeComponent();

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            _clientService = clientService;
            _navigationService = navigationService;
            _message = message;

            if (ApiInfo.CanCreateThemeShadow)
            {
                var shadow = new ThemeShadow();
                var translation = new Vector3(0, 0, Constants.BubbleElevation * 2);

                BackShadow.Shadow = shadow;
                BackShadow.Translation = translation;

                HistoryShadow.Shadow = shadow;
                HistoryShadow.Translation = translation;

                EmojiShadow.Shadow = shadow;
                EmojiShadow.Translation = translation;

                RewriteShadow.Shadow = shadow;
                RewriteShadow.Translation = translation;

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

        private async void Initialize(RichMessage message)
        {
            var file = await Package.Current.InstalledLocation.GetFileAsync("Assets\\editor.html");
            var text = await FileIO.ReadTextAsync(file);

            await View.EnsureCoreWebView2Async();

            _state = new RichEditorState();
            _commands = new RichEditorCommands(View.CoreWebView2);

            View.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            View.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            View.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            View.CoreWebView2.NavigateToString(text);

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

                    _commands.SetTheme(ActualTheme == ElementTheme.Light ? Theme.AccentLight.Dark1 : Theme.AccentDark.Light2, ActualTheme == ElementTheme.Dark);
                    _commands.SetModel(_message);

                    //PostEvent("setTheme", "accent", "#ff0000", "dark", false);
                    //PostEvent("setModel", _message.ToJson());
                }
                else if (type == "result")
                {
                    Debugger.Break();
                }
                else if (type == "state")
                {
                    _state.Update(data);

                    UndoButton.IsEnabled = _state.CanUndo;
                    RedoButton.IsEnabled = _state.CanRedo;

                    ParagraphButton.IsChecked = _state.BlockType is RichEditorBlockType.Heading or RichEditorBlockType.Paragraph or RichEditorBlockType.Pullquote or RichEditorBlockType.Preformatted;
                    QuoteButton.IsChecked = _state.BlockType == RichEditorBlockType.Blockquote;
                    ListButton.IsChecked = _state.BlockType == RichEditorBlockType.List;
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
                    LinkButton.IsChecked = _state.Link;
                    DateButton.IsChecked = _state.DateTime;

                    UpdateModel();
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

        private void OnContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            args.Handled = false;
        }

        private async void UpdateModel()
        {
            var model = await _commands.GetModelAsync();
            if (model != null)
            {
                Content.UpdateView(_clientService, model.Blocks, false);
            }
        }

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
            var heading = new MenuFlyoutSubItem
            {
                Text = "Heading"
            };

            {
                var command = new RelayCommand<int>(_commands.SetHeading);

                for (int i = 1; i < 7; i++)
                {
                    var child = new ToggleMenuFlyoutItem
                    {
                        Text = string.Format("Heading {0}", i),
                        FontFamily = new FontFamily("Times New Roman"),
                        FontSize = 24 - ((i - 1) * 2),
                        FontWeight = FontWeights.SemiBold,
                        IsChecked = _state.BlockType == RichEditorBlockType.Heading && _state.HeadingSize == i,
                        CommandParameter = i,
                        Command = command
                    };

                    heading.Items.Add(child);
                }
            }

            var text = new ToggleMenuFlyoutItem
            {
                Text = "Text",
                IsChecked = _state.BlockType == RichEditorBlockType.Paragraph,
                Command = new RelayCommand(_commands.SetParagraph)
            };

            var pullquote = new ToggleMenuFlyoutItem
            {
                Text = "Pullquote",
                IsChecked = _state.BlockType == RichEditorBlockType.Pullquote,
                Command = new RelayCommand(_commands.TogglePullquote)
            };

            var preformatted = new ToggleMenuFlyoutItem
            {
                Text = "Code",
                IsChecked = _state.BlockType == RichEditorBlockType.Preformatted,
                Command = new RelayCommand(_commands.SetPreformatted)
            };

            flyout.Items.Add(heading);
            flyout.Items.Add(text);
            flyout.Items.Add(pullquote);
            flyout.Items.Add(preformatted);

            flyout.ShowAt(ParagraphButton, FlyoutPlacementMode.Top);

        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            _commands.ToggleBlockquote();
        }

        private void List_Click(object sender, RoutedEventArgs e)
        {
            var command = new RelayCommand<RichEditorListType>(_commands.ToggleList);

            var flyout = new MenuFlyout();
            var none = new ToggleMenuFlyoutItem
            {
                Text = "None",
                IsChecked = _state.ListType == RichEditorListType.None,
                CommandParameter = RichEditorListType.None,
                Command = command
            };

            var bulleted = new ToggleMenuFlyoutItem
            {
                Text = "Bulleted",
                IsChecked = _state.ListType == RichEditorListType.Bullet,
                CommandParameter = RichEditorListType.Bullet,
                Command = command
            };

            var numbered = new ToggleMenuFlyoutItem
            {
                Text = "Numbered",
                IsChecked = _state.ListType == RichEditorListType.Ordered,
                CommandParameter = RichEditorListType.Ordered,
                Command = command
            };

            var checklist = new ToggleMenuFlyoutItem
            {
                Text = "To-Do",
                IsChecked = _state.ListType == RichEditorListType.Checkbox,
                CommandParameter = RichEditorListType.Checkbox,
                Command = command
            };

            flyout.Items.Add(none);
            flyout.Items.Add(bulleted);
            flyout.Items.Add(numbered);
            flyout.Items.Add(checklist);

            flyout.ShowAt(ListButton, FlyoutPlacementMode.Top);
        }

        private void Table_Click(object sender, RoutedEventArgs e)
        {
            if (_state.BlockType != RichEditorBlockType.Table)
            {
                _commands.InsertTable();
                return;
            }

            var flyout = new MenuFlyout();
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

            flyout.Items.Add(alignment);
            flyout.CreateFlyoutItem(_commands.TableToggleHeader, _state.CellIsHeader is true ? "Remove Highlight" : "Highlight Cell", Icons.TabInPrivate);
            
            if (_state.CanMergeCells)
            {
                flyout.CreateFlyoutItem(_commands.TableMergeCells, "Merge Cells", Icons.TableCellMerge);
            }

            if (_state.CanUnmergeCells)
            {
                flyout.CreateFlyoutItem(_commands.TableSplitCell, "Split Cells", Icons.TableCellSplit);
            }

            if (_state.CanAddRow)
            {
                flyout.CreateFlyoutItem(_commands.TableAddRowAfter, "Add Row", Icons.TableInsertRow);
            }

            if (_state.CanDeleteRow)
            {
                flyout.CreateFlyoutItem(_commands.TableDeleteRow, "Delete Row", Icons.TableDeleteRow, destructive: true);
            }

            if (_state.CanAddColumn)
            {
                flyout.CreateFlyoutItem(_commands.TableAddColumnAfter, "Add Column", Icons.TableInsertColumn);
            }

            if (_state.CanDeleteColumn)
            {
                flyout.CreateFlyoutItem(_commands.TableDeleteColumn, "Delete Column", Icons.TableDeleteColumn, destructive: true);
            }

            flyout.ShowAt(TableButton, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Top,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        private void Formula_Click(object sender, RoutedEventArgs e)
        {
            _commands.InsertAnchor("yolo");
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
