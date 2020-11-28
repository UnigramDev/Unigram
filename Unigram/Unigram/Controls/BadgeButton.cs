using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class BadgeButton : GlyphButton
    {
        private BadgeButtonAutomationPeer _peer;

        public BadgeButton()
        {
            DefaultStyleKey = typeof(BadgeButton);
        }

        #region Badge

        public object Badge
        {
            get { return (object)GetValue(BadgeProperty); }
            set { SetValue(BadgeProperty, value); }
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register("Badge", typeof(object), typeof(BadgeButton), new PropertyMetadata(null, OnBadgeChanged));

        private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BadgeButton)d).OnBadgeChanged(e.NewValue, e.OldValue);
        }

        private void OnBadgeChanged(object newValue, object oldValue)
        {
            if (_peer != null && (newValue is string || newValue is null))
            {
                var newText = newValue?.ToString() ?? string.Empty;
                var oldText = oldValue?.ToString() ?? string.Empty;

                _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldText, newText);
            }
        }

        #endregion

        #region BadgeTemplate

        public DataTemplate BadgeTemplate
        {
            get { return (DataTemplate)GetValue(BadgeTemplateProperty); }
            set { SetValue(BadgeTemplateProperty, value); }
        }

        public static readonly DependencyProperty BadgeTemplateProperty =
            DependencyProperty.Register("BadgeTemplate", typeof(DataTemplate), typeof(BadgeButton), new PropertyMetadata(null));

        #endregion

        #region BadgeVisibility

        public Visibility BadgeVisibility
        {
            get { return (Visibility)GetValue(BadgeVisibilityProperty); }
            set { SetValue(BadgeVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(BadgeButton), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region BadgeLabel

        public string BadgeLabel
        {
            get { return (string)GetValue(BadgeLabelProperty); }
            set { SetValue(BadgeLabelProperty, value); }
        }

        public static readonly DependencyProperty BadgeLabelProperty =
            DependencyProperty.Register("BadgeLabel", typeof(string), typeof(BadgeButton), new PropertyMetadata(null, OnBadgeChanged));

        #endregion

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return _peer ??= new BadgeButtonAutomationPeer(this);
        }
    }

    public class BadgeButtonWithImage : BadgeButton
    {


        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(BadgeButtonWithImage), new PropertyMetadata(null));


    }

    public class BadgeButtonAutomationPeer : ButtonAutomationPeer, IValueProvider
    {
        private readonly BadgeButton _owner;

        public BadgeButtonAutomationPeer(BadgeButton owner) : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Value)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override IList<AutomationPeer> GetChildrenCore()
        {
            return null;
        }

        public string Value
        {
            get
            {
                if (_owner.Badge is string badge)
                {
                    return badge;
                }

                return _owner.BadgeLabel ?? string.Empty;
            }
        }

        public void SetValue(string value)
        {
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
    }
}
