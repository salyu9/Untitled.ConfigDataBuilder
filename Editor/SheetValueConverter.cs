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
    internal interface IMultiSegConverter
    {
        bool IgnoreEmpty { get; set; }
    }

    internal interface ISheetValueConverterCollection
    {
        SheetValueConverter GetConverter(string typeName);
        IEnumerable<CustomConverterInfo> GetCustomConverterInfo();
    }

    internal class CustomConverterInfo
    {
        public string TypeName { get; }
        public string ConverterTypeName { get; }
        public string VariableName { get; }

        public CustomConverterInfo(string typeName, string converterTypeName, string variableName)
            => (TypeName, ConverterTypeName, VariableName) = (typeName, converterTypeName, variableName);
    }

    internal abstract class SheetValueConverter
    {
        public static string GetCustomConverterVariableNameForType(string typeFullName)
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
        public abstract object Convert(object rawValue);
        public abstract string ReadBinaryExp(string readerVarName);
        public abstract void WriteBinary(BinaryWriter writer, object value);

        public virtual string ToStringExp(string varName)
            => varName + ".ToString()";

        private class BoolConverter : SheetValueConverter
        {
            public override string TypeName => "bool";
            public override Type Type { get; } = typeof(bool);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
                => rawValue is bool b ? b : System.Convert.ToBoolean(rawValue);

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadBoolean()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((bool)value);
        }

        private class Int16Converter : SheetValueConverter
        {
            public override string TypeName => "short";
            public override Type Type { get; } = typeof(short);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToInt16(s, 16);
                    }
                    return short.Parse(s);
                }
                return ((IConvertible)rawValue).ToInt16(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadInt16()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((short)value);
        }

        private class UInt16Converter : SheetValueConverter
        {
            public override string TypeName => "ushort";
            public override Type Type { get; } = typeof(ushort);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToInt16(s, 16);
                    }
                    return ushort.Parse(s);
                }
                return ((IConvertible)rawValue).ToInt16(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadUInt16()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((ushort)value);
        }

        private class Int32Converter : SheetValueConverter
        {
            public override string TypeName => "int";
            public override Type Type { get; } = typeof(int);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToInt32(s, 16);
                    }
                    return int.Parse(s);
                }
                return ((IConvertible)rawValue).ToInt32(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadInt32()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((int)value);
        }

        private class UInt32Converter : SheetValueConverter
        {
            public override string TypeName => "uint";
            public override Type Type { get; } = typeof(uint);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToUInt32(s, 16);
                    }
                    return uint.Parse(s);
                }
                return ((IConvertible)rawValue).ToUInt32(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadUInt32()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((uint)value);
        }

        private class Int64Converter : SheetValueConverter
        {
            public override string TypeName => "long";
            public override Type Type { get; } = typeof(long);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToInt64(s, 16);
                    }
                    return long.Parse(s);
                }
                return ((IConvertible)rawValue).ToInt64(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadInt64()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((long)value);
        }

        private class UInt64Converter : SheetValueConverter
        {
            public override string TypeName => "ulong";
            public override Type Type { get; } = typeof(ulong);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
            {
                if (rawValue is string s) {
                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        return System.Convert.ToUInt64(s, 16);
                    }
                    return ulong.Parse(s);
                }
                return ((IConvertible)rawValue).ToUInt64(null);
            }

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadUInt64()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((ulong)value);
        }

        private class SingleConverter : SheetValueConverter
        {
            public override string TypeName => "float";
            public override Type Type { get; } = typeof(float);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
                => ((IConvertible)rawValue).ToSingle(null);

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadSingle()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((float)value);
        }

        private class DoubleConverter : SheetValueConverter
        {
            public override string TypeName => "double";
            public override Type Type { get; } = typeof(double);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
                => ((IConvertible)rawValue).ToDouble(null);

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadDouble()";

            public override void WriteBinary(BinaryWriter writer, object value)
                => writer.Write((double)value);
        }

        private class Vector2Converter : SheetValueConverter
        {
            public override string TypeName => "UnityEngine.Vector2";
            public override Type Type { get; } = typeof(Vector2);
            public override bool IsScalar => false;
            public override bool HasSeparator => true;

            public override object Convert(object rawValue)
            {
                if (!(rawValue is string str)) {
                    throw new InvalidDataException($"Cannot convert {rawValue} to Vector2, require string");
                }
                var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                if (segs.Length != 2) {
                    throw new InvalidDataException($"Cannot convert \"{str}\" to Vector2, segments count must be 2");
                }
                return new Vector2(float.Parse(segs[0]), float.Parse(segs[1]));
            }

            public override string ReadBinaryExp(string readerVarName)
                => "new UnityEngine.Vector2("
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle())";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var vector = (Vector2)value;
                writer.Write(vector.x);
                writer.Write(vector.y);
            }
        }

        private class Vector3Converter : SheetValueConverter
        {
            public override string TypeName => "UnityEngine.Vector3";
            public override Type Type { get; } = typeof(Vector3);
            public override bool IsScalar => false;
            public override bool HasSeparator => true;

            public override object Convert(object rawValue)
            {
                if (!(rawValue is string str)) {
                    throw new InvalidDataException($"Cannot convert {rawValue} to Vector3, require string");
                }
                var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                if (segs.Length != 3) {
                    throw new InvalidDataException($"Cannot convert \"{str}\" to Vector3, segments count must be 3");
                }
                return new Vector3(float.Parse(segs[0]), float.Parse(segs[1]), float.Parse(segs[2]));
            }

            public override string ReadBinaryExp(string readerVarName)
                => "new UnityEngine.Vector3("
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle())";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var vector = (Vector3)value;
                writer.Write(vector.x);
                writer.Write(vector.y);
                writer.Write(vector.z);
            }
        }

        private class Vector4Converter : SheetValueConverter
        {
            public override string TypeName => "UnityEngine.Vector4";
            public override Type Type { get; } = typeof(Vector4);
            public override bool IsScalar => false;
            public override bool HasSeparator => true;

            public override object Convert(object rawValue)
            {
                if (!(rawValue is string str)) {
                    throw new InvalidDataException($"Cannot convert {rawValue} to Vector4, require string");
                }
                var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                if (segs.Length != 4) {
                    throw new InvalidDataException($"Cannot convert \"{str}\" to Vector4, segments count must be 4");
                }
                return new Vector4(
                    float.Parse(segs[0]),
                    float.Parse(segs[1]),
                    float.Parse(segs[2]),
                    float.Parse(segs[3])
                );
            }

            public override string ReadBinaryExp(string readerVarName)
                => "new UnityEngine.Vector4("
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle())";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var vector = (Vector4)value;
                writer.Write(vector.x);
                writer.Write(vector.y);
                writer.Write(vector.z);
                writer.Write(vector.w);
            }
        }

        private class ColorConverter : SheetValueConverter
        {
            public override string TypeName => "UnityEngine.Color";
            public override Type Type { get; } = typeof(Color);
            public override bool IsScalar => false;
            public override bool HasSeparator => true;

            public override object Convert(object rawValue)
            {
                if (!(rawValue is string str)) {
                    throw new InvalidDataException($"Cannot convert {rawValue} to Color, require string (r, g, b, a)");
                }
                var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                if (segs.Length != 4) {
                    throw new InvalidDataException(
                        $"Cannot convert \"{str}\" to Color, segments count must be 4 (r, g, b, a)");
                }
                return new Color(
                    float.Parse(segs[0]),
                    float.Parse(segs[1]),
                    float.Parse(segs[2]),
                    float.Parse(segs[3])
                );
            }

            public override string ReadBinaryExp(string readerVarName)
                => "new UnityEngine.Color("
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle(), "
                  + readerVarName + ".ReadSingle())";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var color = (Color)value;
                writer.Write(color.r);
                writer.Write(color.g);
                writer.Write(color.b);
                writer.Write(color.a);
            }
        }

        private class Color32Converter : SheetValueConverter
        {
            public override string TypeName => "UnityEngine.Color32";
            public override Type Type { get; } = typeof(Color32);
            public override bool IsScalar => false;
            public override bool HasSeparator => true;

            public override object Convert(object rawValue)
            {
                if (!(rawValue is string str)) {
                    throw new InvalidDataException(
                        $"Cannot convert {rawValue} to Color32, require string (r, g, b, a)");
                }
                var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                if (segs.Length != 4) {
                    throw new InvalidDataException(
                        $"Cannot convert \"{str}\" to Color32, segments count must be 4 (r, g, b, a)");
                }
                return new Color32(byte.Parse(segs[0]), byte.Parse(segs[1]), byte.Parse(segs[2]), byte.Parse(segs[3]));
            }

            public override string ReadBinaryExp(string readerVarName)
                => "new UnityEngine.Color32("
                  + readerVarName + ".ReadByte(), "
                  + readerVarName + ".ReadByte(), "
                  + readerVarName + ".ReadByte(), "
                  + readerVarName + ".ReadByte())";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var color = (Color32)value;
                writer.Write(color.r);
                writer.Write(color.g);
                writer.Write(color.b);
                writer.Write(color.a);
            }
        }

        private class StringConverter : SheetValueConverter
        {
            public override string TypeName => "string";
            public override Type Type { get; } = typeof(string);
            public override bool HasSeparator => false;

            public override object Convert(object rawValue)
                => rawValue?.ToString();

            public override string ReadBinaryExp(string readerVarName)
                => readerVarName + ".ReadBoolean() ? " + readerVarName + ".ReadString() : null";

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                var s = (string)value;
                if (s != null) {
                    writer.Write(true);
                    writer.Write(s);
                }
                else {
                    writer.Write(false);
                }
            }
        }

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

            public override object Convert(object rawValue)
            {
                if (rawValue == null) return null;
                if (rawValue is string s && (string.IsNullOrWhiteSpace(s) ||
                    s.Equals("null", StringComparison.OrdinalIgnoreCase))) {
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

        private class ArrayWrappedConverter : SheetValueConverter, IMultiSegConverter
        {
            public override string TypeName
                => _converter.TypeName + "[]";

            public override Type Type
                => _converter.Type.MakeArrayType();

            public override bool HasSeparator => true;

            public override bool IsScalar => false;
            public override bool IsCollection => true;
            public override bool CanBeKey => false;

            public bool IgnoreEmpty { get; set; }

            private readonly SheetValueConverter _converter;

            public ArrayWrappedConverter(SheetValueConverter converter)
            {
                if (!converter.IsScalar) {
                    throw new NotSupportedException("Array of non-scalar type not supported.");
                }
                _converter = converter;
            }

            public override object Convert(object rawValue)
            {
                if (rawValue == null) return Array.CreateInstance(_converter.Type, 0);
                if (rawValue is string str) {
                    if (string.IsNullOrWhiteSpace(str) || str.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                        return Array.CreateInstance(_converter.Type, 0);
                    }
                    var segs = str.Split(new[] { Separator ?? "," }, StringSplitOptions.None);
                    var list = new List<object>();
                    foreach (var seg in segs)
                    {
                        if (IgnoreEmpty && string.IsNullOrWhiteSpace(seg)) {
                            continue;
                        }
                        list.Add(_converter.Convert(seg.Trim()));
                    }
                    var result = Array.CreateInstance(_converter.Type, list.Count);
                    for (var i = 0; i < list.Count; ++i) {
                        result.SetValue(list[i], i);
                    }
                    return result;
                }

                var singleElem = Array.CreateInstance(_converter.Type, 1);
                singleElem.SetValue(_converter.Convert(rawValue), 0);
                return singleElem;
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

            public override object Convert(object rawValue)
            {
                var elemSplitter = new[] { Separator ?? "," };
                var kvSplitter = new[] { ':' };
                var result = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(_keyConverter.Type, _valueConverter.Type));
                if (rawValue == null) return result;
                if (rawValue is string str) {
                    if (string.IsNullOrWhiteSpace(str) || str.Equals("null", StringComparison.OrdinalIgnoreCase)) {
                        return result;
                    }
                    var segs = str.Split(elemSplitter, StringSplitOptions.None);
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

                throw new InvalidCastException($"Cannot convert '{rawValue}'({rawValue.GetType().Name}) to {Type.Name}");
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

            public override object Convert(object rawValue)
            {
                if (rawValue is string str) {
                    if (_isFlags) {
                        var values = str.Split(new[] { Separator ?? "|" }, StringSplitOptions.None);
                        if (values.Length == 0) {
                            throw new ArgumentException($"Empty value for {TypeName}");
                        }
                        var result = 0L;
                        foreach (var value in values) {
                            result |= System.Convert.ToInt64(Enum.Parse(Type, value));
                        }
                        return Enum.ToObject(Type, result);
                    }
                    return Enum.Parse(Type, str);
                }
                throw new ArgumentException($"Cannot convert '{rawValue}' to {TypeName}");
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

        private sealed class CustomTypeConverter<T> : SheetValueConverter
        {
            private readonly IConfigValueConverter<T> _innerConverter;

            public override string TypeName { get; }

            public override Type Type { get; }

            public override bool HasSeparator => false;

            public override bool IsScalar => _innerConverter.IsScalar;

            public CustomTypeConverter(IConfigValueConverter<T> innerConverter)
            {
                _innerConverter = innerConverter;
                Type = typeof(T);
                TypeName = Type.FullName;
            }

            public override object Convert(object rawValue)
            {
                return _innerConverter.Parse(rawValue is string s ? s : rawValue.ToString());
            }

            public override string ReadBinaryExp(string readerVarName)
            {
                return $"ConfigDataManager.{GetCustomConverterVariableNameForType(TypeName)}.ReadFrom({readerVarName})";
            }

            public override void WriteBinary(BinaryWriter writer, object value)
            {
                _innerConverter.WriteTo(writer, (T)value);
            }

            public override string ToStringExp(string varName)
            {
                return $"ConfigDataManager.{GetCustomConverterVariableNameForType(TypeName)}.ToString({varName})";
            }
        }

        private class SheetValueConverterCollection : ISheetValueConverterCollection
        {
            private readonly Dictionary<string, SheetValueConverter> _converters = new Dictionary<string, SheetValueConverter> {
                ["bool"] = new BoolConverter(),
                ["short"] = new Int16Converter(),
                ["ushort"] = new UInt16Converter(),
                ["int"] = new Int32Converter(),
                ["uint"] = new UInt32Converter(),
                ["long"] = new Int64Converter(),
                ["ulong"] = new UInt64Converter(),
                ["float"] = new SingleConverter(),
                ["double"] = new DoubleConverter(),
                ["Vector2"] = new Vector2Converter(),
                ["UnityEngine.Vector2"] = new Vector2Converter(),
                ["Vector3"] = new Vector3Converter(),
                ["UnityEngine.Vector3"] = new Vector3Converter(),
                ["Vector4"] = new Vector4Converter(),
                ["UnityEngine.Vector4"] = new Vector4Converter(),
                ["Color"] = new ColorConverter(),
                ["UnityEngine.Color"] = new ColorConverter(),
                ["Color32"] = new Color32Converter(),
                ["UnityEngine.Color32"] = new Color32Converter(),
                ["string"] = new StringConverter(),
            };

            private readonly Dictionary<string, CustomConverterInfo> _customConverterInfoTable = new Dictionary<string, CustomConverterInfo>();

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
                            typeof(CustomTypeConverter<>).MakeGenericType(type),
                            Activator.CreateInstance(converterTypeAttr.Type));

                        if (!_converters.ContainsKey(fullName)) {
                            _converters.Add(fullName, converter);
                        }
                        if (!_converters.ContainsKey(type.Name)) {
                            _converters.Add(type.Name, converter);
                        }
                        _customConverterInfoTable.Add(fullName,
                            new CustomConverterInfo(fullName, converterTypeAttr.Type.FullName, GetCustomConverterVariableNameForType(fullName)));
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

            public IEnumerable<CustomConverterInfo> GetCustomConverterInfo()
            {
                return _customConverterInfoTable.Values;
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
