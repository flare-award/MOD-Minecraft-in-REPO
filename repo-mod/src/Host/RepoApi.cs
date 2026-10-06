// Reflection access to R.E.P.O.'s own classes.
//
// The plugin is built in three modes (real game install, community NuGet game
// assemblies, offline stubs), so it must never compile against a game type
// directly: everything here goes through reflection by name. If a member is
// renamed by a game update, the self-check logs it and that feature quietly
// turns itself off instead of breaking the game.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace MinecraftInRepo.Host
{
    public sealed class RepoApi
    {
        private const BindingFlags AnyMember =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private readonly Action<string> log;
        private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly Dictionary<string, MemberInfo> memberCache = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
        private readonly List<string> missing = new List<string>();
        private bool loggedMissing;

        public RepoApi(Action<string> log)
        {
            this.log = log ?? delegate { };
        }

        /// <summary>Every member we looked for and did not find - logged once, then silent.</summary>
        public IList<string> Missing => missing;

        // ------------------------------------------------------------- lookups

        public Type FindType(string name)
        {
            Type cached;
            if (typeCache.TryGetValue(name, out cached))
            {
                return cached;
            }

            Type found = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidate;
                try
                {
                    candidate = assembly.GetType(name, false, false);
                }
                catch (Exception)
                {
                    continue;
                }
                if (candidate != null)
                {
                    found = candidate;
                    break;
                }
            }

            typeCache[name] = found;
            if (found == null)
            {
                NoteMissing("type " + name);
            }
            return found;
        }

        private FieldInfo FindField(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }
            string key = type.FullName + "." + name;
            MemberInfo cached;
            if (memberCache.TryGetValue(key, out cached))
            {
                return cached as FieldInfo;
            }

            FieldInfo field = null;
            for (Type walk = type; walk != null && field == null; walk = walk.BaseType)
            {
                field = walk.GetField(name, AnyMember);
            }

            memberCache[key] = field;
            if (field == null)
            {
                NoteMissing("field " + type.Name + "." + name);
            }
            return field;
        }

        private MethodInfo FindMethod(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }
            string key = type.FullName + "::" + name;
            MemberInfo cached;
            if (memberCache.TryGetValue(key, out cached))
            {
                return cached as MethodInfo;
            }

            MethodInfo method = null;
            for (Type walk = type; walk != null && method == null; walk = walk.BaseType)
            {
                method = walk.GetMethod(name, AnyMember);
            }

            memberCache[key] = method;
            if (method == null)
            {
                NoteMissing("method " + type.Name + "." + name);
            }
            return method;
        }

        private void NoteMissing(string what)
        {
            if (!loggedMissing && missing.Count < 64)
            {
                missing.Add(what);
            }
        }

        /// <summary>Print the missing members once and stop collecting them.</summary>
        public void ReportMissing()
        {
            if (loggedMissing)
            {
                return;
            }
            loggedMissing = true;
            if (missing.Count == 0)
            {
                log("[MinecraftInRepo] host API check: every member found.");
                return;
            }
            log("[MinecraftInRepo] host API check: " + missing.Count + " member(s) not found, " +
                "the features that need them are off: " + string.Join(", ", missing.ToArray()));
        }

        // -------------------------------------------------------------- values

        public object StaticValue(string typeName, string field)
        {
            FieldInfo info = FindField(FindType(typeName), field);
            if (info == null || !info.IsStatic)
            {
                return null;
            }
            try
            {
                return info.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object InstanceValue(object instance, string field)
        {
            if (instance == null)
            {
                return null;
            }
            FieldInfo info = FindField(instance.GetType(), field);
            if (info == null)
            {
                return null;
            }
            try
            {
                return info.GetValue(instance);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public T InstanceValue<T>(object instance, string field, T fallback)
        {
            object value = InstanceValue(instance, field);
            return value is T ? (T)value : fallback;
        }

        public bool SetInstanceValue(object instance, string field, object value)
        {
            if (instance == null)
            {
                return false;
            }
            FieldInfo info = FindField(instance.GetType(), field);
            if (info == null)
            {
                return false;
            }
            try
            {
                if (value == null)
                {
                    if (info.FieldType.IsValueType)
                    {
                        return false;
                    }
                    info.SetValue(instance, null);
                    return true;
                }
                object converted = ConvertValue(value, info.FieldType);
                if (converted == null)
                {
                    return false;
                }
                info.SetValue(instance, converted);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public object Invoke(object instance, string method, params object[] args)
        {
            if (instance == null)
            {
                return null;
            }
            MethodInfo info = FindMethod(instance.GetType(), method);
            return InvokeInfo(info, instance, args);
        }

        public object InvokeStatic(string typeName, string method, params object[] args)
        {
            MethodInfo info = FindMethod(FindType(typeName), method);
            return InvokeInfo(info, null, args);
        }

        private object InvokeInfo(MethodInfo info, object instance, object[] args)
        {
            if (info == null)
            {
                return null;
            }
            try
            {
                ParameterInfo[] parameters = info.GetParameters();
                object[] call = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    object arg = i < args.Length ? args[i] : null;
                    if (arg == null)
                    {
                        call[i] = parameters[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[i].ParameterType)
                            : null;
                    }
                    else
                    {
                        call[i] = ConvertValue(arg, parameters[i].ParameterType);
                    }
                }
                return info.Invoke(instance, call);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object ConvertValue(object value, Type target)
        {
            if (value == null)
            {
                return null;
            }
            if (target.IsInstanceOfType(value))
            {
                return value;
            }
            try
            {
                if (target.IsEnum)
                {
                    return Enum.ToObject(target, value);
                }
                return Convert.ChangeType(value, target);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
