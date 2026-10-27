using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MoreRankInfo.Utils
{
    internal static class ReflectionUtil
    {
        internal const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Builds a delegate of <paramref name="delegateType"/> (whose parameter types may be internal to another
        /// assembly) that forwards its boxed arguments to <paramref name="callback"/>.
        /// </summary>
        internal static Delegate CreateForwardingDelegate(Type delegateType, Action<object[]> callback)
        {
            var invoke = delegateType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType != typeof(void))
                throw new ArgumentException($"{delegateType} is not a void delegate.");
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var args = Expression.NewArrayInit(typeof(object), parameters.Select(p => (Expression)Expression.Convert(p, typeof(object))));
            var body = Expression.Invoke(Expression.Constant(callback), args);
            return Expression.Lambda(delegateType, body, parameters).Compile();
        }

        /// <summary>Reads a property or field by name; returns null when it does not exist.</summary>
        internal static object GetMember(object target, string name)
        {
            if (target == null)
                return null;
            var type = target.GetType();
            var property = type.GetProperty(name, AnyInstance);
            if (property != null)
                return property.GetValue(target);
            var field = type.GetField(name, AnyInstance);
            if (field != null)
                return field.GetValue(target);
            // Explicit interface implementations (e.g. IWebRequest<T>.Result)
            foreach (var iface in type.GetInterfaces())
            {
                property = iface.GetProperty(name);
                if (property != null)
                    return property.GetValue(target);
            }
            return null;
        }
    }
}
