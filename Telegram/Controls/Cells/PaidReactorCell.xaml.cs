using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class PaidReactorCell : Grid
    {
        public PaidReactorCell()
        {
            InitializeComponent();
        }

        public void UpdateCell(IClientService clientService, PaidReactor reactor)
        {
            if (reactor.IsAnonymous)
            {
                Photo.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, long.MinValue);
                Title.Text = Strings.StarsReactionAnonymous;
            }
            else if (clientService.TryGetChat(reactor.SenderId, out Chat chat))
            {
                Photo.SetChat(clientService, chat, 48);
                Title.Text = chat.Title;
            }
            else if (clientService.TryGetUser(reactor.SenderId, out User user))
            {
                Photo.SetUser(clientService, user, 48);
                Title.Text = user.FullName();
            }

            Badge.Text = Icons.Premium + "\u2004" + reactor.StarCount.ToString("N0");
        }
    }
}
