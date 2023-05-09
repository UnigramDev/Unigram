using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public delegate string TextValueProvider(double newValue);
    public delegate string IconValueProvider(double newValue);

    public class MenuFlyoutSlider : MenuFlyoutItem
    {
        private MenuFlyoutSliderPresenter Presenter;

        public MenuFlyoutSlider()
        {
            DefaultStyleKey = typeof(MenuFlyoutSlider);
        }

        public event RangeBaseValueChangedEventHandler ValueChanged;

        protected override void OnApplyTemplate()
        {
            Presenter = GetTemplateChild(nameof(Presenter)) as MenuFlyoutSliderPresenter;
            Presenter.ValueChanged += OnValueChanged;

            if (FocusState != FocusState.Unfocused)
            {
                Presenter.Focus(FocusState);
            }

            UpdateTextAndIcon(Value);
            base.OnApplyTemplate();
        }

        private void OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, e);
            UpdateTextAndIcon(e.NewValue);
        }

        private void UpdateTextAndIcon(double newValue)
        {
            if (TextValueConverter != null)
            {
                Text = TextValueConverter(newValue);
            }

            if (IconValueConverter != null && Icon is FontIcon fontIcon)
            {
                fontIcon.Glyph = IconValueConverter(newValue);
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (FocusState != FocusState.Unfocused)
            {
                Presenter.Focus(FocusState);
            }
        }

        #region TextValueConverter

        public TextValueProvider TextValueConverter
        {
            get { return (TextValueProvider)GetValue(TextValueConverterProperty); }
            set { SetValue(TextValueConverterProperty, value); }
        }

        public static readonly DependencyProperty TextValueConverterProperty =
            DependencyProperty.Register("TextValueConverter", typeof(TextValueProvider), typeof(MenuFlyoutSlider), new PropertyMetadata(null));

        #endregion

        #region IconValueConverter

        public IconValueProvider IconValueConverter
        {
            get { return (IconValueProvider)GetValue(IconValueConverterProperty); }
            set { SetValue(IconValueConverterProperty, value); }
        }

        public static readonly DependencyProperty IconValueConverterProperty =
            DependencyProperty.Register("IconValueConverter", typeof(IconValueProvider), typeof(MenuFlyoutSlider), new PropertyMetadata(null));

        #endregion

        #region Value

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(0d));

        #endregion

        #region SmallChange

        public double SmallChange
        {
            get { return (double)GetValue(SmallChangeProperty); }
            set { SetValue(SmallChangeProperty, value); }
        }

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register("SmallChange", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(1d));

        #endregion

        #region Minimum

        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(0d));

        #endregion

        #region Maximum

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(100d));

        #endregion

        #region LargeChange

        public double LargeChange
        {
            get { return (double)GetValue(LargeChangeProperty); }
            set { SetValue(LargeChangeProperty, value); }
        }

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register("LargeChange", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(10d));

        #endregion

        #region StepFrequency

        public double StepFrequency
        {
            get { return (double)GetValue(StepFrequencyProperty); }
            set { SetValue(StepFrequencyProperty, value); }
        }

        public static readonly DependencyProperty StepFrequencyProperty =
            DependencyProperty.Register("StepFrequency", typeof(double), typeof(MenuFlyoutSlider), new PropertyMetadata(1d));

        #endregion
    }

    public class MenuFlyoutSliderStateManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if (templateRoot is MenuFlyoutSliderPresenter presenter)
            {
                GoToState(presenter, stateName, useTransitions);
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }

    public class MenuFlyoutSliderPresenter : Slider
    {
        private Grid HorizontalDecreaseRect;

        public MenuFlyoutSliderPresenter()
        {
            DefaultStyleKey = typeof(MenuFlyoutSliderPresenter);
        }

        protected override void OnApplyTemplate()
        {
            HorizontalDecreaseRect = GetTemplateChild(nameof(HorizontalDecreaseRect)) as Grid;
            HorizontalDecreaseRect.SizeChanged += OnSizeChanged;

            UpdateDecreaseRect(Value);
            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Logger.Debug();
            UpdateDecreaseRect(Value);
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);
            UpdateDecreaseRect(newValue);
        }

        private void UpdateDecreaseRect(double newValue)
        {
            if (HorizontalDecreaseRect != null && !double.IsNaN(HorizontalDecreaseRect.ActualWidth))
            {
                var visual = ElementCompositionPreview.GetElementVisual(HorizontalDecreaseRect);
                visual.Clip ??= visual.Compositor.CreateInsetClip();

                if (visual.Clip is InsetClip inset)
                {
                    var range = Maximum - Minimum;
                    var value = Maximum - newValue;

                    inset.RightInset = (float)(HorizontalDecreaseRect.ActualWidth * value / range);
                }
            }
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(MenuFlyoutSliderPresenter), new PropertyMetadata(string.Empty));

        #endregion

        #region Icon

        public IconElement Icon
        {
            get { return (IconElement)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(IconElement), typeof(MenuFlyoutSliderPresenter), new PropertyMetadata(null));

        #endregion

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Home or VirtualKey.End)
            {
                base.OnKeyDown(e);
            }
        }
    }
}
