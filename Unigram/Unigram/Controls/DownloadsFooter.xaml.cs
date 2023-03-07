//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;

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
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
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
