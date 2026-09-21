//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text;
using Telegram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    /// <summary>
    /// An amount being typed, drawn rather than edited: the whole part at the control's own size
    /// and the fraction smaller beside it.
    /// </summary>
    /// <remarks>
    /// Not a TextBox, and not a RichEditBox either. Two font sizes in one editable field needs
    /// RichEdit, which costs the input filter - <c>BeforeTextChanging</c> is a TextBox event, and
    /// rejecting bad input after the fact means putting the caret back where it was on every
    /// keystroke, paste and IME composition.
    ///
    /// What makes the trade worth it is that an amount is not really text: it is appended to and
    /// backspaced, never edited in the middle and never partially selected. Windows Calculator
    /// takes the same view - its display is a TextBlock and its digits arrive through
    /// CharacterReceived - and so do the payment apps this was modelled on, which all drive a
    /// rendered display from a keypad rather than a text field.
    ///
    /// Because it is not a text field, it has to say so itself: see
    /// <see cref="AmountTextBlockAutomationPeer"/>, which is what a screen reader reads.
    /// </remarks>
    public sealed partial class AmountTextBlock : Control
    {
        private TextBlock PlaceholderText;

        // Runs of one block, so that the two sizes share a baseline.
        private Run WholeText;
        private Run FractionText;

        private readonly StringBuilder _typed = new();

        public AmountTextBlock()
        {
            DefaultStyleKey = typeof(AmountTextBlock);

            IsTabStop = true;
            UseSystemFocusVisuals = true;
        }

        protected override void OnApplyTemplate()
        {
            PlaceholderText = GetTemplateChild(nameof(PlaceholderText)) as TextBlock;
            WholeText = GetTemplateChild(nameof(WholeText)) as Run;
            FractionText = GetTemplateChild(nameof(FractionText)) as Run;

            UpdateFractionFontSize();

            base.OnApplyTemplate();

            UpdateText();
        }

        #region Properties

        /// <summary>
        /// How big the fraction is drawn, the whole part taking <see cref="Control.FontSize"/>.
        /// </summary>
        public double FractionFontSize
        {
            get => (double)GetValue(FractionFontSizeProperty);
            set => SetValue(FractionFontSizeProperty, value);
        }

        public static readonly DependencyProperty FractionFontSizeProperty =
            DependencyProperty.Register(nameof(FractionFontSize), typeof(double), typeof(AmountTextBlock), new PropertyMetadata(16d, OnFractionFontSizeChanged));

        // Pushed onto the run rather than bound from the template: TemplateBinding resolves against
        // the elements of a template, and a Run is a TextElement rather than one - the binding is
        // accepted and then does nothing at all.
        private static void OnFractionFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AmountTextBlock)d).UpdateFractionFontSize();
        }

        private void UpdateFractionFontSize()
        {
            if (FractionText != null)
            {
                FractionText.FontSize = FractionFontSize;
            }
        }

        /// <summary>
        /// What stands in for the number before anything has been typed.
        /// </summary>
        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(AmountTextBlock), new PropertyMetadata(string.Empty));

        #endregion

        #region Text

        /// <summary>
        /// What has been typed, exactly as typed - including a trailing separator, and the zeros
        /// after it that have not been followed by a digit yet.
        /// </summary>
        /// <remarks>
        /// Kept verbatim rather than round-tripped through a number, because a number cannot hold
        /// the half-written states: "1." and "1.0" both parse as one and neither may be rewritten
        /// while the next digit is still coming.
        /// </remarks>
        public string Text
        {
            get => _typed.ToString();
            set
            {
                _typed.Clear();

                if (value != null)
                {
                    _typed.Append(value);
                }

                UpdateText();
            }
        }

        /// <summary>
        /// Raised for anything that changed <see cref="Text"/>, typing included.
        /// </summary>
        public event EventHandler TextChanged;

        /// <summary>
        /// Answers whether the amount that would result from a keystroke is one this field accepts.
        /// Nothing is appended when it says no, so what is on screen is always sendable.
        /// </summary>
        public Func<string, bool> Validate { get; set; }

        private void UpdateText()
        {
            if (WholeText == null)
            {
                return;
            }

            var text = _typed.ToString();
            var index = text.IndexOf(Separator);

            WholeText.Text = index < 0 ? text : text.Substring(0, index);

            // The separator belongs to the fraction, so that it shrinks along with the digits it
            // introduces rather than sitting at full size between them.
            FractionText.Text = index < 0 ? string.Empty : text.Substring(index);

            PlaceholderText.Visibility = text.Length > 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            // What a screen reader reads, since there is no text field here for it to read by
            // itself. Raised as a property change so it is announced as it is typed.
            var peer = FrameworkElementAutomationPeer.FromElement(this) as AmountTextBlockAutomationPeer;
            peer?.RaiseValueChanged();
        }

        /// <summary>
        /// The decimal separator, which is the app's rather than the region's: the amount is
        /// written and read back in the language the rest of the number is in.
        /// </summary>
        private static char Separator => LocaleService.Current.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        #endregion

        #region Input

        /// <summary>
        /// Offers the field one character, and answers whether it took it.
        /// </summary>
        /// <remarks>
        /// Fed rather than listened for. Where a keystroke came from is the screen's business, not
        /// the field's: the popup wants them whichever of its parts has focus, and the one thing
        /// this must not do is guess.
        ///
        /// Backspace is the only way to take anything off - there is no caret, so there is nothing
        /// else a delete could mean.
        /// </remarks>
        public bool TryAppend(char character)
        {
            if (character == '\b')
            {
                if (_typed.Length == 0)
                {
                    return false;
                }

                _typed.Length--;
            }
            else if (char.IsDigit(character) || (character == Separator && _typed.ToString().IndexOf(Separator) < 0))
            {
                // One separator, in the app's own language: what is typed is read back by
                // TryParseUnits, and a field holding both would not parse.
                //
                // Into an empty field, a separator and a zero mean the same thing and write the
                // same pair. The separator needs the zero because a leading one is not something
                // the rest of this can read back; the zero needs the separator because there is no
                // amount that starts with one and does not go on to a fraction.
                var candidate = _typed.Length == 0 && (character == Separator || character == '0')
                    ? "0" + Separator
                    : _typed.ToString() + character;

                if (Validate != null && !Validate(candidate))
                {
                    return false;
                }

                _typed.Clear();
                _typed.Append(candidate);
            }
            else
            {
                return false;
            }

            UpdateText();
            TextChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            // The number is the field, so pressing it is what focuses it - there is no box to
            // click into.
            Focus(FocusState.Pointer);
        }

        #endregion

        #region Automation

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new AmountTextBlockAutomationPeer(this);
        }

        #endregion
    }

    /// <summary>
    /// Makes the control read as the editable number it is rather than as the static text it is
    /// made of.
    /// </summary>
    public partial class AmountTextBlockAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
    {
        private readonly AmountTextBlock _owner;

        private string _announced = string.Empty;

        public AmountTextBlockAutomationPeer(AmountTextBlock owner)
            : base(owner)
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

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Edit;
        }

        protected override string GetLocalizedControlTypeCore()
        {
            return AutomationProperties.GetLocalizedControlType(_owner);
        }

        public void RaiseValueChanged()
        {
            var value = _owner.Text;

            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _announced, value);
            _announced = value;
        }

        public bool IsReadOnly => false;

        public string Value => _owner.Text;

        public void SetValue(string value)
        {
            _owner.Text = value;
        }
    }
}
