using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Views;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public partial class VersionLabel : Button
    {
        private int _advanced;

        public VersionLabel()
        {
            DefaultStyleKey = typeof(VersionLabel);

            Content = "Unigram " + GetVersion();

            Click += OnClick;
            ContextRequested += OnContextRequested;
        }

        public event RoutedEventHandler Navigate;

        private void OnClick(object sender, RoutedEventArgs e)
        {
            _advanced++;

            if (_advanced >= 10)
            {
                _advanced = 0;

                if (Navigate != null)
                {
                    Navigate.Invoke(this, e);
                }
                else
                {
                    var frame = this.GetParent<Frame>();
                    frame?.Navigate(typeof(DiagnosticsPage));
                }
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            flyout.CreateFlyoutItem(CopyVersion, Strings.Copy, Icons.DocumentCopy);

            flyout.ShowAt(element, args);
        }

        private void CopyVersion()
        {
            MessageHelper.CopyText(XamlRoot, GetVersion());
        }

        public static string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            var type = Package.Current.SignatureKind switch
            {
                PackageSignatureKind.Store => "",
                PackageSignatureKind.Enterprise => " Direct",
                _ => " Direct"
            };

            var revision = version.Revision > 0
                ? string.Format(" ({0})", version.Revision)
                : string.Empty;

            if (version.Build > 0)
            {
                return string.Format("{0}.{1}.{2}{3} {4}{5}", version.Major, version.Minor, version.Build, revision, packageId.Architecture, type);
            }

            return string.Format("{0}.{1}{2} {3}{4}", version.Major, version.Minor, revision, packageId.Architecture, type);
        }
    }
}
