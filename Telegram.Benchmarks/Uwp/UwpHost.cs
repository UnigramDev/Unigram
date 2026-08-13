//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Benchmarks
{
    /// <summary>
    /// The UWP host. No XAML files on purpose - the whole app is one TextBlock, and skipping the
    /// XAML compiler keeps this a benchmark rather than a project.
    ///
    /// Runs the same <see cref="Suite"/> as the desktop console, so the two runtimes' numbers line
    /// up row for row.
    /// </summary>
    public sealed partial class BenchmarkApp : Application
    {
        private TextBlock? _output;

        public static void Main(string[] args)
        {
            Start(_ => new BenchmarkApp());
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            _output = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                IsTextSelectionEnabled = true,
                Text = "running..."
            };

            Window.Current.Content = new ScrollViewer
            {
                Content = new Border
                {
                    Padding = new Thickness(16),
                    Child = _output
                }
            };

            Window.Current.Activate();

            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            var dispatcher = Window.Current.Dispatcher;

            // Off the UI thread: the harness blocks for a second or two per row, and a UWP app that
            // stops pumping gets killed.
            var report = await Task.Run(() =>
            {
                var harness = new Harness();
                var failures = new System.Text.StringBuilder();

                // Validated here as well as on the desktop, because this host resolves System.Text
                // .Json's netstandard2.0 asset - agreeing with the net10.0 reader proves nothing
                // about the one the app actually parses against.
                var valid = Validation.Run(line => failures.AppendLine(line));

                try
                {
                    Suite.Run(harness, includeRoundTrips: true);
                }
                catch (Exception ex)
                {
                    return harness.Report() + Environment.NewLine + ex;
                }

                return Describe()
                    + (valid ? "validation ok" : "validation FAILED" + Environment.NewLine + failures)
                    + Environment.NewLine
                    + harness.Report();
            });

            // Also to disk: there is no console here, and the results need to leave the sandbox.
            // %LOCALAPPDATA%\Packages\Telegram.Benchmarks_*\LocalState\report.txt
            //
            // System.IO rather than StorageFile: an app may always touch its own LocalState, and
            // the WinRT async awaiters are not in the same place across CsWinRT 2.x and 3.0.
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "report.txt"),
                report);

            _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => _output!.Text = report);
        }

        private static string Describe()
        {
            return $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, " +
                   $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}" +
                   Environment.NewLine;
        }
    }
}
