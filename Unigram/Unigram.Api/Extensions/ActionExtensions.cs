using System;

namespace Telegram.Api.Extensions
{
    public static class ActionExtensions
    {
        public static void SafeInvoke(this Action action)
        {
            if (action != null)
            {
                action.Invoke();
            }
        }

        public static void SafeInvoke<T>(this Action<T> action, T param)
        {
            if (action != null)
            {
                action.Invoke(param);
            }
        }

        public static void SafeInvoke<T1, T2>(this Action<T1, T2> action, T1 param1, T2 param2)
        {
            if (action != null)
            {
                action.Invoke(param1, param2);
            }
        }

        public static void SafeInvoke<T1, T2, T3>(this Action<T1, T2, T3> action, T1 param1, T2 param2, T3 param3)
        {
            if (action != null)
            {
                action.Invoke(param1, param2, param3);
            }
        }

        public static void SafeInvoke<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (action != null)
            {
                action.Invoke(param1, param2, param3, param4);
            }
        }
    }
}
