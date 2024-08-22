//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace Telegram.Controls
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
                Count.Text = Locale.Declension(Strings.R.Files, update.TotalCount);

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

            var parent = ElementComposition.GetElementVisual(this);
            var visual = ElementComposition.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, -40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("BottomInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
