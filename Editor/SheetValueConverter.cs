using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Untitled.ConfigDataBuilder.Base;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal sealed class ConverterInfo
    {
        public string ConverterTypeName { get; }
        public string VariableName { get; }

        public ConverterInfo(string converterTypeName, string variableName)
            => (ConverterTypeName, VariableName) = (converterTypeName, variableName);
    }

    /// <summary>
    /// Wrapper for all config type converters. <br />
    /// Inner converter can be: <br />
    ///   - IConfigValueConverter <br />
    ///   - IMultiSegConfigValueConverter <br />
    ///   - EnumConverter <br />
    ///   - NullableConverter <br />
    ///   - ArrayConverter <br />
    ///   - DictionaryConverter <br />
    /// </summary>
    internal interface ISheetValueConverter
    {
        string TypeName { get; }
        Type Type { get; }
        int SeparatorLevel { get; }
        bool TryCreateDefaultSeparators(out string separators);
        object ParseEscaped(string rawValue, string separators);
        string ReadBinaryExp(string readerVarName);
        void WriteBinary(BinaryWriter writer, object value);
        string ToStringExp(string varName);
    }

    internal interface ISheetValueConverterCollection
    {
        ISheetValueConverter GetConverter(string typeName);
        IEnumerable<ConverterInfo> EnumerateConverterInfo();
    }

    internal static class SheetValueConverter
    {
        private static string GetConverterVariableNameForType(string typeFullName)
        {
            return typeFullName.Replace(".", "_");
        }

        private sealed class NullableConverter : ISheetValueConverter
        {
            public string TypeName
                => _converter.TypeName + "?";

            public Type Type
                => typeof(Nullable<>).MakeGenericType(_converter.Type);

            public int SeparatorLevel => _converter.SeparatorLevel;

            private readonly ISheetValueConverter _converter;

            public NullableConverter(ISheetValueConverter converter)
            {
                if (converter.Type.IsClass || converter is NullableConverter) {
                    throw new NotSupportedException($"Nullable wrapper for {converter.TypeName} not supported");
                }
                _converter = converter;
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                return _converter.TryCreateDefaultSeparators(out separators);
            }

            public object ParseEscaped(string rawValue, string separators)
            {
                if (rawValue == null
                 || string.IsNullOrWhiteSpace(rawValue)
                 || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                return _converter.ParseEscaped(rawValue, separators);
            }

            public string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadBoolean() ? (" + TypeName + ")" +
                    _converter.ReadBinaryExp(readerVarName) + " : null";

            public void WriteBinary(BinaryWriter writer, object value)
            {
                if (value != null) {
                    writer.Write(true);
                    _converter.WriteBinary(writer, value);
                }
                else {
                    writer.Write(false);
                }
            }

            public string ToStringExp(string varName)
            {
                return $"({varName}.HasValue ? {_converter.ToStringExp(varName + ".Value")} : \"null\")";
            }
        }

        private abstract class AbstractIListConverter<T> : ISheetValueConverter
        {
            public abstract string TypeName { get; }

            public abstract Type Type { get; }

            public int SeparatorLevel => Converter.SeparatorLevel + 1;

            protected readonly ISheetValueConverter Converter;

            protected AbstractIListConverter(ISheetValueConverter converter)
            {
                System.Diagnostics.Debug.Assert(typeof(T) == converter.Type);
                Converter = converter;
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                var separatorLevel = SeparatorLevel;

                if (separatorLevel == 1) {
                    separators = ",";
                    return true;
                }
                if (separatorLevel == 2) {
                    if (!Converter.TryCreateDefaultSeparators(out var innerSeparators)) {
                        separators = default;
                        return false;
                    }
                    separators = ";" + innerSeparators;
                    return true;
                }

                separators = null;
                return false;
            }

            public abstract object ParseEscaped(string rawValue, string separators);

            protected T[] ParseEscapedToArray(string rawValue, string separators)
            {
                if (string.IsNullOrWhiteSpace(rawValue)) {
                    return Array.Empty<T>();
                }
                var list = new List<T>();
                var separator = separators[0];
                foreach (var seg in Helper.SplitEscapedString(rawValue, separator)) {
                    list.Add((T)Converter.ParseEscaped(seg.Trim(), separators.Substring(1)));
                }
                return list.ToArray();
            }

            public abstract string ReadBinaryExp(string readerVarName);

            public void WriteBinary(BinaryWriter writer, object value)
            {
                var list = (IReadOnlyList<T>)value;
                writer.Write(list.Count);
                foreach (var obj in list) {
                    Converter.WriteBinary(writer, obj);
                }
            }

            public string ToStringExp(string varName)
            {
                var arg = "v" + SeparatorLevel;
                return $"\"[\" + string.Join(\", \", {varName}.Select({arg} => {Converter.ToStringExp(arg)})) + \"]\"";
            }
        }

        private sealed class ArrayConverter<T> : AbstractIListConverter<T>
        {
            public override string TypeName
                => Converter.TypeName + "[]";

            public override Type Type
                => typeof(T[]);

            public override object ParseEscaped(string rawValue, string separators)
            {
                return ParseEscapedToArray(rawValue, separators);
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                var arg = "r" + SeparatorLevel;
                return $"ConfigDataManager.ReadArray({readerVarName}, {arg} => {Converter.ReadBinaryExp(arg)})";
            }

            public ArrayConverter(ISheetValueConverter converter) : base(converter)
            { }
        }

        private sealed class ListConverter<T> : AbstractIListConverter<T>
        {
            public override string TypeName
                => "System.Collections.Generic.IReadOnlyList<" + Converter.TypeName + ">";

            public override Type Type
                => typeof(IReadOnlyList<T>);

            public override object ParseEscaped(string rawValue, string separators)
            {
                return Array.AsReadOnly(ParseEscapedToArray(rawValue, separators));
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                var arg = "r" + SeparatorLevel;
                return $"ConfigDataManager.ReadList({readerVarName}, {arg} => {Converter.ReadBinaryExp(arg)})";
            }

            public ListConverter(ISheetValueConverter converter) : base(converter)
            { }
        }

        private sealed class DictionaryConverter<TKey, TValue> : ISheetValueConverter
        {
            public string TypeName
                => "System.Collections.Generic.IReadOnlyDictionary<" + _keyConverter.TypeName + ", " + _valueConverter.TypeName + ">";

            public Type Type
                => typeof(IReadOnlyDictionary<TKey, TValue>);

            public int SeparatorLevel => _keyConverter.SeparatorLevel + _valueConverter.SeparatorLevel + 2;

            private readonly ISheetValueConverter _keyConverter;

            private readonly ISheetValueConverter _valueConverter;

            public DictionaryConverter(ISheetValueConverter keyConverter, ISheetValueConverter valueConverter)
            {
                Debug.Assert(typeof(TKey) == keyConverter.Type);
                Debug.Assert(typeof(TValue) == valueConverter.Type);
                _keyConverter = keyConverter;
                _valueConverter = valueConverter;
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                if (!_keyConverter.TryCreateDefaultSeparators(out var keySeparators)
                 || !_valueConverter.TryCreateDefaultSeparators(out var valueSeparators)) {
                    separators = null;
                    return false;
                }
                var kvSeparators = keySeparators + valueSeparators;
                if (kvSeparators.Contains(':') || kvSeparators.Contains(',')) {
                    separators = null;
                    return false;
                }
                separators = ",:" + kvSeparators;
                return true;
            }

            public object ParseEscaped(string rawValue, string separators)
            {
                var elemSeparator = separators[0];
                var kvSeparator = separators[1];
                var keySeparator = separators.Substring(2, _keyConverter.SeparatorLevel);
                var valueSeparator = separators.Substring(2 + _keyConverter.SeparatorLevel, _valueConverter.SeparatorLevel);
                var result = new Dictionary<TKey, TValue>();
                if (rawValue == null) return result;
                if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return result;
                }
                var segs = Helper.SplitEscapedString(rawValue, elemSeparator);
                foreach (var seg in segs) {
                    var kvSeg = seg.Trim();
                    var subSegs = Helper.SplitEscapedString(kvSeg, kvSeparator).ToArray();
                    if (subSegs.Length != 2) {
                        throw new InvalidDataException($"Invalid key-value pair: {kvSeg}");
                    }
                    var key = (TKey)_keyConverter.ParseEscaped(subSegs[0], keySeparator);
                    var value = (TValue)_valueConverter.ParseEscaped(subSegs[1], valueSeparator);
                    if (result.ContainsKey(key)) {
                        throw new InvalidDataException($"Duplicated key: {key}");
                    }
                    result.Add(key, value);
                }
                return result;
            }

            public string ReadBinaryExp(string readerVarName)
            {
                var arg = "r" + SeparatorLevel;
                return
                    $"ConfigDataManager.ReadDictionary({readerVarName}, {arg} => {_keyConverter.ReadBinaryExp(arg)}, {arg} => {_valueConverter.ReadBinaryExp(arg)})";
            }

            public void WriteBinary(BinaryWriter writer, object value)
            {
                var dict = (IDictionary)value;
                writer.Write(dict.Count);
                foreach (var obj in dict.Keys) {
                    _keyConverter.WriteBinary(writer, obj);
                    _valueConverter.WriteBinary(writer, dict[obj]);
                }
            }

            public string ToStringExp(string varName)
            {
                var arg = "kv" + SeparatorLevel;
                return
                    $"(\"{{\" + string.Join(\", \", {varName}.Select({arg} => {_keyConverter.ToStringExp(arg + ".Key")} + \": \" + {_valueConverter.ToStringExp(arg + ".Value")})) + \"}}\")";
            }
        }

        private sealed class EnumConverter : ISheetValueConverter
        {
            public string TypeName { get; }
            public Type Type { get; }

            public int SeparatorLevel => _isFlags ? 1 : 0;

            private readonly bool _isFlags;
            private readonly bool _is64Bit;

            public EnumConverter(Type enumType)
            {
                TypeName = enumType.FullName;
                Type = enumType;
                _isFlags = Type.GetCustomAttribute<FlagsAttribute>() != null;
                var underlying = Enum.GetUnderlyingType(enumType);
                _is64Bit = underlying == typeof(long) || underlying == typeof(ulong);
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                separators = ",";
                return true;
            }

            public object ParseEscaped(string rawValue, string separators)
            {
                if (_isFlags) {
                    var result = 0L;
                    var values = Helper.SplitAndUnescapeString(rawValue, separators[0]);
                    foreach (var value in values) {
                        result |= Convert.ToInt64(Enum.Parse(Type, value));
                    }
                    return Enum.ToObject(Type, result);
                }
                return Enum.Parse(Type, rawValue);
            }

            public string ReadBinaryExp(string readerVarName)
            {
                return _is64Bit
                    ? $"({TypeName}){readerVarName}.ReadInt64()"
                    : $"({TypeName}){readerVarName}.ReadInt32()";
            }

            public void WriteBinary(BinaryWriter writer, object value)
            {
                if (_is64Bit) {
                    writer.Write(Convert.ToInt64(value));
                }
                else {
                    writer.Write(Convert.ToInt32(value));
                }
            }

            public string ToStringExp(string varName)
                => varName + ".ToString()";
        }

        private sealed class WrappedConverter<T> : ISheetValueConverter
        {
            private readonly IConfigValueConverter<T> _innerConverter;

            public string TypeName { get; }

            public Type Type { get; }

            public int SeparatorLevel => 0;

            public WrappedConverter(IConfigValueConverter<T> innerConverter)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = typeof(T).FullName;
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                separators = "";
                return true;
            }

            public object ParseEscaped(string rawValue, string separators)
            {
                return _innerConverter.Parse(rawValue != null ? Helper.UnescapeString(rawValue) : null);
            }

            public string ReadBinaryExp(string readerVarName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ReadFrom({readerVarName})";
            }

            public void WriteBinary(BinaryWriter writer, object value)
            {
                _innerConverter.WriteTo(writer, (T)value);
            }

            public string ToStringExp(string varName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ToString({varName})";
            }
        }

        private sealed class WrappedMultiSegConverter<T> : ISheetValueConverter
        {
            private readonly IMultiSegConfigValueConverter<T> _innerConverter;

            public string TypeName { get; }

            public Type Type { get; }

            public int SeparatorLevel => 1;

            private readonly char _defaultSeparator;

            public WrappedMultiSegConverter(IMultiSegConfigValueConverter<T> innerConverter, char defaultSeparator)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = typeof(T).FullName;
                _defaultSeparator = defaultSeparator;
            }

            public bool TryCreateDefaultSeparators(out string separators)
            {
                separators = new string(_defaultSeparator, 1);
                return true;
            }

            public object ParseEscaped(string rawValue, string separators)
            {
                var segs = Helper.SplitAndUnescapeString(rawValue, separators[0]).ToArray();
                return _innerConverter.Parse(segs);
            }

            public string ReadBinaryExp(string readerVarName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ReadFrom({readerVarName})";
            }

            public void WriteBinary(BinaryWriter writer, object value)
            {
                _innerConverter.WriteTo(writer, (T)value);
            }

            public string ToStringExp(string varName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ToString({varName})";
            }
        }

        private sealed class SheetValueConverterCollection : ISheetValueConverterCollection
        {
            private class BasicConverterInfo
            {
                public ISheetValueConverter Converter { get; }
                public Type Type { get; }
                public Type ConverterType { get; }
                public string[] Alias { get; }

                private BasicConverterInfo(ISheetValueConverter converter, Type type, Type converterType, string[] alias)
                    => (Converter, Type, ConverterType, Alias) = (converter, type, converterType, alias);

                public static BasicConverterInfo Create<T>(IConfigValueConverter<T> converter, params string[] alias)
                    => new BasicConverterInfo(new WrappedConverter<T>(converter), typeof(T), converter.GetType(), alias);

                public static BasicConverterInfo Create<T>(IMultiSegConfigValueConverter<T> converter, char defaultSeparator, params string[] alias)
                    => new BasicConverterInfo(new WrappedMultiSegConverter<T>(converter, defaultSeparator), typeof(T), converter.GetType(), alias);
            }

            private static readonly BasicConverterInfo[] BasicConverterInfos = {
                BasicConverterInfo.Create(new BoolConverter(), "bool"),
                BasicConverterInfo.Create(new Int16Converter(), "short"),
                BasicConverterInfo.Create(new UInt16Converter(), "ushort"),
                BasicConverterInfo.Create(new Int32Converter(), "int"),
                BasicConverterInfo.Create(new UInt32Converter(), "uint"),
                BasicConverterInfo.Create(new Int64Converter(), "long"),
                BasicConverterInfo.Create(new UInt64Converter(), "ulong"),
                BasicConverterInfo.Create(new SingleConverter(), "float"),
                BasicConverterInfo.Create(new DoubleConverter(), "double"),
                BasicConverterInfo.Create(new Vector2Converter(), ',', "vector2", "float2"),
                BasicConverterInfo.Create(new Vector3Converter(), ',', "vector3", "float3"),
                BasicConverterInfo.Create(new Vector4Converter(), ',', "vector4", "float4"),
                BasicConverterInfo.Create(new Vector2IntConverter(), ',', "vector2int", "int2"),
                BasicConverterInfo.Create(new Vector3IntConverter(), ',', "vector3int", "int3"),
                BasicConverterInfo.Create(new ColorConverter(), ',', "color"),
                BasicConverterInfo.Create(new Color32Converter(), ',', "color32"),
                BasicConverterInfo.Create(new StringConverter(), "string"),
            };

            private readonly Dictionary<string, ISheetValueConverter> _converters = new Dictionary<string, ISheetValueConverter>();

            private readonly Dictionary<Type, ConverterInfo> _converterInfoTable = new Dictionary<Type, ConverterInfo>();

            private void LoadAssembly(Assembly assembly)
            {
                foreach (var type in assembly.GetExportedTypes()) {
                    if (type.IsGenericType || type.IsAbstract || type.IsInterface) {
                        continue;
                    }
                    var shortName = type.Name;
                    var fullName = type.FullName!;
                    var converterTypeAttr = type.GetCustomAttribute<ConfigValueConverterAttribute>();
                    if (converterTypeAttr != null) {
                        var converterType = converterTypeAttr.Type;
                        if (converterType == null || converterType.IsGenericType || converterType.IsAbstract || converterType.IsInterface ||
                            converterType.IsNested) {
                            Debug.Log(
                                $"Custom converter '{converterTypeAttr.Type.FullName}' for type '{fullName}' is null/generic/abstract/interface/nested");
                            continue;
                        }
                        ISheetValueConverter converter;
                        if (typeof(IConfigValueConverter<>).MakeGenericType(type).IsAssignableFrom(converterTypeAttr.Type)) {
                            if (converterTypeAttr.Type.GetConstructor(Array.Empty<Type>()) == null) {
                                Debug.LogError(
                                    $"Custom converter '{converterTypeAttr.Type.FullName}' does not have a public parameterless constructor");
                            }
                            converter = (ISheetValueConverter)Activator.CreateInstance(
                                typeof(WrappedConverter<>).MakeGenericType(type),
                                Activator.CreateInstance(converterTypeAttr.Type));
                        }
                        else if (typeof(IMultiSegConfigValueConverter<>).MakeGenericType(type).IsAssignableFrom(converterTypeAttr.Type)) {
                            if (converterTypeAttr.Type.GetConstructor(Array.Empty<Type>()) == null) {
                                Debug.LogError(
                                    $"Custom converter '{converterTypeAttr.Type.FullName}' does not have a public parameterless constructor");
                            }
                            converter = (ISheetValueConverter)Activator.CreateInstance(
                                typeof(WrappedMultiSegConverter<>).MakeGenericType(type),
                                Activator.CreateInstance(converterTypeAttr.Type), converterTypeAttr.DefaultSeparator);
                        }
                        else {
                            Debug.LogError(
                                $"Custom converter '{converterTypeAttr.Type.FullName}' for type '{fullName}' "
                              + $"does not implement IConfigValueConverter<{shortName}> or IMultiSetConfigValueConverter<{shortName}>");
                            continue;
                        }

                        if (!_converters.ContainsKey(fullName)) {
                            _converters.Add(fullName, converter);
                        }
                        if (!_converters.ContainsKey(type.Name)) {
                            _converters.Add(type.Name, converter);
                        }
                        _converterInfoTable.Add(converter.Type,
                            new ConverterInfo(converterTypeAttr.Type.FullName, GetConverterVariableNameForType(fullName)));
                    }
                    else if (type.IsEnum) {
                        var converter = new EnumConverter(type);
                        if (!_converters.ContainsKey(fullName)) {
                            _converters.Add(fullName, converter);
                        }
                        if (!_converters.ContainsKey(type.Name)) {
                            _converters.Add(type.Name, converter);
                        }
                    }
                }
            }

            public IEnumerable<ConverterInfo> EnumerateConverterInfo()
            {
                return _converterInfoTable.Values;
            }

            public ISheetValueConverter GetConverter(string typeName)
            {
                typeName = typeName.Trim();
                if (_converters.TryGetValue(typeName, out var converter)) {
                    return converter;
                }

                ISheetValueConverter newConverter = null;
                if (typeName.EndsWith("[]", StringComparison.Ordinal)) {
                    var innerConverter = GetConverter(typeName.Substring(0, typeName.Length - 2));
                    var converterType = typeof(ArrayConverter<>).MakeGenericType(innerConverter.Type);
                    newConverter = (ISheetValueConverter)Activator.CreateInstance(converterType, innerConverter);
                }
                else if (typeName.StartsWith("[", StringComparison.Ordinal) && typeName.EndsWith("]", StringComparison.Ordinal)) {
                    var innerConverter = GetConverter(typeName.Substring(1, typeName.Length - 2));
                    var converterType = typeof(ListConverter<>).MakeGenericType(innerConverter.Type);
                    newConverter = (ISheetValueConverter)Activator.CreateInstance(converterType, innerConverter);
                }
                else if (typeName.EndsWith("?", StringComparison.Ordinal)) {
                    newConverter = new NullableConverter(GetConverter(typeName.Substring(0, typeName.Length - 1)));
                }
                else if (typeName.First() == '{' && typeName.Last() == '}') {
                    var inner = typeName.TrimStart('{').TrimEnd('}');
                    var segs = inner.Split(new[] { ':' }, StringSplitOptions.None);
                    if (segs.Length == 2) {
                        var key = segs[0];
                        var value = segs[1];
                        var keyConverter = GetConverter(key);
                        var valueConverter = GetConverter(value);
                        var converterType = typeof(DictionaryConverter<,>).MakeGenericType(keyConverter.Type, valueConverter.Type);
                        newConverter = (ISheetValueConverter)Activator.CreateInstance(converterType, keyConverter, valueConverter);
                    }
                }

                if (newConverter == null) {

                    throw new NotSupportedException($"Type '{typeName}' not supported");
                }

                _converters.Add(typeName, newConverter);
                return newConverter;
            }

            public SheetValueConverterCollection()
            {
                foreach (var converterInfo in BasicConverterInfos) {
                    _converters.Add(converterInfo.Type.Name, converterInfo.Converter);
                    _converters.Add(converterInfo.Type.FullName!, converterInfo.Converter);
                    foreach (var alias in converterInfo.Alias) {
                        _converters.Add(alias, converterInfo.Converter);
                    }
                    _converterInfoTable.Add(converterInfo.Type,
                        new ConverterInfo(converterInfo.ConverterType.FullName, GetConverterVariableNameForType(converterInfo.Type.FullName)));
                }

                var settings = ConfigDataBuilderSettings.GetOrCreateSettings();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .ToLookup(a => a.GetName().Name)
                    .ToDictionary(a => a.Key, a => a.First());
                foreach (var asmName in settings.customTypesAssemblies) {
                    if (assemblies.TryGetValue(asmName, out var asm)) {
                        LoadAssembly(asm);
                    }
                    else {
                        Debug.LogError($"Cannot find custom types assembly '{asmName}'", settings);
                    }
                }
            }
        }

        public static ISheetValueConverterCollection GetSheetValueConverters()
        {
            return new SheetValueConverterCollection();
        }
    }
}
