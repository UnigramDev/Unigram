//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class PaidReactorCell : Grid
    {
        public PaidReactorCell()
        {
            InitializeComponent();
        }

        public void UpdateCell(IClientService clientService, PaidReactor reactor, int position, bool groupCall)
        {
            if (groupCall && clientService.TryGetGroupCallMessageLevel(reactor.StarCount, out GroupCallMessageLevel level))
            {
                Badge.Background = new SolidColorBrush(level.SecondColor.ToColor());
                Crown.Foreground = new SolidColorBrush(level.SecondColor.ToColor());

                Crown.Text = position switch
                {
                    1 => "\uEAEB",
                    2 => "\uEAEC",
                    3 => "\uEAED",
                    _ => string.Empty
                };

                Crown.Visibility = Visibility.Visible;
                CrownBackground.Visibility = Visibility.Visible;
            }

            if (reactor.IsAnonymous)
            {
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.AuthorHiddenFilled, long.MinValue);
                Title.Text = Strings.StarsReactionAnonymous;
            }
            else if (clientService.TryGetChat(reactor.SenderId, out Chat chat))
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Title.Text = chat.Title;
            }
            else if (clientService.TryGetUser(reactor.SenderId, out User user))
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Title.Text = user.FullName();
            }

            Badge.Text = Icons.Premium + "\u2004" + reactor.StarCount.ToString("N0");
        }
    }
}
