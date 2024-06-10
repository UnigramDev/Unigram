//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public class FluidGridView
    {
        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var triggers = GetTriggers(sender as ItemsControl);

            var reference = GetReference(triggers?.Owner);
            if (reference != null)
            {
                if (reference.Orientation == Orientation.Horizontal && Math.Truncate(e.PreviousSize.Width) != Math.Truncate(e.NewSize.Width))
                {
                    SetActive(triggers, reference);
                }
                else if (reference.Orientation == Orientation.Vertical && Math.Truncate(e.PreviousSize.Height) != Math.Truncate(e.NewSize.Height))
                {
                    SetActive(triggers, reference);
                }
            }
        }

        private static void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var triggers = sender as FluidGridViewTriggerCollection;
            if (triggers != null && triggers.Owner != null)
            {
                SetActive(triggers, GetReference(triggers.Owner));
            }
        }

        public static void Update(ItemsControl owner)
        {
            var triggers = GetTriggers(owner);
            if (triggers != null && triggers.Owner != null)
            {
                SetActive(triggers, GetReference(triggers.Owner));
            }
        }

        private static void SetActive(FluidGridViewTriggerCollection triggers, WrapGridReference reference)
        {
            if (triggers.Owner.ItemsPanelRoot == null || reference == null)
            {
                return;
            }

            var owner = triggers.Owner;

            //var paddingNear = reference.Orientation == Orientation.Horizontal
            //    ? owner.Padding.Left
            //    : owner.Padding.Top;

            //var paddingFar = reference.Orientation == Orientation.Horizontal
            //    ? owner.Padding.Right
            //    : owner.Padding.Bottom;

            var padding = GetPadding(owner);

            var parentLength = reference.Orientation == Orientation.Horizontal
                ? owner.ItemsPanelRoot.ActualWidth - padding.Left - padding.Right
                : owner.ItemsPanelRoot.ActualHeight - padding.Top - padding.Bottom;

            FluidGridViewTriggerBase trigger = null;

            foreach (var child in triggers)
            {
                if (child.MaybeActive(parentLength))
                {
                    trigger = child;
                }
            }

            if (trigger != null)
            {
                var maxLength = GetMaxLength(owner);
                if (parentLength > maxLength && !double.IsNaN(maxLength))
                {
                    parentLength = maxLength;
                }

                //var itemLength = trigger.GetItemLength(parentLength - paddingNear - paddingFar);
                var itemLength = trigger.GetItemLength(Math.Floor(parentLength), out int maximumRowsOrColumns);

                reference.MaximumRowsOrColumns = maximumRowsOrColumns;

                if (reference.Orientation == Orientation.Horizontal)
                {
                    reference.ItemWidth = itemLength;
                }
                else
                {
                    reference.ItemHeight = itemLength;
                }

                var orientationOnly = GetOrientationOnly(owner);
                if (!orientationOnly)
                {
                    if (reference.Orientation == Orientation.Horizontal)
                    {
                        reference.ItemHeight = itemLength;
                    }
                    else
                    {
                        reference.ItemWidth = itemLength;
                    }
                }
            }
        }

        #region OrientationOnly
        public static bool GetOrientationOnly(DependencyObject obj)
        {
            return (bool)obj.GetValue(OrientationOnlyProperty);
        }

        public static void SetOrientationOnly(DependencyObject obj, bool value)
        {
            obj.SetValue(OrientationOnlyProperty, value);
        }

        public static readonly DependencyProperty OrientationOnlyProperty =
            DependencyProperty.RegisterAttached("OrientationOnly", typeof(bool), typeof(ItemsControl), new PropertyMetadata(true));
        #endregion

        #region Triggers
        public static FluidGridViewTriggerCollection GetTriggers(DependencyObject obj)
        {
            var sender = obj as ItemsControl;
            var triggers = (FluidGridViewTriggerCollection)obj.GetValue(TriggersProperty);
            if (triggers == null)
            {
                triggers = new FluidGridViewTriggerCollection(sender);
                triggers.CollectionChanged += OnCollectionChanged;
                sender.SizeChanged += OnSizeChanged;

                obj.SetValue(TriggersProperty, triggers);
            }

            return triggers;
        }

        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.RegisterAttached("Triggers", typeof(FluidGridViewTriggerCollection), typeof(ItemsControl), new PropertyMetadata(null, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ItemsControl;
            if (sender != null && e.NewValue is FluidGridViewTriggerCollection triggers)
            {
                SetActive(triggers, GetReference(triggers.Owner));
            }
        }
        #endregion

        #region MaxLength

        public static double GetMaxLength(DependencyObject obj)
        {
            return (double)obj.GetValue(MaxLengthProperty);
        }

        public static void SetMaxLength(DependencyObject obj, double value)
        {
            obj.SetValue(MaxLengthProperty, value);
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.RegisterAttached("MaxLength", typeof(double), typeof(FluidGridViewTriggerCollection), new PropertyMetadata(double.NaN));

        #endregion

        #region Padding

        public static Thickness GetPadding(DependencyObject obj)
        {
            return (Thickness)obj.GetValue(PaddingProperty);
        }

        public static void SetPadding(DependencyObject obj, Thickness value)
        {
            obj.SetValue(PaddingProperty, value);
        }

        public static readonly DependencyProperty PaddingProperty =
            DependencyProperty.RegisterAttached("Padding", typeof(Thickness), typeof(FluidGridView), new PropertyMetadata(default(Thickness)));

        #endregion


        #region Reference
        private static WrapGridReference GetReference(DependencyObject obj)
        {
            var sender = obj as ItemsControl;
            if (sender?.ItemsPanelRoot == null)
            {
                return null;
            }

            var value = (WrapGridReference)obj.GetValue(ReferenceProperty);
            if (value == null)
            {
                value = new WrapGridReference(sender.ItemsPanelRoot);
                obj.SetValue(ReferenceProperty, value);
            }
            else
            {
                value.Owner = sender.ItemsPanelRoot;
            }

            return value;
        }

        private static readonly DependencyProperty ReferenceProperty =
            DependencyProperty.RegisterAttached("Reference", typeof(WrapGridReference), typeof(ItemsControl), new PropertyMetadata(null));

        private class WrapGridReference
        {
            public object Owner { get; set; }

            public Orientation Orientation
            {
                get
                {
                    if (Owner is WrapGrid)
                    {
                        return (Owner as WrapGrid).Orientation;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        return (Owner as ItemsWrapGrid).Orientation;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        return (Owner as VariableSizedWrapGrid).Orientation;
                    }

                    return Orientation.Horizontal;
                }
            }

            public double ItemWidth
            {
                get
                {
                    if (Owner is WrapGrid)
                    {
                        return (Owner as WrapGrid).ItemWidth;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        return (Owner as ItemsWrapGrid).ItemWidth;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        return (Owner as VariableSizedWrapGrid).ItemWidth;
                    }

                    return double.NaN;
                }
                set
                {
                    if (Owner is WrapGrid)
                    {
                        (Owner as WrapGrid).ItemWidth = value;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        (Owner as ItemsWrapGrid).ItemWidth = value;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        (Owner as VariableSizedWrapGrid).ItemWidth = value;
                    }
                }
            }

            public double ItemHeight
            {
                get
                {
                    if (Owner is WrapGrid)
                    {
                        return (Owner as WrapGrid).ItemHeight;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        return (Owner as ItemsWrapGrid).ItemHeight;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        return (Owner as VariableSizedWrapGrid).ItemHeight;
                    }

                    return double.NaN;
                }
                set
                {
                    if (Owner is WrapGrid)
                    {
                        (Owner as WrapGrid).ItemHeight = value;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        (Owner as ItemsWrapGrid).ItemHeight = value;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        (Owner as VariableSizedWrapGrid).ItemHeight = value;
                    }
                }
            }

            public int MaximumRowsOrColumns
            {
                get
                {
                    if (Owner is WrapGrid)
                    {
                        return (Owner as WrapGrid).MaximumRowsOrColumns;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        return (Owner as ItemsWrapGrid).MaximumRowsOrColumns;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        return (Owner as VariableSizedWrapGrid).MaximumRowsOrColumns;
                    }

                    return 0;
                }
                set
                {
                    if (Owner is WrapGrid)
                    {
                        (Owner as WrapGrid).MaximumRowsOrColumns = value;
                    }
                    else if (Owner is ItemsWrapGrid)
                    {
                        (Owner as ItemsWrapGrid).MaximumRowsOrColumns = value;
                    }
                    else if (Owner is VariableSizedWrapGrid)
                    {
                        (Owner as VariableSizedWrapGrid).MaximumRowsOrColumns = value;
                    }
                }
            }

            public WrapGridReference(object owner)
            {
                Owner = owner;
            }
        }
        #endregion
    }

    public class FluidGridViewTriggerCollection : ObservableCollection<FluidGridViewTriggerBase>
    {
        public ItemsControl Owner { get; private set; }

        public FluidGridViewTriggerCollection(ItemsControl owner)
        {
            Owner = owner;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    (item as FluidGridViewTriggerBase).PropertyChanged += OnItemPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    (item as FluidGridViewTriggerBase).PropertyChanged -= OnItemPropertyChanged;
                }
            }
        }

        private void OnItemPropertyChanged(object sender, EventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged;
    }

    public abstract class FluidGridViewTriggerBase : DependencyObject
    {
        #region MinLength
        public double MinLength
        {
            get => (double)GetValue(MinLengthProperty);
            set => SetValue(MinLengthProperty, value);
        }

        public static readonly DependencyProperty MinLengthProperty =
            DependencyProperty.Register("MinLength", typeof(double), typeof(FluidGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public bool MaybeActive(double parentWidth)
        {
            return parentWidth >= MinLength;
        }

        public abstract double GetItemLength(double parentLength, out int maximumRowsOrColumns);

        #region PropertyChanged
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FluidGridViewTriggerBase;
            if (sender.PropertyChanged != null)
            {
                sender.PropertyChanged(sender, EventArgs.Empty);
            }
        }

        public virtual event EventHandler PropertyChanged;
        #endregion
    }

    public class FluidGridViewTrigger : FluidGridViewTriggerBase
    {
        #region RowsOrColumns
        public int RowsOrColumns
        {
            get => (int)GetValue(RowsOrColumnsProperty);
            set => SetValue(RowsOrColumnsProperty, value);
        }

        public static readonly DependencyProperty RowsOrColumnsProperty =
            DependencyProperty.Register("RowsOrColumns", typeof(int), typeof(FluidGridViewTrigger), new PropertyMetadata(0, OnPropertyChanged));
        #endregion

        #region MaxLength
        public double MaxLength
        {
            get => (double)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register("MaxLength", typeof(double), typeof(FluidGridViewTrigger), new PropertyMetadata(0d));
        #endregion

        #region Margin

        public double Margin
        {
            get { return (double)GetValue(MarginProperty); }
            set { SetValue(MarginProperty, value); }
        }

        public static readonly DependencyProperty MarginProperty =
            DependencyProperty.Register("Margin", typeof(double), typeof(FluidGridViewTrigger), new PropertyMetadata(0d));

        #endregion

        public override double GetItemLength(double parentLength, out int maximumRowsOrColumns)
        {
            maximumRowsOrColumns = RowsOrColumns;

            if (MaxLength > 0)
            {
                return Math.Min(MaxLength, parentLength / RowsOrColumns) - Margin;
            }

            return (parentLength / RowsOrColumns) - Margin;
        }

        #region PropertyChanged
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FluidGridViewTrigger;
            if (sender.PropertyChanged != null)
            {
                sender.PropertyChanged(sender, EventArgs.Empty);
            }
        }

        public override event EventHandler PropertyChanged;
        #endregion
    }

    public class FixedGridViewTrigger : FluidGridViewTriggerBase
    {
        #region ItemLength
        public double ItemLength
        {
            get => (double)GetValue(ItemLengthProperty);
            set => SetValue(ItemLengthProperty, value);
        }

        public static readonly DependencyProperty ItemLengthProperty =
            DependencyProperty.Register("ItemLength", typeof(double), typeof(FixedGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public override double GetItemLength(double parentLength, out int maximumRowsOrColumns)
        {
            maximumRowsOrColumns = (int)Math.Floor(parentLength / ItemLength);
            return ItemLength;
        }

        #region PropertyChanged
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FixedGridViewTrigger;
            if (sender.PropertyChanged != null)
            {
                sender.PropertyChanged(sender, EventArgs.Empty);
            }
        }

        public override event EventHandler PropertyChanged;
        #endregion
    }

    public class LengthGridViewTrigger : FluidGridViewTriggerBase
    {
        #region ItemLength
        public double ItemLength
        {
            get => (double)GetValue(ItemLengthProperty);
            set => SetValue(ItemLengthProperty, value);
        }

        public static readonly DependencyProperty ItemLengthProperty =
            DependencyProperty.Register("ItemLength", typeof(double), typeof(LengthGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public override double GetItemLength(double parentLength, out int maximumRowsOrColumns)
        {
            if (parentLength <= 400)
            {
                maximumRowsOrColumns = 3;
                return parentLength / 3d;
            }
            else if (parentLength <= 500)
            {
                maximumRowsOrColumns = 4;
                return parentLength / 4d;
            }
            else
            {
                var parent = ItemLength;
                var itemsCount = 0;

                while (parent <= parentLength)
                {
                    parent += ItemLength;
                    itemsCount += 1;
                }

                maximumRowsOrColumns = itemsCount;
                return Math.Floor(parentLength / itemsCount);
            }
        }

        #region PropertyChanged
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as LengthGridViewTrigger;
            if (sender.PropertyChanged != null)
            {
                sender.PropertyChanged(sender, EventArgs.Empty);
            }
        }

        public override event EventHandler PropertyChanged;
        #endregion
    }
}
