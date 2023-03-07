//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Unigram.Common;

namespace Unigram.Controls.Chats
{
    /// <summary>
    /// This button is intended to be placed over the BubbleTextBox
    /// </summary>
    public class ChatBottomButton : Button
    {
        private ContentPresenter _label1;
        private ContentPresenter _label2;

        private Visual _visual1;
        private Visual _visual2;

        private ContentPresenter _label;
        private Visual _visual;

        public ChatBottomButton()
        {
            DefaultStyleKey = typeof(ChatBottomButton);
        }

        protected override void OnApplyTemplate()
        {
            _label1 = _label = GetTemplateChild("ContentPresenter1") as ContentPresenter;
            _label2 = GetTemplateChild("ContentPresenter2") as ContentPresenter;

            if (_label1 != null && _label2 != null)
            {
                _visual1 = _visual = ElementCompositionPreview.GetElementVisual(_label1);
                _visual2 = ElementCompositionPreview.GetElementVisual(_label2);

                _label2.Content = new object();

                _visual2.Opacity = 0;
                _visual2.Scale = new Vector3();
                _visual2.CenterPoint = new Vector3(10);

                _label1.Content = Content ?? new object();

                _visual1.Opacity = 1;
                _visual1.Scale = new Vector3(1);
                _visual1.CenterPoint = new Vector3(10);
            }

            base.OnApplyTemplate();
        }

        protected override async void OnContentChanged(object oldContent, object newContent)
        {
            if (_visual == null || _label == null)
            {
                return;
            }

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var labelShow = _visual == _visual1 ? _label2 : _label1;
            var labelHide = _visual == _visual1 ? _label1 : _label2;

            labelShow.Content = newContent ?? new object();

            await this.UpdateLayoutAsync();

            _visual1.CenterPoint = new Vector3(_label1.ActualSize / 2f, 0);
            _visual2.CenterPoint = new Vector3(_label2.ActualSize / 2f, 0);

            var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(1, new Vector3(1));
            show1.InsertKeyFrame(0, new Vector3(0));

            var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(1, 1);
            show2.InsertKeyFrame(0, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);

            _visual = visualShow;
            _label = labelShow;
        }
    }
}
