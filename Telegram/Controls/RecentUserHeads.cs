//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public delegate void RecentUserHeadChangedHandler(ProfilePicture sender, MessageSender messageSender);

    public partial class RecentUserHeads : Control
    {
        private const int MaxCount = 3;

        private sealed class Head
        {
            public Border Container;
            public ProfilePicture Picture;
            public MessageSender Sender;

            // Where the visual is, or is heading. Equal to its index in _heads between updates.
            public int Slot = -1;
        }

        private readonly RecentUserCollection _items;

        // The heads on display, in order. The order on screen comes from Slot and the Z index, so
        // this is the only thing to index by: HeadsRoot.Children also holds the heads animating
        // out, in no particular order.
        private readonly List<Head> _heads = new();

        private readonly List<UIElement> _removing = new();
        private int _removalBatches;

        private bool _animated;

        // Only these have a Translation: StopAnimation throws for a property that does not exist.
        private List<UIElement> _slid;

        private Canvas HeadsRoot;
        private ContentPresenter HeaderPresenter;
        private ContentPresenter FooterPresenter;

        private int _itemSize = 32;
        private int _itemOverlap = 10;

        public RecentUserHeads()
        {
            DefaultStyleKey = typeof(RecentUserHeads);

            _items = new RecentUserCollection(this);
        }

        #region ItemSize

        public int ItemSize
        {
            get { return (int)GetValue(ItemSizeProperty); }
            set { SetValue(ItemSizeProperty, value); }
        }

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register("ItemSize", typeof(int), typeof(RecentUserHeads), new PropertyMetadata(32, OnItemSizeChanged));

        private static void OnItemSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (RecentUserHeads)d;
            sender._itemSize = (int)e.NewValue;
            sender.Rebuild();
        }

        #endregion

        #region ItemOverlap

        public int ItemOverlap
        {
            get { return (int)GetValue(ItemOverlapProperty); }
            set { SetValue(ItemOverlapProperty, value); }
        }

        public static readonly DependencyProperty ItemOverlapProperty =
            DependencyProperty.Register("ItemOverlap", typeof(int), typeof(RecentUserHeads), new PropertyMetadata(10, OnItemOverlapChanged));

        private static void OnItemOverlapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (RecentUserHeads)d;
            sender._itemOverlap = (int)e.NewValue;
            sender.Rebuild();
        }

        #endregion

        #region Header

        public object Header
        {
            get { return GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(RecentUserHeads), new PropertyMetadata(null, OnHeaderChanged));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RecentUserHeads)d).UpdateHeader();
        }

        // Realized on first use: most hosts set neither, and there are many of them in a chat.
        private void UpdateHeader()
        {
            var header = Header;
            if (HeadsRoot == null || (header == null && HeaderPresenter == null))
            {
                return;
            }

            HeaderPresenter ??= GetTemplateChild(nameof(HeaderPresenter)) as ContentPresenter;
            HeaderPresenter.Content = header;
        }

        #endregion

        #region Footer

        public object Footer
        {
            get { return GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(object), typeof(RecentUserHeads), new PropertyMetadata(null, OnFooterChanged));

        private static void OnFooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RecentUserHeads)d).UpdateFooter();
        }

        private void UpdateFooter()
        {
            var footer = Footer;
            if (HeadsRoot == null || (footer == null && FooterPresenter == null))
            {
                return;
            }

            FooterPresenter ??= GetTemplateChild(nameof(FooterPresenter)) as ContentPresenter;
            FooterPresenter.Content = footer;
        }

        #endregion

        public RecentUserCollection Items => _items;

        public event RecentUserHeadChangedHandler RecentUserHeadChanged;

        protected override void OnApplyTemplate()
        {
            HeadsRoot = GetTemplateChild(nameof(HeadsRoot)) as Canvas;
            HeaderPresenter = null;
            FooterPresenter = null;

            _slid?.Clear();

            UpdateHeader();
            UpdateFooter();
            Rebuild();
        }

        private int HeadSize => _itemSize + 4;

        private float SlotOffset(int slot)
        {
            return slot * (HeadSize - _itemOverlap);
        }

        private double HeadsWidth(int count)
        {
            return count > 0 ? (count * HeadSize) - ((count - 1) * _itemOverlap) : 0;
        }

        private void Rebuild()
        {
            if (HeadsRoot == null)
            {
                return;
            }

            HeadsRoot.Children.Clear();
            HeadsRoot.Width = 0;
            HeadsRoot.Height = HeadSize;

            _heads.Clear();
            _removing.Clear();
            _animated = false;

            Update(false);
        }

        internal void OnItemsChanged(bool animated)
        {
            if (HeadsRoot != null)
            {
                Update(animated);
            }
        }

        private void Update(bool animated)
        {
            var count = Math.Min(MaxCount, _items.Count);
            if (IsUnchanged(count))
            {
                return;
            }

            var previousWidth = HeadsWidth(_heads.Count);
            var width = HeadsWidth(count);

            if (animated)
            {
                Reconcile(count);
            }
            else
            {
                Assign(count);
            }

            HeadsRoot.Width = width;

            if (animated)
            {
                AnimateAlignment(width - previousWidth);
            }
        }

        private bool IsUnchanged(int count)
        {
            if (_heads.Count != count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!_heads[i].Sender.AreTheSame(_items[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // No animation, so whatever is on screen can be reused in place: this is what runs for every
        // recycled container, where rebuilding the heads is the cost to avoid.
        private void Assign(int count)
        {
            StopAnimations();
            ClearRemoving();

            while (_heads.Count > count)
            {
                HeadsRoot.Children.Remove(_heads[^1].Container);
                _heads.RemoveAt(_heads.Count - 1);
            }

            for (int i = 0; i < count; i++)
            {
                if (i < _heads.Count)
                {
                    if (!_heads[i].Sender.AreTheSame(_items[i]))
                    {
                        SetSender(_heads[i], _items[i]);
                    }
                }
                else
                {
                    var head = CreateHead(_items[i]);
                    head.Slot = i;

                    Canvas.SetZIndex(head.Container, -i);

                    if (i > 0)
                    {
                        ElementComposition.GetElementVisual(head.Container).Offset = new Vector3(SlotOffset(i), 0, 0);
                    }

                    _heads.Add(head);
                    HeadsRoot.Children.Add(head.Container);
                }
            }
        }

        // Follows each sender to its new position, so a head keeps its container wherever it goes.
        private void Reconcile(int count)
        {
            CompositionScopedBatch batch = null;

            for (int i = _heads.Count - 1; i >= 0; i--)
            {
                var head = _heads[i];
                if (IndexOfItem(head.Sender, count) < 0)
                {
                    batch ??= BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                    _heads.RemoveAt(i);
                    _removing.Add(head.Container);

                    AnimateRemoving(head);
                }
            }

            if (batch != null)
            {
                _removalBatches++;

                batch.Completed += OnRemovalCompleted;
                batch.End();
            }

            for (int i = 0; i < count; i++)
            {
                var index = IndexOfHead(_items[i], i);

                Head head;
                if (index == i)
                {
                    head = _heads[i];
                }
                else if (index > i)
                {
                    head = _heads[index];

                    _heads.RemoveAt(index);
                    _heads.Insert(i, head);
                }
                else
                {
                    head = CreateHead(_items[i]);

                    _heads.Insert(i, head);
                    HeadsRoot.Children.Add(head.Container);
                }

                if (head.Slot < 0)
                {
                    AnimateEntering(head, i);
                }
                else if (head.Slot != i)
                {
                    AnimateMoving(head, i);
                }

                head.Slot = i;
                Canvas.SetZIndex(head.Container, -i);
            }
        }

        private int IndexOfItem(MessageSender sender, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_items[i].AreTheSame(sender))
                {
                    return i;
                }
            }

            return -1;
        }

        private int IndexOfHead(MessageSender sender, int start)
        {
            for (int i = start; i < _heads.Count; i++)
            {
                if (_heads[i].Sender.AreTheSame(sender))
                {
                    return i;
                }
            }

            return -1;
        }

        // Only once every batch is done: one finishing early must not cut short the heads of a later
        // one, and a head that has finished is invisible until then.
        private void OnRemovalCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnRemovalCompleted;
            }

            if (--_removalBatches == 0)
            {
                ClearRemoving();
            }
        }

        private void ClearRemoving()
        {
            foreach (var element in _removing)
            {
                HeadsRoot.Children.Remove(element);
            }

            _removing.Clear();
        }

        private Head CreateHead(MessageSender sender)
        {
            var picture = new ProfilePicture();
            picture.Size = _itemSize;

            var borderBrush = new Binding();
            borderBrush.Source = this;
            borderBrush.Path = new PropertyPath(nameof(BorderBrush));

            var container = new Border();
            container.Width = HeadSize;
            container.Height = HeadSize;
            container.BorderThickness = new Thickness(2);
            container.Child = picture;
            container.SetBinding(Border.BorderBrushProperty, borderBrush);

            var head = new Head
            {
                Container = container,
                Picture = picture
            };

            SetSender(head, sender);
            return head;
        }

        private void SetSender(Head head, MessageSender sender)
        {
            head.Sender = sender;

            RecentUserHeadChanged?.Invoke(head.Picture, sender);

            // After the event: the shape comes from the source the handler just set.
            head.Container.CornerRadius = new CornerRadius(HeadSize / (head.Picture.ComputedShape == ProfilePictureShape.Superellipse ? 4d : 2d));
        }

        private void AnimateEntering(Head head, int slot)
        {
            var compositor = BootStrapper.Current.Compositor;

            var visual = ElementComposition.GetElementVisual(head.Container);
            visual.Offset = new Vector3(SlotOffset(slot), 0, 0);
            visual.CenterPoint = new Vector3(HeadSize / 2f, HeadSize / 2f, 0);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0.0f, new Vector3(0));
            scale.InsertKeyFrame(0.9f, new Vector3(1.1f, 1.1f, 1));
            scale.InsertKeyFrame(1.0f, new Vector3(1));

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0.0f, 0);
            opacity.InsertKeyFrame(1.0f, 1);

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);

            _animated = true;
        }

        // From wherever it is, as it may still be on its way in.
        private void AnimateRemoving(Head head)
        {
            var compositor = BootStrapper.Current.Compositor;

            Canvas.SetZIndex(head.Container, -(head.Slot + 1));

            var visual = ElementComposition.GetElementVisual(head.Container);
            visual.CenterPoint = new Vector3(HeadSize / 2f, HeadSize / 2f, 0);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1.0f, new Vector3(0));

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1.0f, 0);

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);

            _animated = true;
        }

        // From wherever it is, as it may still be on its way to another slot.
        private void AnimateMoving(Head head, int slot)
        {
            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(1, new Vector3(SlotOffset(slot), 0, 0));

            ElementComposition.GetElementVisual(head.Container).StartAnimation("Offset", offset);

            _animated = true;
        }

        // The new width takes effect in the next layout pass, the same frame these start in. What
        // layout moved is put back where it was and slid home: with the alignment anchoring the start
        // the footer moves, anchoring the end everything before it does, and centred both do by half.
        private void AnimateAlignment(double delta)
        {
            if (delta == 0)
            {
                return;
            }

            var anchor = HorizontalAlignment switch
            {
                HorizontalAlignment.Center => 0.5f,
                HorizontalAlignment.Right => 1f,
                _ => 0f
            };

            if (anchor > 0)
            {
                Slide(HeaderPresenter, anchor * (float)delta);
                Slide(HeadsRoot, anchor * (float)delta);
            }

            if (anchor < 1)
            {
                Slide(FooterPresenter, (anchor - 1) * (float)delta);
            }
        }

        private void Slide(UIElement element, float from)
        {
            if (element == null)
            {
                return;
            }

            _slid ??= new List<UIElement>(3);

            if (!_slid.Contains(element))
            {
                _slid.Add(element);
                ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            }

            var animation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();

            // Added to StartingValue so that a change arriving mid-slide carries on from there.
            animation.SetScalarParameter("From", from);
            animation.InsertExpressionKeyFrame(0, "this.StartingValue + From");
            animation.InsertKeyFrame(1, 0);

            ElementComposition.GetElementVisual(element).StartAnimation("Translation.X", animation);

            _animated = true;
        }

        private void StopAnimations()
        {
            if (!_animated)
            {
                return;
            }

            _animated = false;

            foreach (var head in _heads)
            {
                var visual = ElementComposition.GetElementVisual(head.Container);

                // A write does not override an animation still running on the property.
                visual.StopAnimation("Offset");
                visual.StopAnimation("Scale");
                visual.StopAnimation("Opacity");

                visual.Offset = new Vector3(SlotOffset(head.Slot), 0, 0);
                visual.Scale = Vector3.One;
                visual.Opacity = 1;
            }

            for (int i = 0; i < _slid?.Count; i++)
            {
                var visual = ElementComposition.GetElementVisual(_slid[i]);
                visual.StopAnimation("Translation.X");
                visual.Properties.InsertVector3("Translation", Vector3.Zero);
            }
        }
    }

    // ReplaceDiff needs no sequence of edits: only the heads on display can be told apart, and
    // RecentUserHeads compares those itself.
    public partial class RecentUserCollection : Collection<MessageSender>
    {
        private readonly RecentUserHeads _owner;
        private bool _suppressed;

        internal RecentUserCollection(RecentUserHeads owner)
        {
            _owner = owner;
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            Notify(false);
        }

        protected override void InsertItem(int index, MessageSender item)
        {
            base.InsertItem(index, item);
            Notify(true);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            Notify(true);
        }

        protected override void SetItem(int index, MessageSender item)
        {
            base.SetItem(index, item);
            Notify(true);
        }

        private void Notify(bool animated)
        {
            if (!_suppressed)
            {
                _owner.OnItemsChanged(animated);
            }
        }

        public void ReplaceWith(IEnumerable<MessageSender> items)
        {
            Replace(items, false);
        }

        public void ReplaceDiff(IEnumerable<MessageSender> items)
        {
            Replace(items, true);
        }

        private void Replace(IEnumerable<MessageSender> items, bool animated)
        {
            if (items == null)
            {
                return;
            }

            _suppressed = true;

            Clear();
            this.AddRange(items);

            _suppressed = false;

            Notify(animated);
        }
    }
}
