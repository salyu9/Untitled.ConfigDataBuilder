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
    internal class ConverterInfo
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
        string Separators { get; set; }
        bool SetAutoSeparators();
        object ParseEscaped(string rawValue);
        string ReadBinaryExp(string readerVarName);
        void WriteBinary(BinaryWriter writer, object value);
        string ToStringExp(string varName);
    }

    internal interface ISheetValueConverterCollection
    {
        ISheetValueConverter CreateConverter(string typeName);
        IEnumerable<ConverterInfo> EnumerateConverterInfo();
    }

    internal static class SheetValueConverter
    {
        private static string GetConverterVariableNameForType(string typeFullName)
        {
            return typeFullName.Replace(".", "_");
        }

        private class NullableConverter : ISheetValueConverter
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

            public string Separators
            {
                get => _converter.Separators;
                set => _converter.Separators = value;
            }

            public bool SetAutoSeparators()
            {
                return _converter.SetAutoSeparators();
            }

            public object ParseEscaped(string rawValue)
            {
                if (rawValue == null
                 || string.IsNullOrWhiteSpace(rawValue)
                 || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                return _converter.ParseEscaped(rawValue);
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

        private class ArrayWrappedConverter : ISheetValueConverter
        {
            public string TypeName
                => _converter.TypeName + "[]";

            public Type Type
                => _converter.Type.MakeArrayType();

            public int SeparatorLevel => _converter.SeparatorLevel + 1;

            private readonly ISheetValueConverter _converter;

            private char _separator = ',';

            public ArrayWrappedConverter(ISheetValueConverter converter)
            {
                _converter = converter;
            }

            public string Separators
            {
                get => _separator + _converter.Separators;
                set
                {
                    if (value.Length == 0)
                    {
                        _converter.Separators = value;
                    }
                    else
                    {
                        _separator = value[0];
                        _converter.Separators = value.Substring(1);
                    }
                }
            }

            public bool SetAutoSeparators()
            {
                if (!_converter.SetAutoSeparators()) {
                    return false;
                }

                var separators = _converter.Separators;
                if (separators.Contains(',')) {
                    if (separators.Contains(';')) {
                        return false;
                    }
                    _separator = ';';
                }
                else {
                    _separator = ',';
                }
                return true;
            }

            public object ParseEscaped(string rawValue)
            {
                if (rawValue == null) {
                    return Array.CreateInstance(_converter.Type, 0);
                }
                if (string.IsNullOrWhiteSpace(rawValue)) {
                    return Array.CreateInstance(_converter.Type, 0);
                }
                var list = new List<object>();
                foreach (var seg in Helper.SplitEscapedString(rawValue, _separator)) {
                    list.Add(_converter.ParseEscaped(seg.Trim()));
                }
                var result = Array.CreateInstance(_converter.Type, list.Count);
                for (var i = 0; i < list.Count; ++i) {
                    result.SetValue(list[i], i);
                }
                return result;
            }

            public string ReadBinaryExp(string readerVarName)
            {
                var arg = "r" + SeparatorLevel;
                return $"ConfigDataManager.ReadArray({readerVarName}, {arg} => {_converter.ReadBinaryExp(arg)})";
            }

            public void WriteBinary(BinaryWriter writer, object value)
            {
                var arr = (Array)value;
                writer.Write(arr.Length);
                foreach (var obj in arr) {
                    _converter.WriteBinary(writer, obj);
                }
            }

            public string ToStringExp(string varName)
            {
                var arg = "v" + SeparatorLevel;
                return $"\"[\" + string.Join(\", \", {varName}.Select({arg} => {_converter.ToStringExp(arg)})) + \"]\"";
            }
        }

        private class DictionaryConverter : ISheetValueConverter
        {
            public string TypeName
                => "System.Collections.Generic.IReadOnlyDictionary<" + _keyConverter.TypeName + ", " + _valueConverter.TypeName + ">";

            public Type Type
                => typeof(IReadOnlyDictionary<,>).MakeGenericType(_keyConverter.Type, _valueConverter.Type);

            public int SeparatorLevel => Math.Max(_keyConverter.SeparatorLevel, _valueConverter.SeparatorLevel) + 2;

            private readonly ISheetValueConverter _keyConverter;

            private readonly ISheetValueConverter _valueConverter;

            private char _elemSeparator = ',';

            private char _kvSeparator = ':';

            public DictionaryConverter(ISheetValueConverter keyConverter, ISheetValueConverter valueConverter)
            {
                _keyConverter = keyConverter;
                _valueConverter = valueConverter;
            }

            public string Separators
            {
                get => _elemSeparator + _keyConverter.Separators + _kvSeparator + _valueConverter.Separators;
                set
                {
                    if (value.Length == 0)
                    {
                        _elemSeparator = ',';
                        _kvSeparator = ':';
                        _keyConverter.Separators = "";
                        _valueConverter.Separators = "";
                    }
                    else if (value.Length == 1)
                    {
                        _elemSeparator = value[0];
                        _kvSeparator = ':';
                        _keyConverter.Separators = "";
                        _valueConverter.Separators = "";
                    }
                    else
                    {
                        _elemSeparator = value[0];
                        _kvSeparator = value[1];
                        var remain = value.Substring(2);
                        _keyConverter.Separators = remain;
                        _valueConverter.Separators = remain;
                    }
                }
            }

            public bool SetAutoSeparators()
            {
                if (!_keyConverter.SetAutoSeparators() || !_valueConverter.SetAutoSeparators()) {
                    return false;
                }
                var separators = _keyConverter.Separators + _valueConverter.Separators;
                if (separators.Contains(':') || separators.Contains(',')) {
                    return false;
                }
                _elemSeparator = ',';
                _kvSeparator = ':';
                return true;
            }

            public object ParseEscaped(string rawValue)
            {
                var result = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(_keyConverter.Type, _valueConverter.Type));
                if (rawValue == null) return result;
                if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return result;
                }
                var segs = Helper.SplitEscapedString(rawValue, _elemSeparator);
                foreach (var seg in segs) {
                    var kvSeg = seg.Trim();
                    var subSegs = Helper.SplitEscapedString(kvSeg, _kvSeparator).ToArray();
                    if (subSegs.Length != 2) {
                        throw new InvalidDataException($"Invalid key-value pair: {kvSeg}");
                    }
                    var key = _keyConverter.ParseEscaped(subSegs[0]);
                    var value = _valueConverter.ParseEscaped(subSegs[1]);
                    if (result.Contains(key)) {
                        throw new InvalidDataException($"Duplicated key: {key}");
                    }
                    result.Add(key, value);
                }
                return result;
            }

            public string ReadBinaryExp(string readerVarName)
            {
                var arg = "r" + SeparatorLevel;
                return $"ConfigDataManager.ReadDictionary({readerVarName}, {arg} => {_keyConverter.ReadBinaryExp(arg)}, {arg} => {_valueConverter.ReadBinaryExp(arg)})";
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

            private char _separator = ',';

            public EnumConverter(Type enumType)
            {
                TypeName = enumType.FullName;
                Type = enumType;
                _isFlags = Type.GetCustomAttribute<FlagsAttribute>() != null;
                var underlying = Enum.GetUnderlyingType(enumType);
                _is64Bit = underlying == typeof(long) || underlying == typeof(ulong);
            }

            public string Separators
            {
                get => _isFlags ? _separator.ToString() : string.Empty;
                set => _separator = value.Length == 0 ? ',' : value[0];
            }

            public bool SetAutoSeparators()
            {
                _separator = ',';
                return true;
            }

            public object ParseEscaped(string rawValue)
            {
                if (_isFlags) {
                    var result = 0L;
                    var values = Helper.SplitAndUnescapeString(rawValue, _separator);
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

            public string Separators
            {
                get => string.Empty;
                set { }
            }

            public bool SetAutoSeparators()
            {
                return true;
            }

            public object ParseEscaped(string rawValue)
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

            private char _separator;

            private readonly char _defaultSeparator;

            public WrappedMultiSegConverter(IMultiSegConfigValueConverter<T> innerConverter, char defaultSeparator)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = typeof(T).FullName;
                _separator = _defaultSeparator = defaultSeparator;
            }

            public string Separators
            {
                get => _separator.ToString();
                set => _separator = value.Length == 0 ? ',' : value[0];
            }

            public bool SetAutoSeparators()
            {
                _separator = _defaultSeparator;
                return true;
            }

            public object ParseEscaped(string rawValue)
            {
                return _innerConverter.Parse(Helper.SplitAndUnescapeString(rawValue, _separator).ToArray());
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

        private class SheetValueConverterCollection : ISheetValueConverterCollection
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

            public ISheetValueConverter CreateConverter(string typeName)
            {
                typeName = typeName.Trim();
                if (typeName.EndsWith("[]", StringComparison.Ordinal)) {
                    return new ArrayWrappedConverter(CreateConverter(typeName.Substring(0, typeName.Length - 2)));
                }
                if (typeName.EndsWith("?", StringComparison.Ordinal)) {
                    return new NullableConverter(CreateConverter(typeName.Substring(0, typeName.Length - 1)));
                }
                if (typeName.First() == '{' && typeName.Last() == '}') {
                    var inner = typeName.TrimStart('{').TrimEnd('}');
                    var segs = inner.Split(new[] { ':' }, StringSplitOptions.None);
                    if (segs.Length == 2) {
                        var key = segs[0];
                        var value = segs[1];
                        return new DictionaryConverter(CreateConverter(key), CreateConverter(value));
                    }
                }
                if (_converters.TryGetValue(typeName, out var converter)) {
                    return converter;
                }

                throw new NotSupportedException($"Type '{typeName}' not supported");
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
