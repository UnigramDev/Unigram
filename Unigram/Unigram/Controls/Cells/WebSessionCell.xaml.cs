using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Cells
{
    public sealed partial class WebSessionCell : Grid
    {
        public WebSessionCell()
        {
            this.InitializeComponent();
        }

        public void UpdateConnectedWebsite(IProtoService protoService, ConnectedWebsite session)
        {
            var bot = protoService.GetUser(session.BotUserId);
            if (bot == null)
            {
                return;
            }

            Photo.Source = PlaceholderHelper.GetUser(protoService, bot, 18, 18);

            Domain.Text = session.DomainName;
            Title.Text = string.Format("{0}, {1}, {2}", bot.FirstName, session.Browser, session.Platform);
            Subtitle.Text = string.Format("{0} — {1}", session.Ip, session.Location);

            LastActiveDate.Text = BindConvert.Current.DateExtended(session.LastActiveDate);
        }
    }
}
