using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
