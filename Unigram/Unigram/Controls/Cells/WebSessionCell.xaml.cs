using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class WebSessionCell : Grid
    {
        public WebSessionCell()
        {
            InitializeComponent();
        }

        public void UpdateConnectedWebsite(IProtoService protoService, ConnectedWebsite session)
        {
            var bot = protoService.GetUser(session.BotUserId);
            if (bot == null)
            {
                return;
            }

            Photo.Source = PlaceholderHelper.GetUser(protoService, bot, 18);

            Domain.Text = session.DomainName;
            Title.Text = string.Format("{0}, {1}, {2}", bot.FirstName, session.Browser, session.Platform);
            Subtitle.Text = string.Format("{0} — {1}", session.Ip, session.Location);

            LastActiveDate.Text = BindConvert.Current.DateExtended(session.LastActiveDate);
        }
    }
}
