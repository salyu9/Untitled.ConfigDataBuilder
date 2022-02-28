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
    internal interface ISheetValueConverterCollection
    {
        SheetValueConverter GetConverter(string typeName);
        IEnumerable<ConverterInfo> EnumerateConverterInfo();
    }

    internal class ConverterInfo
    {
        public string TypeName { get; }
        public string ConverterTypeName { get; }
        public string VariableName { get; }

        public ConverterInfo(string typeName, string converterTypeName, string variableName)
            => (TypeName, ConverterTypeName, VariableName) = (typeName, converterTypeName, variableName);
    }

    internal abstract class SheetValueConverter
    {
        private static string GetConverterVariableNameForType(string typeFullName)
        {
            return typeFullName.Replace(".", "_");
        }

        public abstract string TypeName { get; }
        public abstract Type Type { get; }
        public abstract bool HasSeparator { get; }
        public virtual string Separator { get; set; }
        public virtual bool IsScalar => true;
        public virtual bool IsCollection => false;
        public virtual bool CanBeKey => true;
        public abstract object Convert(string rawValue);
        public abstract string ReadBinaryExp(string readerVarName);
        public abstract void WriteBinary(BinaryWriter writer, object value);

        public virtual string ToStringExp(string varName)
            => varName + ".ToString()";

        private class NullableWrappedConverter : SheetValueConverter
        {
            public override string TypeName
                => _converter.TypeName + "?";

            public override Type Type
                => typeof(Nullable<>).MakeGenericType(_converter.Type);

            public override bool HasSeparator => _converter.HasSeparator;

            public override string Separator
            {
                get => _converter.Separator;
                set => _converter.Separator = value;
            }

            private readonly SheetValueConverter _converter;

            public NullableWrappedConverter(SheetValueConverter converter)
            {
                if (converter.Type.IsClass || converter is NullableWrappedConverter || converter is ArrayWrappedConverter) {
                    throw new NotSupportedException($"Nullable wrapper for {converter.TypeName} not supported");
                }
                _converter = converter;
            }

            public override object Convert(string rawValue)
            {
                if (rawValue == null
                 || string.IsNullOrWhiteSpace(rawValue)
                 || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                return _converter.Convert(rawValue);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadBoolean() ? (" + TypeName + ")" +
                    _converter.ReadBinaryExp(readerVarName) + " : null";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                if (value != null) {
                    writer.Write(true);
                    _converter.WriteBinary(writer, value);
                }
                else {
                    writer.Write(false);
                }
            }

            public override string ToStringExp(string varName)
            {
                return $"({varName}.HasValue ? {_converter.ToStringExp(varName + ".Value")} : \"null\")";
            }
        }

        private class ArrayWrappedConverter : SheetValueConverter
        {
            public override string TypeName
                => _converter.TypeName + "[]";

            public override Type Type
                => _converter.Type.MakeArrayType();

            public override bool HasSeparator => true;

            public override bool IsScalar => false;
            public override bool IsCollection => true;
            public override bool CanBeKey => false;

            private readonly SheetValueConverter _converter;

            public ArrayWrappedConverter(SheetValueConverter converter)
            {
                if (!converter.IsScalar) {
                    throw new NotSupportedException("Array of non-scalar type not supported.");
                }
                _converter = converter;
            }

            public override object Convert(string rawValue)
            {
                if (rawValue == null) return Array.CreateInstance(_converter.Type, 0);
                if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return Array.CreateInstance(_converter.Type, 0);
                }
                var segs = rawValue.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                var list = new List<object>();
                foreach (var seg in segs) {
                    list.Add(_converter.Convert(seg.Trim()));
                }
                var result = Array.CreateInstance(_converter.Type, list.Count);
                for (var i = 0; i < list.Count; ++i) {
                    result.SetValue(list[i], i);
                }
                return result;
            }

            public override string ReadBinaryExp(string readerVarName) =>
                $"ConfigDataManager.ReadArray({readerVarName}, r => {_converter.ReadBinaryExp("r")})";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var arr = (Array)value;
                writer.Write(arr.Length);
                foreach (var obj in arr) {
                    _converter.WriteBinary(writer, obj);
                }
            }

            public override string ToStringExp(string varName)
            {
                return $"\"[\" + string.Join(\", \", {varName}.Select(v => {_converter.ToStringExp("v")})) + \"]\"";
            }
        }

        private class DictionaryConverter : SheetValueConverter
        {
            public override string TypeName
                => "System.Collections.Generic.IReadOnlyDictionary<" + _keyConverter.TypeName + ", " + _valueConverter.TypeName + ">";

            public override Type Type
                => typeof(IReadOnlyDictionary<,>).MakeGenericType(_keyConverter.Type, _valueConverter.Type);

            public override bool HasSeparator => true;
            public override bool IsScalar => false;
            public override bool IsCollection => true;
            public override bool CanBeKey => false;

            private readonly SheetValueConverter _keyConverter;

            private readonly SheetValueConverter _valueConverter;

            public DictionaryConverter(SheetValueConverter keyConverter, SheetValueConverter valueConverter)
            {
                if (!keyConverter.IsScalar) {
                    throw new NotSupportedException("Dictionary with non-scalar key type not supported.");
                }
                if (!valueConverter.IsScalar) {
                    throw new NotSupportedException("Dictionary with non-scalar value type not supported.");
                }
                _keyConverter = keyConverter;
                _valueConverter = valueConverter;
            }

            public override object Convert(string rawValue)
            {
                var elemSplitter = new[] { Separator ?? "," };
                var kvSplitter = new[] { ':' };
                var result = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(_keyConverter.Type, _valueConverter.Type));
                if (rawValue == null) return result;
                if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                    return result;
                }
                var segs = rawValue.Split(elemSplitter, StringSplitOptions.None);
                for (var i = 0; i < segs.Length; ++i) {
                    segs[i] = segs[i].Trim();
                    var subSegs = segs[i].Split(kvSplitter, StringSplitOptions.None);
                    if (subSegs.Length != 2) {
                        throw new InvalidDataException($"Invalid key-value pair: {segs[i]}");
                    }
                    var key = _keyConverter.Convert(subSegs[0]);
                    var value = _valueConverter.Convert(subSegs[1]);
                    if (result.Contains(key)) {
                        throw new InvalidDataException($"Duplicated key: {key}");
                    }
                    result.Add(key, value);
                }
                return result;
            }

            public override string ReadBinaryExp(string readerVarName) =>
                $"ConfigDataManager.ReadDictionary({readerVarName}, r => {_keyConverter.ReadBinaryExp("r")}, r => {_valueConverter.ReadBinaryExp("r")})";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var dict = (IDictionary)value;
                writer.Write(dict.Count);
                foreach (var obj in dict.Keys) {
                    _keyConverter.WriteBinary(writer, obj);
                    _valueConverter.WriteBinary(writer, dict[obj]);
                }
            }

            public override string ToStringExp(string varName)
            {
                return $"(\"{{\" + string.Join(\", \", {varName}.Select(kv => {_keyConverter.ToStringExp("kv.Key")} + \": \" + {_valueConverter.ToStringExp("kv.Value")})) + \"}}\")";
            }
        }

        private sealed class EnumConverter : SheetValueConverter
        {
            public override string TypeName { get; }
            public override Type Type { get; }
            public override bool HasSeparator => true;
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

            public override object Convert(string rawValue)
            {
                if (_isFlags) {
                    var values = rawValue.Split(new[] { Separator ?? "|" }, StringSplitOptions.None);
                    if (values.Length == 0) {
                        throw new ArgumentException($"Empty value for {TypeName}");
                    }
                    var result = 0L;
                    foreach (var value in values) {
                        result |= System.Convert.ToInt64(Enum.Parse(Type, value));
                    }
                    return Enum.ToObject(Type, result);
                }
                return Enum.Parse(Type, rawValue);
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                return _is64Bit
                    ? $"({TypeName}){readerVarName}.ReadInt64()"
                    : $"({TypeName}){readerVarName}.ReadInt32()";
            }

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                if (_is64Bit) {
                    writer.Write(System.Convert.ToInt64(value));
                }
                else {
                    writer.Write(System.Convert.ToInt32(value));
                }
            }
        }

        private sealed class WrappedConverter<T> : SheetValueConverter
        {
            private readonly IConfigValueConverter<T> _innerConverter;

            public override string TypeName { get; }

            public override Type Type { get; }

            public override bool HasSeparator => false;

            public override bool IsScalar => _innerConverter.IsScalar;

            public WrappedConverter(IConfigValueConverter<T> innerConverter)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = typeof(T).FullName;
            }

            public override object Convert(string rawValue)
            {
                return _innerConverter.Parse(rawValue);
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ReadFrom({readerVarName})";
            }

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                _innerConverter.WriteTo(writer, (T)value);
            }

            public override string ToStringExp(string varName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ToString({varName})";
            }
        }
        
        private sealed class WrappedMultiSegConverter<T> : SheetValueConverter
        {
            private readonly IMultiSegConfigValueConverter<T> _innerConverter;

            public override string TypeName { get; }

            public override Type Type { get; }

            public override bool HasSeparator => true;

            public override bool IsScalar => _innerConverter.IsScalar;

            public WrappedMultiSegConverter(IMultiSegConfigValueConverter<T> innerConverter)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = typeof(T).FullName;
            }

            public override object Convert(string rawValue)
            {
                return _innerConverter.Parse(rawValue.Split(new[] { Separator ?? "," }, StringSplitOptions.None));
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ReadFrom({readerVarName})";
            }

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                _innerConverter.WriteTo(writer, (T)value);
            }

            public override string ToStringExp(string varName)
            {
                return $"ConfigDataManager.{GetConverterVariableNameForType(TypeName)}.ToString({varName})";
            }
        }

        private class SheetValueConverterCollection : ISheetValueConverterCollection
        {
            private class BasicConverterInfo
            {
                public SheetValueConverter Converter { get; }
                public Type Type { get; }
                public Type ConverterType { get; }
                public string[] Alias { get; }

                private BasicConverterInfo(SheetValueConverter converter, Type type, Type converterType, string[] alias)
                    => (Converter, Type, ConverterType, Alias) = (converter, type, converterType, alias);

                public static BasicConverterInfo Create<T>(IConfigValueConverter<T> converter, params string[] alias)
                    => new BasicConverterInfo(new WrappedConverter<T>(converter), typeof(T), converter.GetType(), alias);
                
                public static BasicConverterInfo Create<T>(IMultiSegConfigValueConverter<T> converter, params string[] alias)
                    => new BasicConverterInfo(new WrappedMultiSegConverter<T>(converter), typeof(T), converter.GetType(), alias);
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
                BasicConverterInfo.Create(new Vector2Converter(), "vector2", "float2"),
                BasicConverterInfo.Create(new Vector3Converter(), "vector3", "float3"),
                BasicConverterInfo.Create(new Vector4Converter(), "vector4", "float4"),
                BasicConverterInfo.Create(new ColorConverter(), "color"),
                BasicConverterInfo.Create(new Color32Converter(), "color32"),
                BasicConverterInfo.Create(new StringConverter(), "string"),
            };

            private readonly Dictionary<string, SheetValueConverter> _converters = new Dictionary<string, SheetValueConverter>();

            private readonly Dictionary<Type, ConverterInfo> _converterInfoTable = new Dictionary<Type, ConverterInfo>();

            private void LoadAssembly(Assembly assembly)
            {
                foreach (var type in assembly.GetExportedTypes()) {
                    if (type.IsGenericType || type.IsAbstract || type.IsInterface) {
                        continue;
                    }
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
                        var ifType = typeof(IConfigValueConverter<>).MakeGenericType(type);
                        if (!ifType.IsAssignableFrom(converterTypeAttr.Type)) {
                            Debug.LogError(
                                $"Custom converter '{converterTypeAttr.Type.FullName}' for type '{fullName}' does not implement IConfigValueConverter<{fullName}>");
                            continue;
                        }
                        if (converterTypeAttr.Type.GetConstructor(Array.Empty<Type>()) == null) {
                            Debug.LogError($"Custom converter '{converterTypeAttr.Type.FullName}' does not have a parameterless constructor");
                        }
                        var converter = (SheetValueConverter)Activator.CreateInstance(
                            typeof(WrappedConverter<>).MakeGenericType(type),
                            Activator.CreateInstance(converterTypeAttr.Type));

                        if (!_converters.ContainsKey(fullName)) {
                            _converters.Add(fullName, converter);
                        }
                        if (!_converters.ContainsKey(type.Name)) {
                            _converters.Add(type.Name, converter);
                        }
                        _converterInfoTable.Add(converter.Type,
                            new ConverterInfo(fullName, converterTypeAttr.Type.FullName, GetConverterVariableNameForType(fullName)));
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

            public SheetValueConverter GetConverter(string typeName)
            {
                typeName = typeName.Trim();
                if (typeName.EndsWith("[]", StringComparison.Ordinal)) {
                    return new ArrayWrappedConverter(GetConverter(typeName.Substring(0, typeName.Length - 2)));
                }
                if (typeName.EndsWith("?", StringComparison.Ordinal)) {
                    return new NullableWrappedConverter(GetConverter(typeName.Substring(0, typeName.Length - 1)));
                }
                if (typeName.First() == '{' && typeName.Last() == '}') {
                    var inner = typeName.TrimStart('{').TrimEnd('}');
                    var segs = inner.Split(new[] { ':' }, StringSplitOptions.None);
                    if (segs.Length == 2) {
                        var key = segs[0];
                        var value = segs[1];
                        return new DictionaryConverter(GetConverter(key), GetConverter(value));
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
                        new ConverterInfo(converterInfo.Type.FullName,
                        converterInfo.ConverterType.FullName,
                        GetConverterVariableNameForType(converterInfo.Type.FullName)));
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
