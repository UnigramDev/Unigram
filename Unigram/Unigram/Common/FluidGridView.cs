﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
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

            var parentLength = reference.Orientation == Orientation.Horizontal
                ? owner.ItemsPanelRoot.ActualWidth
                : owner.ItemsPanelRoot.ActualHeight;

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
                //var itemLength = trigger.GetItemLength(parentLength - paddingNear - paddingFar);
                var itemLength = trigger.GetItemLength(parentLength);

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

            return value;
        }

        private static void SetReference(DependencyObject obj, WrapGridReference value)
        {
            obj.SetValue(ReferenceProperty, value);
        }

        private static readonly DependencyProperty ReferenceProperty =
            DependencyProperty.RegisterAttached("Reference", typeof(WrapGridReference), typeof(ItemsControl), new PropertyMetadata(null));

        private class WrapGridReference
        {
            public object Owner { get; private set; }

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
            get { return (double)GetValue(MinLengthProperty); }
            set { SetValue(MinLengthProperty, value); }
        }

        public static readonly DependencyProperty MinLengthProperty =
            DependencyProperty.Register("MinLength", typeof(double), typeof(FluidGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public bool MaybeActive(double parentWidth)
        {
            return parentWidth >= MinLength;
        }

        public abstract double GetItemLength(double parentLength);

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
            get { return (int)GetValue(RowsOrColumnsProperty); }
            set { SetValue(RowsOrColumnsProperty, value); }
        }

        public static readonly DependencyProperty RowsOrColumnsProperty =
            DependencyProperty.Register("RowsOrColumns", typeof(int), typeof(FluidGridViewTrigger), new PropertyMetadata(0, OnPropertyChanged));
        #endregion

        #region MaxLength
        public double MaxLength
        {
            get { return (double)GetValue(MaxLengthProperty); }
            set { SetValue(MaxLengthProperty, value); }
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register("MaxLength", typeof(double), typeof(FluidGridViewTrigger), new PropertyMetadata(0d));
        #endregion

        public override double GetItemLength(double parentLength)
        {
            if (MaxLength > 0)
            {
                return Math.Min(MaxLength, parentLength / RowsOrColumns);
            }

            return parentLength / RowsOrColumns;
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
            get { return (double)GetValue(ItemLengthProperty); }
            set { SetValue(ItemLengthProperty, value); }
        }

        public static readonly DependencyProperty ItemLengthProperty =
            DependencyProperty.Register("ItemLength", typeof(double), typeof(FixedGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public override double GetItemLength(double parentLength)
        {
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
            get { return (double)GetValue(ItemLengthProperty); }
            set { SetValue(ItemLengthProperty, value); }
        }

        public static readonly DependencyProperty ItemLengthProperty =
            DependencyProperty.Register("ItemLength", typeof(double), typeof(LengthGridViewTrigger), new PropertyMetadata(0d, OnPropertyChanged));
        #endregion

        public override double GetItemLength(double parentLength)
        {
            if (parentLength <= 400)
            {
                return parentLength / 4d;
            }
            else if (parentLength <= 500)
            {
                return parentLength / 5d;
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

                return parentLength / itemsCount;
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
