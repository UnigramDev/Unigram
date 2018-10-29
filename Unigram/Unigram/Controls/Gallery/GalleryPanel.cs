using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Controls.Gallery
{
    public class GalleryPanel : Grid, IScrollSnapPointsInfo
    {
        public GalleryPanel()
        {
            SizeChanged += OnSizeChanged;
        }

        public float SnapPointWidth { get; set; }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            HorizontalSnapPointsChanged?.Invoke(this, new object());
        }

        //protected override Size ArrangeOverride(Size finalSize)
        //{
        //    HorizontalSnapPointsChanged?.Invoke(this, new object());

        //    return base.ArrangeOverride(finalSize);
        //}

        public float GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment alignment, out float offset)
        {
            offset = 0;

            if (orientation == Orientation.Horizontal && alignment == SnapPointsAlignment.Near)
            {
                return SnapPointWidth;
            }

            return 0;
        }

        public bool AreHorizontalSnapPointsRegular => true;

        public event EventHandler<object> HorizontalSnapPointsChanged;




        #region Unused

        public bool AreVerticalSnapPointsRegular => true;
        public event EventHandler<object> VerticalSnapPointsChanged;
        public IReadOnlyList<float> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment alignment) => throw new NotImplementedException();

        #endregion
    }
}
