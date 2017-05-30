using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using Telegram.Api.Helpers;

namespace Telegram.Api.Services
{
    public abstract class ServiceBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Enables/Disables property change notification.
        /// 
        /// </summary>
#if WINDOWS_PHONE
        [Browsable(false)]
#endif
        public bool IsNotifying { get; set; }

        /// <summary>
        /// Occurs when a property value changes.
        /// 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged = (param0, param1) => { };

        public ServiceBase()
        {
            IsNotifying = true;
        }

        /// <summary>
        /// Raises a change notification indicating that all bindings should be refreshed.
        /// 
        /// </summary>
        public void Refresh()
        {
            RaisePropertyChanged(string.Empty);
        }

        /// <summary>
        /// Notifies subscribers of the property change.
        /// 
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        public virtual void RaisePropertyChanged(string propertyName)
        {
            if (!IsNotifying)
                return;
            Execute.BeginOnUIThread(() => OnPropertyChanged(new PropertyChangedEventArgs(propertyName)));
            //Execute.OnUIThread(() => OnPropertyChanged(new PropertyChangedEventArgs(propertyName)));
        }

        /// <summary>
        /// Notifies subscribers of the property change.
        /// 
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam><param name="property">The property expression.</param>
        public void RaisePropertyChanged<TProperty>(Expression<Func<TProperty>> property)
        {
            this.RaisePropertyChanged(GetMemberInfo(property).Name);
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
