using Windows.UI.Xaml;

namespace Unigram.Common
{
    public static class ControlProperties
    {
        #region Label

        public static string GetLabel(DependencyObject obj)
        {
            return (string)obj.GetValue(LabelProperty);
        }

        public static void SetLabel(DependencyObject obj, string value)
        {
            obj.SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.RegisterAttached("Label", typeof(string), typeof(ControlProperties), new PropertyMetadata(null));

        #endregion
    }
}
