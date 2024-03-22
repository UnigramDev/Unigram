//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class BoostCell : Grid
    {
        public BoostCell()
        {
            InitializeComponent();
        }

        public void UpdateChatBoost(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var boost = args.Item as ChatBoost;
            if (boost == null)
            {
                return;
            }

            var user = clientService.GetUser(boost.UserId());

            if (args.Phase == 0)
            {
                TitleLabel.Text = boost.Source switch
                {
                    ChatBoostSourcePremium => user.FullName(),
                    ChatBoostSourceGiftCode => user.FullName(),
                    ChatBoostSourceGiveaway giveaway => giveaway.IsUnclaimed
                        ? Strings.BoostingUnclaimed
                        : giveaway.UserId == 0
                        ? Strings.BoostingToBeDistributed
                        : user.FullName(),
                    _ => string.Empty
                };
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = string.Format(Strings.BoostExpireOn, Formatter.ShortDate.Format(Formatter.ToLocalTime(boost.ExpirationDate)));
            }
            else if (args.Phase == 2)
            {
                switch (boost.Source)
                {
                    case ChatBoostSourcePremium:
                    case ChatBoostSourceGiftCode:
                        Photo.SetUser(clientService, user, 36);
                        break;
                    case ChatBoostSourceGiveaway giveaway:
                        if (giveaway.IsUnclaimed)
                        {
                            Photo.Source = PlaceholderImage.GetGlyph(Icons.PersonDeleteFilled);
                        }
                        else if (giveaway.UserId == 0)
                        {
                            Photo.Source = PlaceholderImage.GetGlyph(Icons.PersonQuestionMarkFilled);
                        }
                        else
                        {
                            Photo.SetUser(clientService, user, 36);
                        }
                        break;
                }

                if (boost.Count > 1)
                {
                    BoostCount.Text = Icons.Boosters12 + boost.Count;
                    BoostCount.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    BoostCount.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }
    }
}
