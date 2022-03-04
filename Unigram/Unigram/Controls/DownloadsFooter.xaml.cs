using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public sealed partial class DownloadsFooter : HyperlinkButton
    {
        public DownloadsFooter()
        {
            InitializeComponent();
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
        }

        public void UpdateFileDownloads(UpdateFileDownloads update)
        {
            if (update.TotalSize > 0)
            {
                ShowHide(true);

                Icon.Progress = Math.Max((double)update.DownloadedSize / update.TotalSize, 0.05);
                Count.Text = Locale.Declension("Files", update.TotalCount);

                if (update.DownloadedSize < update.TotalSize)
                {
                    Size.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(update.DownloadedSize, update.TotalSize), FileSizeConverter.Convert(update.TotalSize));
                }
                else
                {
                    Size.Text = FileSizeConverter.Convert(update.TotalSize);
                }
            }
            else
            {
                ShowHide(false);
            }
        }

        private bool _collapsed = true;

        public void ShowHide(bool show)
        {
            if ((show && Visibility == Visibility.Visible) || (!show && (Visibility == Visibility.Collapsed || _collapsed)))
            {
                return;
            }

            if (show)
            {
                _collapsed = false;
            }
            else
            {
                _collapsed = true;
            }

            Visibility = Visibility.Visible;

            var parent = ElementCompositionPreview.GetElementVisual(this);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, -40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("BottomInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
