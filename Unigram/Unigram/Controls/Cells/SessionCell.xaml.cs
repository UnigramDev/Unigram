using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
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

            Title.Text = string.Format("{0}, {1} {2}", session.DeviceModel, session.Platform, session.SystemVersion);
            Subtitle.Text = string.Format("{0} — {1}", session.Ip, session.Country);
        }
    }
}
