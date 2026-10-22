using System;
using System.Reflection;

namespace MapGenerator
{
    internal static class WorkshopBridge
    {
        public static bool IsLoaded => BeginMethod() != null;

        public static IDisposable Force()
        {
            var begin = BeginMethod();
            if (begin == null)
                return null;
            return begin.Invoke(null, null) as IDisposable;
        }

        private static MethodInfo BeginMethod()
        {
            var type = Type.GetType("WorkshopProcgen.ForcedReplacement, WorkshopProcgen");
            return type?.GetMethod("Begin", BindingFlags.Public | BindingFlags.Static);
        }
    }
}
