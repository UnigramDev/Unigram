using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class SettingsDownloadView : BottomSheet
    {
        public SettingsDownloadView()
        {
            this.InitializeComponent();
        }

        private static SettingsDownloadView _current;
        public static SettingsDownloadView Current
        {
            get
            {
                if (_current == null)
                    _current = new SettingsDownloadView();

                return _current;
            }
        }

        public AutoDownloadType SelectedItems { get; private set; }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(AutoDownloadType flags)
        {
            CheckPhoto.IsChecked = flags.HasFlag(AutoDownloadType.Photo);
            CheckAudio.IsChecked = flags.HasFlag(AutoDownloadType.Audio);
            CheckRound.IsChecked = flags.HasFlag(AutoDownloadType.Round);
            CheckVideo.IsChecked = flags.HasFlag(AutoDownloadType.Video);
            CheckDocument.IsChecked = flags.HasFlag(AutoDownloadType.Document);
            CheckMusic.IsChecked = flags.HasFlag(AutoDownloadType.Music);
            CheckGIF.IsChecked = flags.HasFlag(AutoDownloadType.GIF);

            return ShowAsync();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var flags = (AutoDownloadType)0;
            if (CheckPhoto.IsChecked == true)
            {
                flags |= AutoDownloadType.Photo;
            }
            if (CheckAudio.IsChecked == true)
            {
                flags |= AutoDownloadType.Audio;
            }
            if (CheckRound.IsChecked == true)
            {
                flags |= AutoDownloadType.Round;
            }
            if (CheckVideo.IsChecked == true)
            {
                flags |= AutoDownloadType.Video;
            }
            if (CheckDocument.IsChecked == true)
            {
                flags |= AutoDownloadType.Document;
            }
            if (CheckMusic.IsChecked == true)
            {
                flags |= AutoDownloadType.Music;
            }
            if (CheckGIF.IsChecked == true)
            {
                flags |= AutoDownloadType.GIF;
            }

            SelectedItems = flags;
            Hide(ContentDialogBaseResult.OK);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
