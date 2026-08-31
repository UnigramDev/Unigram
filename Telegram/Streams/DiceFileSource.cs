//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.IO.Compression;
using System.Linq;
using System.Threading;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Data.Json;

namespace Telegram.Streams
{
    /// <summary>
    /// One dice message: the state it rolls on, the state it lands on, and how to turn either into
    /// the animation that draws it.
    /// </summary>
    /// <remarks>
    /// Both states live in the one source rather than one source each, because the switch between
    /// them is the whole point. An outgoing dice loops its initial state as a "sending" placeholder
    /// and has to pick the final one up at a loop boundary with no blank frame in between - which
    /// only works while the control's Source stays put, so that the presenter is never torn down
    /// and rebuilt underneath it. DiceAnimatedImageTask does the switching.
    /// </remarks>
    public partial class DiceFileSource : DelayedFileSource
    {
        private readonly DiceStickers _initial;
        private DiceStickers _final;

        // A slot machine is five files and the token the base class keeps only ever covers the one
        // it was handed. One set per state, because the two are different files: sharing a set
        // would have whichever subscribed second unsubscribe the first.
        private readonly long[] _initialTokens = new long[5];
        private readonly long[] _finalTokens = new long[5];

        public DiceFileSource(IClientService clientService, DiceStickers initial, DiceStickers final)
            : base(clientService, PrimaryFile(initial))
        {
            _initial = initial;
            _final = final;

            if (initial is DiceStickersRegular regular)
            {
                // Everything the placeholder needs, and the base class already knows how to ask for
                // it: the file whose outline is fetched, and the size that outline is drawn in.
                SetSticker(regular.Sticker);
            }
            else
            {
                Format = new StickerFormatTgs();
                Width = 512;
                Height = 512;
            }

            // One message, one source: two dice that happen to have rolled the same number are
            // still at different points of their own animation.
            IsUnique = true;

            // The base constructor ran this before the states were assigned, when it could do
            // nothing with them.
            DownloadFile(DelayedFileDownload.Loaded);
        }

        public DiceStickers InitialState => _initial;

        public override void RequestOutline()
        {
            // Only a one-sticker dice has a silhouette worth drawing. A slot machine is five files
            // and the one standing for the source is a reel strip, which is not the shape of
            // anything the message shows. Left unanswered rather than answered with None, because
            // an outline that is known to be absent draws the generic rounded rectangle.
            if (_initial is DiceStickersRegular)
            {
                base.RequestOutline();
            }
        }

        /// <summary>Null until the message has been sent and the server has said what it rolled.</summary>
        public DiceStickers FinalState => Volatile.Read(ref _final);

        /// <summary>
        /// False draws the final state's last frame and nothing else: the result has already been
        /// seen, and re-rolling it every time the message scrolls back into view is not a replay,
        /// it is a lie about when it happened.
        /// </summary>
        /// <remarks>
        /// Read when an animation starts, and settable because the source outlives it: scrolling
        /// away and back builds a new presenter over this same source, by which time the dice the
        /// user just watched land has been marked read.
        /// </remarks>
        public bool IsContentUnread { get; set; }

        /// <summary>
        /// The state the animation opens on. The initial one only while the result is still on its
        /// way, which is the case a dice being sent is in and a settled one never is.
        /// </summary>
        public DiceStickers StartState
        {
            get
            {
                var final = FinalState;
                return final != null && final.IsDownloadingCompleted() ? final : _initial;
            }
        }

        /// <summary>
        /// Whether the dice is still rolling, and so loops rather than plays out. The chat pauses a
        /// looping player that scrolls out of view and leaves a one-shot alone to finish, and this
        /// is how it tells a dice apart.
        /// </summary>
        public bool IsLooping => StartState == _initial;

        /// <summary>
        /// Names the final state, once the server has. The running animation takes it at its next
        /// loop boundary; replacing the source instead would restart the roll.
        /// </summary>
        public void SetFinalState(DiceStickers final)
        {
            if (final == null || FinalState != null)
            {
                return;
            }

            Volatile.Write(ref _final, final);

            Download(final, _finalTokens, DelayedFileDownload.Playing);
        }

        /// <summary>
        /// Whether the animation can start, and deliberately not whether all of it has arrived: the
        /// final state is allowed to land later, and the presenter would otherwise sit on a source
        /// it considers incomplete and never build a task for it.
        /// </summary>
        /// <remarks>
        /// The initial state and nothing else, because this has to agree with
        /// <see cref="TdExtensions.IsAnimatedContentDownloadCompleted"/>, which is what the chat
        /// checks before it will hand a message a viewport. Widening it here to "either state" only
        /// looks like an improvement: a dice whose result arrives first then loads, draws, and is
        /// never given a viewport to play in, because the chat is still waiting for the state this
        /// stopped waiting for.
        /// </remarks>
        public override bool IsDownloadingCompleted => _initial.IsDownloadingCompleted();

        public override void DownloadFile(DelayedFileDownload download)
        {
            // Nothing to drop the priority of: the base class re-asks for its one file, and doing
            // the same here would re-subscribe all ten. The subscriptions live until Complete.
            if (download == DelayedFileDownload.Unloaded)
            {
                return;
            }

            // Both states, always: the roll is what plays while the result is still on its way, and
            // the result is what it changes to. Which of the two is drawn first is StartState's
            // call, not this one's.
            Download(_initial, _initialTokens, download);
            Download(FinalState, _finalTokens, download);

            if (IsDownloadingCompleted)
            {
                OnDownloaded();
            }
        }

        public override void Complete()
        {
            base.Complete();

            for (int i = 0; i < _initialTokens.Length; i++)
            {
                UpdateManager.Unsubscribe(this, ref _initialTokens[i]);
                UpdateManager.Unsubscribe(this, ref _finalTokens[i]);
            }
        }

        private void Download(DiceStickers state, long[] tokens, DelayedFileDownload download)
        {
            if (state is DiceStickersRegular regular)
            {
                Download(regular.Sticker, ref tokens[0], download);
            }
            else if (state is DiceStickersSlotMachine slotMachine)
            {
                Download(slotMachine.Background, ref tokens[0], download);
                Download(slotMachine.LeftReel, ref tokens[1], download);
                Download(slotMachine.CenterReel, ref tokens[2], download);
                Download(slotMachine.RightReel, ref tokens[3], download);
                Download(slotMachine.Lever, ref tokens[4], download);
            }
        }

        private void Download(Sticker sticker, ref long token, DelayedFileDownload download)
        {
            var file = sticker.StickerValue;

            // Every part watched, and OnFileUpdated deciding: a dice is complete only once the
            // whole state is on disk, and one that starts drawing with three of its five reels
            // present draws nothing.
            UpdateManager.Subscribe(this, _clientService, file, ref token, OnFileUpdated, true);

            if (file.Local.CanBeDownloaded)
            {
                _clientService.DownloadFile(file.Id, download == DelayedFileDownload.Playing ? 16 : 15);
            }
        }

        /// <summary>
        /// The animation a state draws as. A slot machine's three reels are merged into one rather
        /// than layered as three, because they share a timeline: three animations that only agreed
        /// on a frame rate would drift apart, and the reels have to stop together.
        /// </summary>
        /// <remarks>
        /// Never cached. The merged document exists only for this roll, and a cache key short of
        /// all three files would hand the next roll the wrong reels.
        /// </remarks>
        public static LottieAnimation CreateReels(DiceStickers state, int pixelWidth, int pixelHeight)
        {
            if (state is DiceStickersRegular regular)
            {
                return LottieAnimation.LoadFromFile(regular.Sticker.StickerValue.Local.Path, pixelWidth, pixelHeight, false, null);
            }
            else if (state is DiceStickersSlotMachine slotMachine)
            {
                return LottieAnimation.LoadFromData(MergeReels(slotMachine), pixelWidth, pixelHeight, string.Empty, false, null);
            }

            return null;
        }

        public static LottieAnimation CreateBackground(DiceStickers state, int pixelWidth, int pixelHeight)
        {
            return state is DiceStickersSlotMachine slotMachine
                ? LottieAnimation.LoadFromFile(slotMachine.Background.StickerValue.Local.Path, pixelWidth, pixelHeight, false, null)
                : null;
        }

        public static LottieAnimation CreateLever(DiceStickers state, int pixelWidth, int pixelHeight)
        {
            return state is DiceStickersSlotMachine slotMachine
                ? LottieAnimation.LoadFromFile(slotMachine.Lever.StickerValue.Local.Path, pixelWidth, pixelHeight, false, null)
                : null;
        }

        /// <summary>
        /// Folds the centre and right reels into the left one's document, prefixing their asset ids
        /// with the layer name so that the three sets cannot collide.
        /// </summary>
        private static string MergeReels(DiceStickersSlotMachine slotMachine)
        {
            var left = JsonObject.Parse(DecompressReel(slotMachine.LeftReel.StickerValue.Local.Path));
            var center = JsonObject.Parse(DecompressReel(slotMachine.CenterReel.StickerValue.Local.Path));
            var right = JsonObject.Parse(DecompressReel(slotMachine.RightReel.StickerValue.Local.Path));

            var assets = left.GetNamedArray("assets");
            var layers = left.GetNamedArray("layers");

            foreach (var part in new[] { center, right })
            {
                var name = part.GetNamedString("nm");

                foreach (var asset in part.GetNamedArray("assets").Select(x => x.GetObject()))
                {
                    asset.SetNamedValue("id", Windows.Data.Json.JsonValue.CreateStringValue($"{name}_{asset.GetNamedString("id")}"));
                    assets.Add(asset);
                }

                foreach (var layer in part.GetNamedArray("layers").Select(x => x.GetObject()))
                {
                    if (layer.TryGetValue("refId", out var refId))
                    {
                        layer.SetNamedValue("refId", Windows.Data.Json.JsonValue.CreateStringValue($"{name}_{refId.GetString()}"));
                    }

                    layers.Add(layer);
                }
            }

            return left.ToString();
        }

        private static string DecompressReel(string path)
        {
            using var file = System.IO.File.OpenRead(path);
            using var stream = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new System.IO.StreamReader(stream);

            return reader.ReadToEnd();
        }

        // The base class needs one file to stand for the source - it is what FilePath and Id come
        // back as. Any part would do, so it is the one a regular dice actually draws from.
        private static File PrimaryFile(DiceStickers state)
        {
            return state switch
            {
                DiceStickersRegular regular => regular.Sticker.StickerValue,
                DiceStickersSlotMachine slotMachine => slotMachine.CenterReel.StickerValue,
                _ => null
            };
        }
    }
}
