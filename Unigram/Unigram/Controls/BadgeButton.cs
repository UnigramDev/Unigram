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
            if (_peer != null)
            {
                _peer.RaisePropertyChangedEvent(
                    ValuePatternIdentifiers.ValueProperty,
                    oldValue?.ToString(),
                    newValue?.ToString());
            }
        }

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

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            if (_peer == null)
            {
                _peer = new BadgeButtonAutomationPeer(this);
            }

            return _peer;
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
        private BadgeButton _owner;

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
                return _owner.Badge?.ToString();
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
