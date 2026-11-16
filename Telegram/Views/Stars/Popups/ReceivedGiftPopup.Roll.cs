//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    // The reveal after an upgrade. The header spins through the preview's variants and lands on
    // the gift that came out of it: backdrop first, then symbol, then model, each slowing down for
    // a couple more variants once it is allowed to stop. Positions are in variants: 0.5 is centred,
    // and every attribute is drawn at its offset from the roller's current one.
    public sealed partial class ReceivedGiftPopup
    {
        private sealed class RollAttribute
        {
            public RollAttribute(string name, UpgradedGiftAttributeRarity rarity, Sticker sticker, UpgradedGiftBackdrop backdrop, File gate)
            {
                Name = name;
                Rarity = rarity;
                Sticker = sticker;
                Backdrop = backdrop;
                Gate = gate;
            }

            public string Name { get; }

            public UpgradedGiftAttributeRarity Rarity { get; }

            public Sticker Sticker { get; }

            public UpgradedGiftBackdrop Backdrop { get; }

            /// <summary>
            /// The file that has to be on disk before the attribute may come up; a model that has
            /// not loaded would spin past as an empty slot.
            /// </summary>
            public File Gate { get; }
        }

        private sealed class AttributeRoller
        {
            private readonly ReceivedGiftPopup _owner;
            private readonly List<RollAttribute> _attributes;
            private readonly Random _random;

            private readonly float _speed;
            private readonly int _totalSlowing;
            private int _slowing;
            private int _lastIndex = -1;

            private float _fast = 1;
            private float _fastFrom = 1;
            private float _fastTarget = 1;
            private float _fastElapsed;

            public AttributeRoller(ReceivedGiftPopup owner, List<RollAttribute> attributes, RollAttribute start, RollAttribute finish, float speed, int totalSlowing, Random random)
            {
                _owner = owner;
                _attributes = attributes;
                _random = random;
                _speed = speed;
                _totalSlowing = totalSlowing;
                _slowing = totalSlowing;

                Start = start;
                Finish = finish;

                Prev = start;
                Current = NextAttribute(false);
                Next = NextAttribute(false);
            }

            public RollAttribute Start { get; }

            public RollAttribute Finish { get; }

            public RollAttribute Prev { get; private set; }

            public RollAttribute Current { get; private set; }

            public RollAttribute Next { get; private set; }

            public int CurrentT { get; private set; } = 1;

            public float Time { get; private set; } = -0.5f;

            public bool IsFinished => Current == Finish && Time >= CurrentT + 0.5f;

            public bool IsAlmostFinished(float offset)
            {
                return Current == Finish && Time + offset >= CurrentT + 0.5f;
            }

            public void Skip()
            {
                Prev = Current;
                Current = Finish;
                Next = null;
                CurrentT++;
                Time = CurrentT + 0.5f;
            }

            public float Step(float deltaTime, bool allowFinish)
            {
                // Full speed until the first slowing variant is drawn, then an ease-out into the
                // slow speed, longer for the roller that has more variants left to show.
                var target = _slowing >= _totalSlowing ? 1f : 0f;
                var duration = _slowing >= _totalSlowing ? 0.45f : _totalSlowing == 3 ? 4.5f : 2.5f;

                if (target != _fastTarget)
                {
                    _fastFrom = _fast;
                    _fastTarget = target;
                    _fastElapsed = 0;
                }

                if (_fast != _fastTarget)
                {
                    _fastElapsed += deltaTime;

                    var progress = Math.Min(1, _fastElapsed / duration);
                    var eased = 1 - (float)Math.Pow(1 - progress, 5);

                    _fast = progress >= 1 ? _fastTarget : Lerp(_fastFrom, _fastTarget, eased);
                }

                Time += deltaTime * Lerp(_totalSlowing == 3 ? 0.75f : 2.0f, 7.5f, _fast) * _speed;

                var t = Time;

                if (t >= 0 && Math.Floor(t) + 1 > CurrentT && Current != Finish)
                {
                    Prev = Current;
                    Current = Next;
                    Next = Current == Finish ? null : NextAttribute(allowFinish);
                    CurrentT = (int)Math.Floor(t) + 1;
                }

                if (Current == Finish)
                {
                    t = Math.Min(t, CurrentT + 0.5f);
                }

                return t;
            }

            private RollAttribute NextAttribute(bool allowFinish)
            {
                if (allowFinish && _owner.IsRollLoaded(Finish))
                {
                    if (_slowing <= 0)
                    {
                        return Finish;
                    }

                    _slowing--;
                }

                // A random loaded variant, never the one just picked. If that leaves nothing, the
                // one just picked is allowed again, and failing that the roll marks time on the start.
                var count = 0;
                for (int i = 0; i < _attributes.Count; i++)
                {
                    if (i != _lastIndex && _owner.IsRollLoaded(_attributes[i]))
                    {
                        count++;
                    }
                }

                var avoidLast = count > 0;
                if (avoidLast is false && _lastIndex >= 0 && _owner.IsRollLoaded(_attributes[_lastIndex]))
                {
                    count = 1;
                }

                if (count == 0)
                {
                    return Start;
                }

                var pick = _random.Next(count);

                for (int i = 0; i < _attributes.Count; i++)
                {
                    if ((avoidLast && i == _lastIndex) || !_owner.IsRollLoaded(_attributes[i]))
                    {
                        continue;
                    }

                    if (pick-- == 0)
                    {
                        _lastIndex = i;
                        return _attributes[i];
                    }
                }

                return Start;
            }
        }

        private sealed class RollRow
        {
            public RollRow(FrameworkElement host, TextBlock[] labels, SettingsButton rarity)
            {
                Host = host;
                Labels = labels;
                Rarity = rarity;
            }

            public FrameworkElement Host { get; }

            public TextBlock[] Labels { get; }

            public RollAttribute[] Shown { get; } = new RollAttribute[3];

            public SettingsButton Rarity { get; }

            public RollAttribute RarityShown;

            public TextBlock Bouncing;
            public float BounceStart = -1;
            public bool Bounced;
        }

        // Android draws a 160 px model 220 px apart and rolls labels by 36 px; scaled to a 120 px
        // model and to the height of a table row.
        private const float RollModelTravel = 165;
        private const float RollLabelTravel = 32;

        private AnimatedImage[] _rollImages;
        private CompositeTransform[] _rollTransforms;
        private PlaneProjection[] _rollProjections;
        private readonly RollAttribute[] _rollImagesShown = new RollAttribute[3];

        private RollRow _modelRow;
        private RollRow _backdropRow;
        private RollRow _symbolRow;

        private AttributeRoller _modelRoller;
        private AttributeRoller _symbolRoller;
        private AttributeRoller _backdropRoller;

        // The backdrop label rolls at its own speed; the header follows the slower roller.
        private AttributeRoller _backdropLabelRoller;

        private UpgradedGift _rollGift;
        private RollAttribute _rollPatternSymbol;
        private RollAttribute _rollPatternBackdrop;
        private float _rollTime;
        private float _rollDuration;
        private long _rollTimestamp;
        private bool _rolling;
        private bool _rollAlmostDone;

        private readonly HashSet<int> _rollLoaded = new();

        // The preview variant on screen when the upgrade went through, where the roll starts.
        private UpgradedGiftModel _shownModel;
        private UpgradedGiftSymbol _shownSymbol;
        private UpgradedGiftBackdrop _shownBackdrop;

        private bool IsRollLoaded(RollAttribute attribute)
        {
            return attribute.Gate == null || attribute.Gate.Local.IsDownloadingCompleted || _rollLoaded.Contains(attribute.Gate.Id);
        }

        private async void PreloadRoll(File file, int priority)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                _rollLoaded.Add(file.Id);
                return;
            }

            var updated = await _clientService.DownloadFileAsync(file, priority);
            if (updated.Local.IsDownloadingCompleted)
            {
                _rollLoaded.Add(file.Id);
            }
        }

        private static RollAttribute ToRollAttribute(UpgradedGiftModel model)
        {
            return new RollAttribute(model.Name, model.Rarity, model.Sticker, null, model.Sticker.StickerValue);
        }

        private static RollAttribute ToRollAttribute(UpgradedGiftSymbol symbol)
        {
            return new RollAttribute(symbol.Name, symbol.Rarity, symbol.Sticker, null, null);
        }

        private static RollAttribute ToRollAttribute(UpgradedGiftBackdrop backdrop)
        {
            return new RollAttribute(backdrop.Name, backdrop.Rarity, null, backdrop, null);
        }

        /// <summary>
        /// Starts the reveal over the details of the upgraded gift, already initialized; returns
        /// false when there is nothing to roll from.
        /// </summary>
        private bool StartRoll(UpgradedGift gift)
        {
            if (_preview == null || _shownModel == null)
            {
                return false;
            }

            if (_rollImages == null)
            {
                _rollImages = new[] { RollModel0, RollModel1, RollModel2 };
                _rollTransforms = new[] { RollModel0Transform, RollModel1Transform, RollModel2Transform };
                _rollProjections = new[] { RollModel0Projection, RollModel1Projection, RollModel2Projection };

                _modelRow = new RollRow(UpgradedModelRoll, new[] { UpgradedModel, UpgradedModelRoll1, UpgradedModelRoll2 }, UpgradedModelRarity);
                _backdropRow = new RollRow(UpgradedBackdropRoll, new[] { UpgradedBackdrop, UpgradedBackdropRoll1, UpgradedBackdropRoll2 }, UpgradedBackdropRarity);
                _symbolRow = new RollRow(UpgradedSymbolRoll, new[] { UpgradedSymbol, UpgradedSymbolRoll1, UpgradedSymbolRoll2 }, UpgradedSymbolRarity);
            }

            var models = new List<RollAttribute>(_preview.Models.Count);
            var symbols = new List<RollAttribute>(_preview.Symbols.Count);
            var backdrops = new List<RollAttribute>(_preview.Backdrops.Count);

            foreach (var model in _preview.Models)
            {
                models.Add(ToRollAttribute(model));
            }

            foreach (var symbol in _preview.Symbols)
            {
                symbols.Add(ToRollAttribute(symbol));
            }

            foreach (var backdrop in _preview.Backdrops)
            {
                backdrops.Add(ToRollAttribute(backdrop));
            }

            PreloadRoll(gift.Model.Sticker.StickerValue, 32);
            PreloadRoll(gift.Symbol.Sticker.StickerValue, 32);

            var random = new Random();
            var finishBackdrop = ToRollAttribute(gift.Backdrop);
            var startBackdrop = ToRollAttribute(_shownBackdrop);

            // A long roll gives every roller one more slowing variant.
            _rollDuration = (float)random.NextDouble();

            var slow = _rollDuration > 0.5f;

            _modelRoller = new AttributeRoller(this, models, ToRollAttribute(_shownModel), ToRollAttribute(gift.Model), 0.9f, slow ? 3 : 2, random);
            _symbolRoller = new AttributeRoller(this, symbols, ToRollAttribute(_shownSymbol), ToRollAttribute(gift.Symbol), 1.0f, slow ? 2 : 1, random);
            _backdropRoller = new AttributeRoller(this, backdrops, startBackdrop, finishBackdrop, 0.5f, slow ? 2 : 1, random);
            _backdropLabelRoller = new AttributeRoller(this, backdrops, startBackdrop, finishBackdrop, 1.25f, slow ? 2 : 1, random);

            _rollGift = gift;
            _rollTime = 0;
            _rollAlmostDone = false;
            _rollPatternSymbol = null;
            _rollPatternBackdrop = null;

            Array.Clear(_rollImagesShown, 0, 3);
            PrepareRow(_modelRow);
            PrepareRow(_backdropRow);
            PrepareRow(_symbolRow);

            UpgradedAnimatedPhoto.Opacity = 0;
            RollModels.Visibility = Visibility.Visible;

            PrimaryButtonText = Strings.GiftSkipAnimation;

            _rolling = true;
            _rollTimestamp = Stopwatch.GetTimestamp();

            CompositionTarget.Rendering += OnRollRendering;
            return true;
        }

        private void PrepareRow(RollRow row)
        {
            // The labels slide past the row's edges; clip them to it.
            var visual = ElementCompositionPreview.GetElementVisual(row.Host);
            visual.Clip ??= visual.Compositor.CreateInsetClip();

            Array.Clear(row.Shown, 0, 3);
            row.RarityShown = null;
            row.Bouncing = null;
            row.BounceStart = -1;
            row.Bounced = false;

            foreach (var label in row.Labels)
            {
                label.Visibility = Visibility.Visible;
                label.Opacity = 0;
            }
        }

        private void SkipRoll()
        {
            _modelRoller.Skip();
            _symbolRoller.Skip();
            _backdropRoller.Skip();
            _backdropLabelRoller.Skip();
        }

        private void StopRoll()
        {
            if (_rolling)
            {
                _rolling = false;
                CompositionTarget.Rendering -= OnRollRendering;
            }
        }

        private void OnRollRendering(object sender, object e)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var deltaTime = Math.Min((timestamp - _rollTimestamp) / (float)Stopwatch.Frequency, 0.25f);

            _rollTimestamp = timestamp;
            _rollTime += deltaTime;

            // The backdrop may stop after a moment; each roller after it waits until the one before
            // has nearly settled, so the attributes land one at a time.
            var backdropMayFinish = _rollTime > Lerp(0.10f, 1.0f, _rollDuration);

            var backdropT = _backdropRoller.Step(deltaTime, backdropMayFinish);
            var backdropLabelT = _backdropLabelRoller.Step(deltaTime, backdropMayFinish);
            var symbolT = _symbolRoller.Step(deltaTime, _backdropRoller.IsAlmostFinished(0.5f));
            var modelT = _modelRoller.Step(deltaTime, _backdropRoller.IsAlmostFinished(0.5f) && _symbolRoller.IsAlmostFinished(0.5f));

            DrawBackdrops(backdropT);
            DrawPattern();
            DrawModels(modelT);
            DrawRow(_modelRow, _modelRoller, modelT);
            DrawRow(_backdropRow, _backdropLabelRoller, backdropLabelT);
            DrawRow(_symbolRow, _symbolRoller, symbolT);

            var settled = _backdropRoller.IsFinished && _symbolRoller.IsFinished;

            if (settled && _modelRoller.IsAlmostFinished(0.25f) && !_rollAlmostDone)
            {
                _rollAlmostDone = true;

                _aggregator.Publish(new UpdateConfetti());
                ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2UpgradedTitle, string.Format(Strings.Gift2UpgradedText, _rollGift.ToName())), ToastPopupIcon.Gift);
            }

            if (settled && _modelRoller.IsFinished)
            {
                LandRoll();
            }
        }

        private static int RollSlot(int currentT, int offset)
        {
            // Prev, current and next keep their slot as the roller advances: the slot that held
            // prev is the one the new next is loaded into, while it is still out of sight.
            return ((currentT + offset) % 3 + 3) % 3;
        }

        private void DrawBackdrops(float t)
        {
            var roller = _backdropRoller;
            var offset = roller.CurrentT - t;

            UpgradedHeader.SetRollBackdrop(RollSlot(roller.CurrentT, -1), roller.Prev?.Backdrop, offset - 1);
            UpgradedHeader.SetRollBackdrop(RollSlot(roller.CurrentT, 0), roller.Current?.Backdrop, offset);
            UpgradedHeader.SetRollBackdrop(RollSlot(roller.CurrentT, 1), roller.Next?.Backdrop, offset + 1);
        }

        private void DrawPattern()
        {
            var symbol = _symbolRoller.Current;
            var backdrop = _backdropRoller.Current;

            if (symbol == _rollPatternSymbol && backdrop == _rollPatternBackdrop)
            {
                return;
            }

            if (symbol != _rollPatternSymbol)
            {
                _rollPatternSymbol = symbol;
                _rollPatternBackdrop = backdrop;
                UpgradedHeader.UpdatePattern(new DelayedFileSource(_clientService, symbol.Sticker), backdrop.Backdrop.Colors.SymbolColor.ToColor());
            }
            else
            {
                _rollPatternBackdrop = backdrop;
                UpgradedHeader.UpdatePatternColor(backdrop.Backdrop.Colors.SymbolColor.ToColor());
            }
        }

        private void DrawModels(float t)
        {
            var roller = _modelRoller;
            var offset = roller.CurrentT - t;

            DrawModel(RollSlot(roller.CurrentT, -1), roller.Prev, offset - 1);
            DrawModel(RollSlot(roller.CurrentT, 0), roller.Current, offset);
            DrawModel(RollSlot(roller.CurrentT, 1), roller.Next, offset + 1);
        }

        private void DrawModel(int slot, RollAttribute attribute, float position)
        {
            var image = _rollImages[slot];

            if (attribute == null)
            {
                image.Opacity = 0;
                return;
            }

            if (_rollImagesShown[slot] != attribute)
            {
                _rollImagesShown[slot] = attribute;
                image.Source = new DelayedFileSource(_clientService, attribute.Sticker);
            }

            if (attribute == _modelRoller.Finish)
            {
                position = Math.Max(0.5f, position);
            }

            // Each model passes on a cylinder: it slides across, turns away from the centre and
            // shrinks and fades the further it is from it.
            var tt = (position - 0.5f) / 1.5f;
            var alpha = Math.Clamp(1 - Math.Abs(tt), 0, 1);
            var scale = Lerp(0.85f, 1, alpha);

            var transform = _rollTransforms[slot];
            transform.TranslateX = -tt * RollModelTravel;
            transform.ScaleX = scale;
            transform.ScaleY = scale;

            var projection = _rollProjections[slot];
            projection.RotationY = -30 * tt;
            projection.CenterOfRotationX = 0.5 + 0.5 * tt;

            image.Opacity = alpha;
        }

        private void DrawRow(RollRow row, AttributeRoller roller, float t)
        {
            var offset = roller.CurrentT - t;

            DrawLabel(row, roller, RollSlot(roller.CurrentT, -1), roller.Prev, offset - 1);
            DrawLabel(row, roller, RollSlot(roller.CurrentT, 0), roller.Current, offset);
            DrawLabel(row, roller, RollSlot(roller.CurrentT, 1), roller.Next, offset + 1);

            if (row.RarityShown != roller.Current && roller.Current != null)
            {
                row.RarityShown = roller.Current;
                row.Rarity.Glyph = roller.Current.Rarity.ToText();
                roller.Current.Rarity.ToColor(row.Rarity);
            }

            // The label that lands gives a small bump, once.
            if (!row.Bounced && roller.Current == roller.Finish && offset <= 0.5f)
            {
                row.Bounced = true;
                row.Bouncing = row.Labels[RollSlot(roller.CurrentT, 0)];
                row.BounceStart = _rollTime;
            }

            if (row.BounceStart >= 0)
            {
                var progress = (_rollTime - row.BounceStart) / 0.18f;
                var scale = progress >= 1 ? 1 : 1 + 0.03f * (float)Math.Sin(Math.PI * progress);

                row.Bouncing.CenterPoint = new Vector3(row.Bouncing.ActualSize / 2, 0);
                row.Bouncing.Scale = new Vector3(scale, scale, 1);

                if (progress >= 1)
                {
                    row.BounceStart = -1;
                }
            }
        }

        private static void DrawLabel(RollRow row, AttributeRoller roller, int slot, RollAttribute attribute, float position)
        {
            var label = row.Labels[slot];

            if (attribute == null)
            {
                label.Opacity = 0;
                return;
            }

            if (row.Shown[slot] != attribute)
            {
                row.Shown[slot] = attribute;
                label.Text = attribute.Name;
            }

            if (attribute == roller.Finish)
            {
                position = Math.Max(0.5f, position);
            }

            label.Translation = new Vector3(0, RollLabelTravel * (position - 0.5f) / 1.5f, 0);
            label.Opacity = 1;
        }

        private void LandRoll()
        {
            StopRoll();

            var gift = _rollGift;
            _rollGift = null;

            UpgradedHeader.ClearRoll();
            UpgradedHeader.Update(_clientService, gift);

            RollModels.Visibility = Visibility.Collapsed;

            foreach (var image in _rollImages)
            {
                image.Source = null;
            }

            UpgradedAnimatedPhoto.Opacity = 1;
            UpgradedAnimatedPhoto.Play();

            ResetRow(_modelRow);
            ResetRow(_backdropRow);
            ResetRow(_symbolRow);

            UpdateAttributes(gift);

            PrimaryButtonText = GetUpgradedPrimaryButtonText(gift);
        }

        private static void ResetRow(RollRow row)
        {
            // The first label is the row's own; the others only exist for the roll.
            for (int i = 0; i < row.Labels.Length; i++)
            {
                var label = row.Labels[i];
                label.Translation = Vector3.Zero;
                label.Scale = Vector3.One;
                label.Opacity = 1;

                if (i > 0)
                {
                    label.Visibility = Visibility.Collapsed;
                    label.Text = string.Empty;
                }
            }
        }

        private static float Lerp(float from, float to, float progress)
        {
            return from + (to - from) * progress;
        }
    }
}
