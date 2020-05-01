using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Unigram.Common
{
    public static class ExpressionUtils
    {
        public static string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
        {
            if (object.Equals(propertyExpression, null))
            {
                throw new ArgumentNullException(nameof(propertyExpression));
            }

            var body = propertyExpression.Body as MemberExpression;

            if (object.Equals(body, null))
            {
                throw new ArgumentException("Invalid argument", nameof(propertyExpression));
            }

            var property = body.Member as PropertyInfo;

            if (object.Equals(property, null))
            {
                throw new ArgumentException("Argument is not a property", nameof(propertyExpression));
            }

            return property.Name;
        }
    }
}