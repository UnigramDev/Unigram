//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Data.Json;
using Windows.UI;

namespace Telegram.Views.Popups
{
    /// <summary>Media kind for <see cref="RichEditorCommands.InsertImage"/>.</summary>
    public enum RichEditorMediaKind
    {
        Photo,
        Video,
        Audio,
        Animation,
        Voice,
    }

    /// <summary>One custom emoji rect reported by <see cref="RichEditorCommands.GetCustomEmojiAsync"/>.</summary>
    public class RichEditorEmojiPlacement
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>Result of <see cref="RichEditorCommands.GetCustomEmojiAsync"/>: the visible emoji rects plus the device pixel ratio.</summary>
    public class RichEditorEmojiLayout
    {
        public double RasterizationScale { get; set; } = 1;
        public bool Moving { get; set; }
        public IList<RichEditorEmojiPlacement> Emojis { get; } = new List<RichEditorEmojiPlacement>();
    }

    /// <summary>
    /// Strongly-typed wrapper over the editor's command surface (the JS
    /// <c>UnigramEditor.exec(command, args)</c> bridge). One method per command, with
    /// typed parameters; queries return parsed results. Transport is the same
    /// inline-script approach the host already uses — a cleaner channel (correlated
    /// PostWebMessageAsJson) can replace <see cref="Exec(string, JsonObject)"/> later
    /// without touching call sites.
    /// </summary>
    public class RichEditorCommands
    {
        private readonly CoreWebView2 _webView;

        public RichEditorCommands(CoreWebView2 webView)
        {
            _webView = webView;
        }

        // --- marks --------------------------------------------------------------
        public void ToggleBold() => Exec("toggleBold");
        public void ToggleItalic() => Exec("toggleItalic");
        public void ToggleUnderline() => Exec("toggleUnderline");
        public void ToggleStrikethrough() => Exec("toggleStrike");
        public void ToggleCode() => Exec("toggleCode");
        public void ToggleSpoiler() => Exec("toggleSpoiler");
        public void ToggleMarked() => Exec("toggleMarked");
        public void ToggleSubscript() => Exec("toggleSubscript");
        public void ToggleSuperscript() => Exec("toggleSuperscript");

        /// <summary>Applies a link to the selection; pass a null/empty url to remove it.</summary>
        public void SetLink(string url, bool isCached = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                Exec("setLink");
            }
            else
            {
                Exec("setLink", new JsonObject { { "href", Str(url) }, { "isCached", Bool(isCached) } });
            }
        }

        /// <summary>Applies a date/time link to the selection; pass null to remove it.</summary>
        public void SetDateTime(long? unixTime)
        {
            if (unixTime is long value)
            {
                Exec("setDateTime", new JsonObject { { "unixTime", Num(value) } });
            }
            else
            {
                Exec("setDateTime");
            }
        }

        // --- block types --------------------------------------------------------
        public void SetParagraph() => Exec("setParagraph");
        public void SetHeading(int size) => Exec("setHeading", new JsonObject { { "size", Num(size) } });
        public void SetPreformatted() => Exec("setPreformatted");

        /// <summary>Sets the current code block's language; pass null/empty for none.</summary>
        public void SetLanguage(string language) => Exec("setLanguage", new JsonObject { { "language", Str(language) } });

        public void ToggleBlockquote() => Exec("toggleBlockquote");
        public void TogglePullquote() => Exec("togglePullquote");

        // --- lists --------------------------------------------------------------
        /// <summary>Sets the list style of the current block; <see cref="RichEditorListType.None"/> removes the list.</summary>
        public void ToggleList(RichEditorListType type = RichEditorListType.None) => Exec("toggleList", new JsonObject { { "type", Str(Slug(type)) } });
        public void Indent() => Exec("indent");
        public void Outdent() => Exec("outdent");

        // --- structural ---------------------------------------------------------
        public void InsertDivider() => Exec("insertDivider");
        public void InsertAnchor(string name = null) => Exec("insertAnchor", new JsonObject { { "name", Str(name) } });
        /// <summary>Renames the selected anchor (when <c>BlockType</c> is Anchor).</summary>
        public void SetAnchorName(string name) => Exec("setAnchorName", new JsonObject { { "name", Str(name) } });
        public void InsertDetails() => Exec("insertDetails");

        // --- atoms --------------------------------------------------------------
        public void InsertEmoji(long customEmojiId, string alternativeText = null)
        {
            var args = new JsonObject { { "id", Str(customEmojiId.ToString(CultureInfo.InvariantCulture)) } };
            if (alternativeText != null) args["alt"] = Str(alternativeText);
            Exec("insertEmoji", args);
        }

        public void InsertImage(RichEditorMediaKind kind = RichEditorMediaKind.Photo, string fileId = null, string src = null, int width = 0, int height = 0, bool hasSpoiler = false, string caption = null)
        {
            var args = new JsonObject
            {
                { "kind", Str(Slug(kind)) },
                { "width", Num(width) },
                { "height", Num(height) },
                { "hasSpoiler", Bool(hasSpoiler) },
            };
            if (fileId != null) args["fileId"] = Str(fileId);
            if (src != null) args["src"] = Str(src);
            if (caption != null) args["caption"] = Str(caption);
            Exec("insertImage", args);
        }

        public void InsertMathInline(string latex) => Exec("insertMathInline", new JsonObject { { "latex", Str(latex) } });
        public void InsertMathBlock(string latex) => Exec("insertMathBlock", new JsonObject { { "latex", Str(latex) } });

        // --- table --------------------------------------------------------------
        public void InsertTable(int rows = 2, int columns = 2) => Exec("insertTable", new JsonObject { { "rows", Num(rows) }, { "cols", Num(columns) } });
        public void TableAddRowAfter() => Exec("tableAddRowAfter");
        public void TableAddRowBefore() => Exec("tableAddRowBefore");
        public void TableAddColumnAfter() => Exec("tableAddColumnAfter");
        public void TableAddColumnBefore() => Exec("tableAddColumnBefore");
        public void TableDeleteRow() => Exec("tableDeleteRow");
        public void TableDeleteColumn() => Exec("tableDeleteColumn");
        public void TableMergeCells() => Exec("tableMergeCells");
        public void TableSplitCell() => Exec("tableSplitCell");
        public void TableToggleHeader() => Exec("tableToggleHeader");
        public void TableDelete() => Exec("tableDelete");
        public void SetCellAlignment(RichEditorCellAlignment align) => Exec("setCellAlign", new JsonObject { { "align", Str(Slug(align)) } });
        public void SetCellVerticalAlignment(RichEditorCellVerticalAlignment valign) => Exec("setCellValign", new JsonObject { { "valign", Str(Slug(valign)) } });

        // --- history ------------------------------------------------------------
        public void Undo() => Exec("undo");
        public void Redo() => Exec("redo");

        // --- theme --------------------------------------------------------------
        /// <summary>Updates the editor accent color and/or light/dark mode at runtime. Either argument is optional.</summary>
        public void SetTheme(Color? accent = null, bool? dark = null)
        {
            var args = new JsonObject();
            if (accent != null) args["accent"] = Str($"#{accent.Value.R:X2}{accent.Value.G:X2}{accent.Value.B:X2}");
            if (dark is bool value) args["dark"] = Bool(value);
            Exec("setTheme", args);
        }

        // --- persistence / queries ----------------------------------------------
        /// <summary>Reads the current document back as a <see cref="RichMessage"/>.</summary>
        public async Task<RichMessage> GetModelAsync(Td.ClientResultHandler handler = null)
        {
            var json = await _webView.ExecuteScriptAsync("UnigramEditor.exec('getModel')");
            if (string.IsNullOrEmpty(json) || json == "null")
            {
                return null;
            }

            ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(json);
            return ClientJson.FromJson(bytes, handler) as RichMessage;
        }

        /// <summary>Loads a document into the editor. Round-trips with <see cref="GetModelAsync"/>.</summary>
        public void SetModel(RichMessage message)
        {
            _ = _webView.ExecuteScriptAsync($"UnigramEditor.exec('setModel', {message.ToJson()})");
        }

        /// <summary>The raw ProseMirror document JSON (mainly for debugging).</summary>
        public async Task<string> GetProseMirrorJsonAsync()
        {
            return await _webView.ExecuteScriptAsync("UnigramEditor.exec('getProseMirrorJSON')");
        }

        /// <summary>The languages offered for code blocks (use for the language dropdown).</summary>
        public async Task<IList<string>> GetCodeLanguagesAsync()
        {
            var json = await _webView.ExecuteScriptAsync("UnigramEditor.exec('getCodeLanguages')");
            var result = new List<string>();
            if (JsonArray.TryParse(json, out var array))
            {
                foreach (var item in array)
                {
                    result.Add(item.GetString());
                }
            }

            return result;
        }

        /// <summary>On-demand pull of the visible custom-emoji rects (the editor also pushes these continuously).</summary>
        public async Task<RichEditorEmojiLayout> GetCustomEmojiAsync()
        {
            var json = await _webView.ExecuteScriptAsync("UnigramEditor.exec('getCustomEmoji')");
            var layout = new RichEditorEmojiLayout();
            if (JsonObject.TryParse(json, out var obj))
            {
                layout.RasterizationScale = obj.GetNamedNumber("dpr", 1);
                layout.Moving = obj.GetNamedBoolean("moving", false);

                foreach (var item in obj.GetNamedArray("emojis", new JsonArray()))
                {
                    var emoji = item.GetObject();
                    layout.Emojis.Add(new RichEditorEmojiPlacement
                    {
                        Id = emoji.GetNamedString("id", string.Empty),
                        X = emoji.GetNamedNumber("x", 0),
                        Y = emoji.GetNamedNumber("y", 0),
                        Width = emoji.GetNamedNumber("w", 0),
                        Height = emoji.GetNamedNumber("h", 0),
                    });
                }
            }

            return layout;
        }

        // --- transport ----------------------------------------------------------
        private void Exec(string command)
        {
            _ = _webView.ExecuteScriptAsync($"UnigramEditor.exec('{command}')");
        }

        private void Exec(string command, JsonObject args)
        {
            _ = _webView.ExecuteScriptAsync($"UnigramEditor.exec('{command}', {args.Stringify()})");
        }

        private static string Slug(Enum value) => value.ToString().ToLowerInvariant();
        private static IJsonValue Str(string value) => Windows.Data.Json.JsonValue.CreateStringValue(value ?? string.Empty);
        private static IJsonValue Num(double value) => Windows.Data.Json.JsonValue.CreateNumberValue(value);
        private static IJsonValue Bool(bool value) => Windows.Data.Json.JsonValue.CreateBooleanValue(value);
    }
}
