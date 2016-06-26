using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PivotRadioButton : RadioButton
    {
        public PivotRadioButton()
        {
            DefaultStyleKey = typeof(PivotRadioButton);

            Click += OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedValue != Index)
            {
                SelectedValue = Index;
            }
        }

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register("Glyph", typeof(string), typeof(PivotRadioButton), new PropertyMetadata("\uE10F"));

        public string Glyph
        {
            get { return GetValue(GlyphProperty) as string; }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register("Label", typeof(string), typeof(PivotRadioButton), new PropertyMetadata("Home"));

        public string Label
        {
            get { return GetValue(LabelProperty) as string; }
            set { SetValue(LabelProperty, value); }
        }



        #region SelectedValue
        public int SelectedValue
        {
            get { return (int)GetValue(SelectedValueProperty); }
            set { SetValue(SelectedValueProperty, value); }
        }

        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register("SelectedValue", typeof(int), typeof(PivotRadioButton), new PropertyMetadata(-1, OnSelectedValueChanged));

        private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PivotRadioButton)d).OnSelectedValueChanged();
        }
        #endregion

        #region Index
        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register("Index", typeof(int), typeof(PivotRadioButton), new PropertyMetadata(-1, OnSelectedValueChanged));
        #endregion

        private void OnSelectedValueChanged()
        {
            if (Index == SelectedValue)
            {
                IsChecked = true;
            }
        }
    }
}
