using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Telegram.Api.Aggregator {
    /// <summary>
    /// Generic extension methods used by the framework.
    /// </summary>
    public static class ExtensionMethods {
        /// <summary>
        /// Get's the name of the assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly's name.</returns>
        public static string GetAssemblyName(this Assembly assembly) {
            return assembly.FullName.Remove(assembly.FullName.IndexOf(','));
        }

        /// <summary>
        /// Gets all the attributes of a particular type.
        /// </summary>
        /// <typeparam name="T">The type of attributes to get.</typeparam>
        /// <param name="member">The member to inspect for attributes.</param>
        /// <param name="inherit">Whether or not to search for inherited attributes.</param>
        /// <returns>The list of attributes found.</returns>
        public static IEnumerable<T> GetAttributes<T>(this MemberInfo member, bool inherit) {
#if WIN_RT
            return member.GetCustomAttributes(inherit).OfType<T>();
#else
            return Attribute.GetCustomAttributes(member, inherit).OfType<T>();
#endif
        }

        /// <summary>
        /// Applies the action to each element in the list.
        /// </summary>
        /// <typeparam name="T">The enumerable item's type.</typeparam>
        /// <param name="enumerable">The elements to enumerate.</param>
        /// <param name="action">The action to apply to each item in the list.</param>
        public static void Apply<T>(this IEnumerable<T> enumerable, Action<T> action) {
            foreach(var item in enumerable) {
                action(item);
            }
        }

        /// <summary>
        /// Converts an expression into a <see cref="MemberInfo"/>.
        /// </summary>
        /// <param name="expression">The expression to convert.</param>
        /// <returns>The member info.</returns>
        public static MemberInfo GetMemberInfo(this Expression expression) {
            var lambda = (LambdaExpression)expression;

            MemberExpression memberExpression;
            if (lambda.Body is UnaryExpression) {
                var unaryExpression = (UnaryExpression)lambda.Body;
                memberExpression = (MemberExpression)unaryExpression.Operand;
            }
            else {
                memberExpression = (MemberExpression)lambda.Body;
            }

            return memberExpression.Member;
        }

#if WINDOWS_PHONE && !WP8
		//Method missing in WP7.1 Linq

		/// <summary>
		/// Merges two sequences by using the specified predicate function.
		/// </summary>
		/// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
		/// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
		/// <typeparam name="TResult">The type of the elements of the result sequence.</typeparam>
		/// <param name="first">The first sequence to merge.</param>
		/// <param name="second">The second sequence to merge.</param>
		/// <param name="resultSelector"> A function that specifies how to merge the elements from the two sequences.</param>
		/// <returns>An System.Collections.Generic.IEnumerable&lt;T&gt; that contains merged elements of two input sequences.</returns>
		public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector) {
			if (first == null) {
				throw new ArgumentNullException("first");
            }

			if (second == null) {
				throw new ArgumentNullException("second");
            }

			if (resultSelector == null) {
				throw new ArgumentNullException("resultSelector");
            }

			var enumFirst = first.GetEnumerator();
			var enumSecond = second.GetEnumerator();

			while (enumFirst.MoveNext() && enumSecond.MoveNext()) {
				yield return resultSelector(enumFirst.Current, enumSecond.Current);
			}
		}
#endif

#if WIN_RT
        /// <summary>
        /// Gets a collection of the public types defined in this assembly that are visible outside the assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>A collection of the public types defined in this assembly that are visible outside the assembly.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IEnumerable<Type> GetExportedTypes(this Assembly assembly) {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            return assembly.ExportedTypes;
        }

        /// <summary>
        /// Returns a value that indicates whether the specified type can be assigned to the current type.
        /// </summary>
        /// <param name="target">The target type</param>
        /// <param name="type">The type to check.</param>
        /// <returns>true if the specified type can be assigned to this type; otherwise, false.</returns>
        public static bool IsAssignableFrom(this Type target, Type type) {
            return target.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
        }
#endif
    }
}
