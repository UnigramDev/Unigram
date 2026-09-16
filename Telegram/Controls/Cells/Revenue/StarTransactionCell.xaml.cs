//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class StarTransactionCell : Grid
    {
        private readonly Brush _received;

        public StarTransactionCell()
        {
            InitializeComponent();

            // Read once: this runs while the list scrolls, and a resource lookup walks the tree.
            // Safe to hold, being made of a colour that follows the theme - unlike the brush the
            // amount is drawn in otherwise, which the theme replaces rather than repaints.
            _received = Resources["AmountReceivedBrush"] as Brush;
        }

        private long _media1Token;
        private long _media2Token;

        public void UpdateInfo(IClientService clientService, StarTransaction transaction)
        {
            UpdateManager.Unsubscribe(this, ref _media1Token);
            UpdateManager.Unsubscribe(this, ref _media2Token);

            var info = TransactionInfo.FromStarTransaction(clientService, transaction);

            Title.Text = info.Title;

            if (info.Subtitle != null)
            {
                Subtitle.Text = info.Subtitle;
                Subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
            }

            if (info.Media?.Count > 0)
            {
                // The thumbnails cover the avatar, so its source is left untouched rather
                // than assigned and downloaded for nothing.
                Photo.Visibility = Visibility.Collapsed;
                MediaPreview.Visibility = Visibility.Visible;

                Media1.Background = null;
                Media2.Background = null;

                UpdateMedia(clientService, info.Media[0], Media1, ref _media1Token);

                if (info.Media.Count > 1)
                {
                    UpdateMedia(clientService, info.Media[1], Media2, ref _media2Token);

                    Media2.Visibility = Visibility.Visible;
                }
                else
                {
                    Media2.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Photo.Source = info.Photo;
                Photo.Visibility = Visibility.Visible;
                MediaPreview.Visibility = Visibility.Collapsed;
            }

            Date.Text = Formatter.DateAt(transaction.Date);

            if (transaction.IsRefund)
            {
                Date.Text += string.Format(" — {0}", Strings.StarsRefunded);
            }
            else if (transaction.Type is StarTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStateFailed })
            {
                Date.Text += string.Format(" — {0}", Strings.StarsFailed);
            }
            else if (transaction.Type is StarTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStatePending })
            {
                Date.Text += string.Format(" — {0}", Strings.StarsPending);
            }

            UpdateAmount(transaction.StarAmount);
        }

        private void UpdateAmount(StarAmount starAmount)
        {
            // TDLib signs the amount rather than naming a direction: negative is outgoing.
            var sent = starAmount.IsNegative();
            var amount = Formatter.SplitAmount(BigInteger.Abs(Formatter.Nanostars(starAmount)), 9, 9);

            AmountInteger.Text = (sent ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;

            if (sent || _received == null)
            {
                // Back to the colour the style gives it. Only a local value is cleared, so the
                // amount must never be given a Foreground in the markup - that is a local value
                // too, and this would take it away for good.
                Amount.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                Amount.Foreground = _received;
            }
        }

        private void UpdateMedia(IClientService clientService, PaidMedia media, Border target, ref long token)
        {
            var file = media.GetThumbnailFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                UpdateMedia(target, file);
            }
            else if (file.Local.CanBeDownloaded)
            {
                UpdateManager.Subscribe(this, clientService, file, ref token, target == Media1 ? UpdateMedia1 : UpdateMedia2, true);
                clientService.DownloadFile(file.Id, 16);
            }
        }

        private void UpdateMedia1(File file)
        {
            UpdateMedia(Media1, file);
        }

        private void UpdateMedia2(File file)
        {
            UpdateMedia(Media2, file);
        }

        private void UpdateMedia(Border target, File file)
        {
            target.Background = new ImageBrush
            {
                ImageSource = UriEx.ToBitmap(file.Local.Path),
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
        }
    }
}
