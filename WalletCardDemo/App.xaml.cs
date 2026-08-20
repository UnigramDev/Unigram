using System;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WalletCardDemo
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// Win2D reports a bad effect property or an unreadable font as a plain
        /// HRESULT from the render thread, where nothing is attached to see it.
        /// Writing it out is the difference between a diagnosis and a guess.
        /// </summary>
        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.txt");
                File.WriteAllText(path, DateTime.Now + Environment.NewLine + e.Exception);
            }
            catch
            {
                // Nothing useful to do if even this fails.
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (Window.Current.Content is not Frame frame)
            {
                frame = new Frame();
                Window.Current.Content = frame;
            }

            if (frame.Content == null)
            {
                frame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
        }
    }
}
