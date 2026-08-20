//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Wallet;
using Telegram.Views.Wallet.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Wallet
{
    public sealed partial class WalletPage : HostedPage
    {
        public WalletViewModel ViewModel => DataContext as WalletViewModel;

        public WalletPage()
        {
            InitializeComponent();

            Title = "[Wallet]";

            //Card.Constraint = new Size(85.60, 53.98);
            Card.Constraint = new Size(360, 220);
            Card.SizeChanged += Card_SizeChanged;

            CardAddress.SizeChanged += CardAddress_SizeChanged;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            //        background: linear - gradient(0deg, #0079FF, #0079FF),
            //conic - gradient(from 20.99deg at 50 % 50 %, #0079FF 0deg, #169AF9 90deg, #0079FF 180deg, #169AF9 270deg, #0079FF 360deg);

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.AttachTilt(Card, Sheen, 10, 20, 0, OnCardTilt);

            // box-shadow: 0px 1px 0px 0px rgba(255, 255, 255, 0.06)
            //
            // No blur and a single pixel down: this is the highlight that makes the
            // address read as engraved into the card rather than printed on it. Here
            // rather than in the markup because the mask comes from the glyphs, which
            // only exist once the text has been laid out.
            VisualUtilities.DropShadow(CardAddress, radius: 0, opacity: 1.0f,
                target: CardAddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.DetachTilt(Card);

            // Detaching stops the per-frame callback wherever it happens to be, and the
            // page can be navigated back to with its surface intact, so the bands are
            // reset explicitly.
            OnCardTilt(Vector2.Zero, 0);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAddress(ViewModel.Address);
            UpdateBalance(ViewModel.Balance);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Address))
            {
                UpdateAddress(ViewModel.Address);
            }
            else if (e.PropertyName == nameof(ViewModel.Balance))
            {
                UpdateBalance(ViewModel.Balance);
            }
        }

        private void UpdateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                CardAddress.Text = string.Empty;
            }
            else
            {
                var builder = new StringBuilder();

                for (int i = 0; i < address.Length; i += 4)
                {
                    if (i > 0)
                    {
                        builder.Append(i == 24 ? "\n" : " ");
                    }

                    builder.Append(address.Substring(i, 4).ToUpperInvariant());
                }

                CardAddress.Text = builder.ToString();

                if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
                {
                    CardName.Text = user.FullName().ToUpperInvariant();
                }
            }
        }

        private void UpdateBalance(BigInteger balance)
        {
            var amount = Formatter.TonBalance(balance);
            CardBalance.Text = amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;
            // million_gram_to_usd_rate is whole dollars per 1,000,000 grams, and a balance is
            // in nanograms: 1e9 to grams, 1e6 more to millions, 100 back for cents. The
            // multiply stays exact and only the formatting step goes to double.
            var rate = ViewModel.ClientService.Options.MillionGramToUsdRate;
            var cents = balance * rate / BigInteger.Pow(10, 13);
            CardBalanceUsd.Text = string.Format("${0:N2}", (double)cents / 100d);
        }

        private void Card_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Card.CornerRadius = new CornerRadius(e.NewSize.Width * (3.18 / 85.60));
            UpdateSheen(e.NewSize);
        }

        // Roughly half the resolution this is painted at: the tilt scales the sheen to
        // twice the card so no rotation can swing an empty corner in.
        //
        // A gradient tolerates magnification, having no detail to lose, and the dither
        // below moves each texel by well under one output level, so magnifying it
        // cannot turn the Bayer pattern into a visible weave. Resolution is traded away
        // because the surface is rebuilt every frame the card is held, at a per-texel
        // cost.
        private const int SheenMaxWidth = 360;

        // How far the bands breathe at the card's edge, in octaves of the shaping
        // exponent. Around 1 the widest arms are roughly twice the narrowest, which
        // matches a flat lacquered surface. Much beyond that the dark arms pinch shut.
        private const float SheenBandWidth = 1.1f;

        // Bayer offsets are +/-0.47, and one level of the widest channel (green, 33
        // levels across the ramp) is just under 8 steps of the 256-entry index, placing
        // the dither at about +/-0.5 of an output level.
        private const float SheenDither = 8f;

        private WriteableBitmap _sheenBitmap;
        private byte[] _sheenAngles;
        private uint[] _sheenColors;
        private unsafe uint* _sheenPixels;

        /// <summary>
        /// Rebuilds the angle map behind the sheen, at the card's own aspect ratio.
        ///
        /// Holds the expensive half of the gradient: one Atan2 per texel, reduced to a
        /// position along the ramp. It depends only on the geometry, so it survives
        /// every frame. The tilt varies the shape of the ramp, which is 256 values
        /// rebuilt in <see cref="OnCardTilt"/>.
        ///
        /// The aspect matters because the map is stretched to fill the card: generated
        /// square, every arm comes out sheared.
        /// </summary>
        private void UpdateSheen(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            var width = (int)Math.Min(size.Width, SheenMaxWidth);
            var height = (int)(width * (size.Height / size.Width));

            // The card is laid out against a constraint, so it settles at one size
            // and then gets the same size again on every reflow of the list.
            if (width <= 0 || height <= 0 || (_sheenBitmap != null
                && _sheenBitmap.PixelWidth == width && _sheenBitmap.PixelHeight == height))
            {
                return;
            }

            _sheenAngles = new byte[width * height];
            FillConicAngles(_sheenAngles, width, height);

            _sheenBitmap = new WriteableBitmap(width, height);

            // Taken once. The blit below runs every frame the card is held, and going
            // back through PixelBuffer would put a WinRT cast, and its allocation, on
            // that path. The buffer belongs to the bitmap and is stable for its
            // lifetime.
            unsafe
            {
                _sheenBitmap.Buffer(out byte* buffer);
                _sheenPixels = (uint*)buffer;
            }

            if (Sheen.Fill is ImageBrush brush)
            {
                brush.ImageSource = _sheenBitmap;
            }
            else
            {
                Sheen.Fill = new ImageBrush
                {
                    ImageSource = _sheenBitmap
                };
            }

            // At rest, so the card is not blank until the first press.
            OnCardTilt(Vector2.Zero, 0);
        }

        /// <summary>
        /// Repaints the sheen for one frame of the tilt.
        ///
        /// This part must be a redraw. Rotating the gradient is a rigid transform and
        /// belongs on the compositor, where AttachTilt already performs it at no cost;
        /// redrawing a conic at an angle would produce identical pixels, since a conic
        /// gradient is rotation invariant. Bands that widen and close are not a
        /// transform: the ramp between the two blues changes shape, which requires a
        /// new surface.
        ///
        /// The cost is kept off the per-texel loop rather than off the frame. Every
        /// Atan2 is already in the angle map, so a frame costs 256 calls to Pow plus one
        /// indexed read and one write per texel.
        /// </summary>
        private void OnCardTilt(Vector2 pointer, float amount)
        {
            if (_sheenAngles == null)
            {
                return;
            }

            // The same signed horizontal lean, under the same radial clamp, that the
            // sheen's rotation uses, so the bands open towards the side the card is
            // turning rather than drifting out of step with the sweep.
            var lean = pointer.X / MathF.Max(pointer.Length(), 1) * amount;

            // f^k over the triangle ramp. Below 1 the ramp bows up and the light arms
            // spread into the dark ones; above 1 it sags and they close around their
            // peaks. Octaves, so the two directions travel equally.
            var exponent = MathF.Pow(2, SheenBandWidth * lean);

            var colors = _sheenColors ??= new uint[256];

            for (int i = 0; i < 256; i++)
            {
                var f = MathF.Pow(i * (1f / 255f), exponent);

                // #0079FF -> #169AF9. Alpha is opaque throughout, so premultiplied
                // BGRA is just the channels in little-endian order.
                var b = (uint)(255f - 6f * f + 0.5f);
                var g = (uint)(121f + 33f * f + 0.5f);
                var r = (uint)(0f + 22f * f + 0.5f);

                colors[i] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }

            var angles = _sheenAngles;

            unsafe
            {
                var pixels = _sheenPixels;

                for (int i = 0; i < angles.Length; i++)
                {
                    pixels[i] = colors[angles[i]];
                }
            }

            _sheenBitmap.Invalidate();
        }

        private void CardAddress_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CardAddressShadow.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
            CardAddress.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
        }

        private static readonly float[] Bayer4x4 =
        {
            -0.46875f,  0.03125f, -0.34375f,  0.15625f,
             0.28125f, -0.21875f,  0.40625f, -0.09375f,
            -0.28125f,  0.21875f, -0.40625f,  0.09375f,
             0.46875f, -0.03125f,  0.34375f, -0.15625f,
        };

        /// <summary>
        /// Where each texel sits along the ramp of
        ///
        /// conic-gradient(from 20.99deg at 50% 50%, #0079FF 0deg, #169AF9 90deg,
        /// #0079FF 180deg, #169AF9 270deg, #0079FF 360deg)
        ///
        /// quantized to the 256 entries <see cref="OnCardTilt"/> colours it through.
        /// 0 is #0079FF and 255 is #169AF9. The arms follow from the triangle over the
        /// doubled angle, giving two of each per turn.
        ///
        /// The dither is applied here rather than at colouring time: it belongs to the
        /// position along the ramp, not to the colours, so it survives the per-frame
        /// shaping at no recurring cost.
        /// </summary>
        private static void FillConicAngles(Span<byte> destination, int width, int height)
        {
            if (destination.Length < width * height)
                throw new ArgumentException("Destination too small.", nameof(destination));

            const float PiInv = 1f / MathF.PI;
            const float Phase = 0.38338889f;   // 0.5f - 41.98f / 360f

            float cx = width * 0.5f;
            float cy = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float dy = y + 0.5f - cy;
                int bayerRow = (y & 3) << 2;
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    float dx = x + 0.5f - cx;

                    float s = MathF.Atan2(dy, dx) * PiInv + Phase;
                    s -= MathF.Floor(s);                    // [0, 1)
                    float f = 1f - MathF.Abs(s * 2f - 1f);  // 0 = #0079FF, 1 = #169AF9

                    float v = f * 255f + Bayer4x4[bayerRow | (x & 3)] * SheenDither;
                    destination[row + x] = (byte)Math.Clamp(v + 0.5f, 0f, 255f);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(WalletBackupPage));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.ShowPopup(new WalletSharePopup(ViewModel.Address));
        }
    }
}
