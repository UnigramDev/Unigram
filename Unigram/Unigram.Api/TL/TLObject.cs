using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using Telegram.Api.Services.Cache.EventArgs;
#if WINDOWS_PHONE
using System.Windows.Media.Imaging;
#elif WIN_RT
using Windows.UI.Xaml.Media.Imaging;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    [DataContract]
    public abstract class TLObject : INotifyPropertyChanged
    {
        public TLDialogBase Dialog { get; set; }

        public bool IsGlobalResult { get; set; }

        #region Flags

        public static byte[] ToBytes(TLObject obj, TLInt flags, int flag)
        {
            return obj != null && IsSet(flags, flag) ? obj.ToBytes() : new byte[] {};
        }

        public static void ToStream(Stream input, TLObject obj, TLInt flags, int flag)
        {
            if (IsSet(flags, flag))
            {
                obj.ToStream(input);
            }
        }

        public static void ToStream(Stream input, TLObject obj, TLLong customFlags, int flag)
        {
            if (IsSet(customFlags, flag))
            {
                obj.ToStream(input);
            }
        }

        protected static bool IsSet(TLLong flags, int flag)
        {
            var isSet = false;

            if (flags != null)
            {
                var intFlag = flag;
                isSet = (flags.Value & intFlag) == intFlag;
            }

            return isSet;
        }

        protected static bool IsSet(TLInt flags, int flag)
        {
            var isSet = false;

            if (flags != null)
            {
                var intFlag = flag;
                isSet = (flags.Value & intFlag) == intFlag;
            }

            return isSet;
        }

        protected static void Set(ref TLLong flags, int flag)
        {
            var intFlag = flag;

            if (flags != null)
            {
                flags.Value |= intFlag;
            }
            else
            {
                flags = new TLLong(intFlag);
            }
        }

        protected static void Set(ref TLInt flags, int flag)
        {
            var intFlag = flag;

            if (flags != null)
            {
                flags.Value |= intFlag;
            }
            else
            {
                flags = new TLInt(intFlag);
            }
        }

        protected static void Unset(ref TLInt flags, int flag)
        {
            var intFlag = flag;

            if (flags != null)
            {
                flags.Value &= ~intFlag;
            }
            else
            {
                flags = new TLInt(0);
            }
        }

        protected static void Unset(ref TLLong flags, int flag)
        {
            var intFlag = flag;

            if (flags != null)
            {
                flags.Value &= ~intFlag;
            }
            else
            {
                flags = new TLLong(0);
            }
        }

        protected static void SetUnset(ref TLInt flags, bool set, int flag)
        {
            if (set)
            {
                Set(ref flags, flag);
            }
            else
            {
                Unset(ref flags, flag);
            }
        }

        protected static void SetUnset(ref TLLong flags, bool set, int flag)
        {
            if (set)
            {
                Set(ref flags, flag);
            }
            else
            {
                Unset(ref flags, flag);
            }
        }
        #endregion

        public virtual TLObject FromBytes(byte[] bytes, ref int position)
        {
            throw new NotImplementedException();
        }

        public virtual byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public virtual TLObject FromStream(Stream input)
        {
            throw new NotImplementedException();
        }

        public virtual void ToStream(Stream output)
        {
            throw new NotImplementedException();
        }

        public static T GetObject<T>(byte[] bytes, ref int position) where T : TLObject
        {
            try
            {
                return (T)TLObjectGenerator.GetObject<T>(bytes, position).FromBytes(bytes, ref position);
            }
            catch (Exception e)
            {
                TLUtils.WriteLine(e.StackTrace, LogSeverity.Error);
            }

            return null;
        }

        public static T GetObject<T>(Stream input) where T : TLObject
        {
            //try
            //{
            return (T)TLObjectGenerator.GetObject<T>(input).FromStream(input);
            //}
            //catch (Exception e)
            //{
            //    TLUtils.WriteLine(e.StackTrace, LogSeverity.Error);
            //}

            //return null;
        }

        public static T GetNullableObject<T>(Stream input) where T : TLObject
        {
            return TLObjectExtensions.NullableFromStream<T>(input);
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(propertyName);
            return true;
        }

        protected bool SetField<T>(ref T field, T value, Expression<Func<T>> selectorExpression)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(selectorExpression);
            return true;
        }

        private WriteableBitmap _bitmap;

        public WriteableBitmap Bitmap
        {
            get { return _bitmap; }
            set
            {
                SetField(ref _bitmap, value, () => Bitmap);
            }
        }

        public void SetBitmap(WriteableBitmap bitmap)
        {
            //if (_bitmap == null)
            //{
            Bitmap = bitmap;
            //}
            //else
            //{
            //    _bitmap = bitmap;
            //}
        }

        public void ClearBitmap()
        {
            _bitmap = null;
        }

        /// <summary>
        /// Enables/Disables property change notification.
        /// 
        /// </summary>
        public bool IsNotifying { get; set; }

        /// <summary>
        /// Occurs when a property value changes.
        /// 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged = (param0, param1) => { };

        public TLObject()
        {
            IsNotifying = true;
        }

        /// <summary>
        /// Raises a change notification indicating that all bindings should be refreshed.
        /// 
        /// </summary>
        public void Refresh()
        {
            NotifyOfPropertyChange(string.Empty);
        }

        /// <summary>
        /// Notifies subscribers of the property change.
        /// 
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        public virtual void NotifyOfPropertyChange(string propertyName)
        {
            if (!IsNotifying)
                return;
            Execute.OnUIThread(() => OnPropertyChanged(new PropertyChangedEventArgs(propertyName)));
        }

        /// <summary>
        /// Notifies subscribers of the property change.
        /// 
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam><param name="property">The property expression.</param>
        public void NotifyOfPropertyChange<TProperty>(Expression<Func<TProperty>> property)
        {
            this.NotifyOfPropertyChange(GetMemberInfo(property).Name);
        }

        public static MemberInfo GetMemberInfo(Expression expression)
        {
            var lambdaExpression = (LambdaExpression)expression;
            return (!(lambdaExpression.Body is UnaryExpression) ? (MemberExpression)lambdaExpression.Body : (MemberExpression)((UnaryExpression)lambdaExpression.Body).Operand).Member;
        }

        /// <summary>
        /// Raises the <see cref="E:PropertyChanged"/> event directly.
        /// 
        /// </summary>
        /// <param name="e">The <see cref="T:System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, e);
        }

        /// <summary>
        /// Called when the object is deserialized.
        /// 
        /// </summary>
        /// <param name="c">The streaming context.</param>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            IsNotifying = true;
        }

        /// <summary>
        /// Used to indicate whether or not the IsNotifying property is serialized to Xml.
        /// 
        /// </summary>
        /// 
        /// <returns>
        /// Whether or not to serialize the IsNotifying property. The default is false.
        /// </returns>
        public virtual bool ShouldSerializeIsNotifying()
        {
            return false;
        }
    }
}
