using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Calls
{
    public sealed partial class MediaStateBadge : UserControl
    {
        public MediaStateBadge()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var visual = ElementComposition.GetElementVisual(this);
            visual.CenterPoint = new Vector3(ActualSize / 2, 0);
        }

        public string Text
        {
            get => TextValue.Text;
            set => TextValue.Text = value;
        }

        private bool _collapsed = true;

        public bool ShowHide(bool show, UIElement relative)
        {
            if (_collapsed != show)
            {
                return false;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(relative, true);

            var parent = ElementComposition.GetElementVisual(relative);
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

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);
            opacity.Duration = Constants.FastAnimation;

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 24 + 8, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Scale", scale);
            parent.StartAnimation("Translation", offset);

            batch.End();
            return true;
        }
    }
}
