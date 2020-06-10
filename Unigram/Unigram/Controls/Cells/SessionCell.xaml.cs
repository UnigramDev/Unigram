using Telegram.Td.Api;
using Unigram.Converters;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class SessionCell : Grid
    {
        public SessionCell()
        {
            InitializeComponent();
        }

        public void UpdateSession(Session session)
        {
            if (session.IsOfficialApplication)
            {
                Name.Text = string.Format("{0} {1}", session.ApplicationName, session.ApplicationVersion);
            }
            else
            {
                Name.Text = string.Format("{0} {1} (ID: {2})", session.ApplicationName, session.ApplicationVersion, session.ApiId);
            }

            if (string.IsNullOrEmpty(session.Platform))
            {
                Title.Text = string.Format("{0}, {1}", session.DeviceModel, session.SystemVersion);
            }
            else
            {
                Title.Text = string.Format("{0}, {1} {2}", session.DeviceModel, session.Platform, session.SystemVersion);
            }

            Subtitle.Text = string.Format("{0} — {1}", session.Ip, session.Country);

            LastActiveDate.Text = BindConvert.Current.DateExtended(session.LastActiveDate);
        }
    }
}
