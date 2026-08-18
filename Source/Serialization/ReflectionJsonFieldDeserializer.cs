using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Ustas.RimAI.Communication.Relations.Serialization
{
    /// <summary>
    /// Dependencies: reflection metadata and in-file JSON token parser.
    /// Responsibility: deserialize public-field object graphs from stable JSON emitted by ReflectionJsonFieldSerializer.
    /// </summary>
    internal static class ReflectionJsonFieldDeserializer
    {
        public static bool TryDeserialize<T>(string json, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            if (!TryParseRoot(json, out object root))
            {
                return false;
            }

            object converted = ConvertValue(root, typeof(T));
            value = converted as T;
            return value != null;
        }

        private static bool TryParseRoot(string json, out object root)
        {
            try
            {
                var parser = new ReflectionJsonParser(json);
                root = parser.Parse();
                return true;
            }
            catch
            {
                root = null;
                return false;
            }
        }

        private static object ConvertValue(object raw, Type targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                return ConvertNullable(raw, nullableType);
            }

            if (raw == null)
            {
                return CreateDefaultValue(targetType);
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            if (targetType == typeof(bool))
            {
                return ConvertBoolean(raw);
            }

            if (targetType.IsEnum)
            {
                return ConvertEnum(raw, targetType);
            }

            if (IsNumericType(targetType))
            {
                return ConvertNumber(raw, targetType);
            }

            if (TryConvertList(raw, targetType, out object listValue))
            {
                return listValue;
            }

            if (raw is Dictionary<string, object> dict)
            {
                return ConvertObject(dict, targetType);
            }

            return targetType.IsAssignableFrom(raw.GetType()) ? raw : CreateDefaultValue(targetType);
        }

        private static object ConvertNullable(object raw, Type nullableType)
        {
            if (raw == null)
            {
                return null;
            }

            return ConvertValue(raw, nullableType);
        }

        private static bool ConvertBoolean(object raw)
        {
            switch (raw)
            {
                case bool boolValue:
                    return boolValue;
                case string text:
                    return string.Equals(text.Trim(), "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(text.Trim(), "1", StringComparison.OrdinalIgnoreCase);
                default:
                    return Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0L;
            }
        }

        private static object ConvertEnum(object raw, Type enumType)
        {
            if (raw is string text && !string.IsNullOrWhiteSpace(text))
            {
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long enumNumber))
                {
                    return Enum.ToObject(enumType, enumNumber);
                }

                if (Enum.IsDefined(enumType, text))
                {
                    return Enum.Parse(enumType, text, ignoreCase: true);
                }
            }

            long numeric = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            return Enum.ToObject(enumType, numeric);
        }

        private static object ConvertNumber(object raw, Type targetType)
        {
            object defaultValue = CreateDefaultValue(targetType);
            if (raw == null)
            {
                return defaultValue;
            }

            try
            {
                if (raw is string text)
                {
                    if (text.Length == 0)
                    {
                        return defaultValue;
                    }

                    if (targetType == typeof(float) || targetType == typeof(double) || targetType == typeof(decimal))
                    {
                        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
                    }

                    long integer = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return Convert.ChangeType(integer, targetType, CultureInfo.InvariantCulture);
                }

                return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool TryConvertList(object raw, Type targetType, out object converted)
        {
            converted = null;
            if (!(raw is List<object> source))
            {
                return false;
            }

            if (targetType.IsArray)
            {
                converted = ConvertArray(source, targetType.GetElementType());
                return true;
            }

            Type itemType = ResolveListItemType(targetType);
            if (itemType == null || !typeof(IList).IsAssignableFrom(targetType))
            {
                return false;
            }

            IList list = CreateListInstance(targetType, itemType);
            foreach (object item in source)
            {
                list.Add(ConvertValue(item, itemType));
            }

            converted = list;
            return true;
        }

        private static object ConvertArray(List<object> source, Type itemType)
        {
            if (itemType == null)
            {
                return Array.Empty<object>();
            }

            Array array = Array.CreateInstance(itemType, source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                array.SetValue(ConvertValue(source[i], itemType), i);
            }

            return array;
        }

        private static Type ResolveListItemType(Type targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            if (targetType.IsGenericType)
            {
                Type[] args = targetType.GetGenericArguments();
                return args.Length == 1 ? args[0] : null;
            }

            Type listInterface = targetType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            if (listInterface == null)
            {
                return null;
            }

            Type[] interfaceArgs = listInterface.GetGenericArguments();
            return interfaceArgs.Length == 1 ? interfaceArgs[0] : null;
        }

        private static IList CreateListInstance(Type targetType, Type itemType)
        {
            if (targetType.IsInterface || targetType.IsAbstract)
            {
                Type fallbackList = typeof(List<>).MakeGenericType(itemType);
                return (IList)Activator.CreateInstance(fallbackList);
            }

            try
            {
                return (IList)Activator.CreateInstance(targetType);
            }
            catch
            {
                Type fallbackList = typeof(List<>).MakeGenericType(itemType);
                return (IList)Activator.CreateInstance(fallbackList);
            }
        }

        private static object ConvertObject(Dictionary<string, object> dict, Type targetType)
        {
            object instance = CreateInstance(targetType);
            if (instance == null)
            {
                return null;
            }

            FieldInfo[] fields = targetType
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsStatic)
                .ToArray();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!TryGetValue(dict, field.Name, out object rawField))
                {
                    continue;
                }

                object fieldValue = ConvertValue(rawField, field.FieldType);
                field.SetValue(instance, fieldValue);
            }

            return instance;
        }

        private static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
        {
            if (source.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (KeyValuePair<string, object> item in source)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static object CreateDefaultValue(Type type)
        {
            return type != null && type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static object CreateInstance(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type.IsInterface || type.IsAbstract)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

    
    }
}
