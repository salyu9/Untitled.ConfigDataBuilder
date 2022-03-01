using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Base
{
    public sealed class BoolConverter : IConfigValueConverter<bool>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Parse(string value) => Convert.ToBoolean(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, bool value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadFrom(BinaryReader reader)
            => reader.ReadBoolean();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(bool value)
            => value.ToString();
    }

    public sealed class Int16Converter : IConfigValueConverter<short>
    {
        public short Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt16(value, 16)
                : short.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, short value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadFrom(BinaryReader reader)
            => reader.ReadInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(short value)
            => value.ToString();
    }

    public sealed class UInt16Converter : IConfigValueConverter<ushort>
    {
        public ushort Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt16(value, 16)
                : ushort.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, ushort value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadFrom(BinaryReader reader)
            => reader.ReadUInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(ushort value)
            => value.ToString();
    }

    public sealed class Int32Converter : IConfigValueConverter<int>
    {
        public int Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(value, 16)
                : int.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, int value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadFrom(BinaryReader reader)
            => reader.ReadInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(int value)
            => value.ToString();
    }

    public sealed class UInt32Converter : IConfigValueConverter<uint>
    {
        public uint Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt32(value, 16)
                : uint.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, uint value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadFrom(BinaryReader reader)
            => reader.ReadUInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(uint value)
            => value.ToString();
    }

    public sealed class Int64Converter : IConfigValueConverter<long>
    {
        public long Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(value, 16)
                : long.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, long value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadFrom(BinaryReader reader)
            => reader.ReadInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(long value)
            => value.ToString();
    }

    public sealed class UInt64Converter : IConfigValueConverter<ulong>
    {
        public ulong Parse(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt64(value, 16)
                : ulong.Parse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, ulong value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadFrom(BinaryReader reader)
            => reader.ReadUInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(ulong value)
            => value.ToString();
    }

    public sealed class SingleConverter : IConfigValueConverter<float>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Parse(string value)
            => float.Parse(value, CultureInfo.InvariantCulture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, float value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFrom(BinaryReader reader)
            => reader.ReadSingle();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(float value)
            => value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class DoubleConverter : IConfigValueConverter<double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Parse(string value)
            => double.Parse(value, CultureInfo.InvariantCulture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, double value)
            => writer.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadFrom(BinaryReader reader)
            => reader.ReadDouble();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(double value)
            => value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class Vector2Converter : IMultiSegConfigValueConverter<Vector2>
    {
        public Vector2 Parse(string[] segs)
        {
            if (segs.Length != 2) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to {nameof(Vector2)}, segments count must be 2");
            }
            return new Vector2(float.Parse(segs[0]), float.Parse(segs[1]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Vector2 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ReadFrom(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            return new Vector2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Vector2 value)
            => value.ToString();
    }
    
    public sealed class Vector2IntConverter : IMultiSegConfigValueConverter<Vector2Int>
    {
        public Vector2Int Parse(string[] segs)
        {
            if (segs.Length != 2) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to {nameof(Vector2Int)}, segments count must be 2");
            }
            return new Vector2Int(int.Parse(segs[0]), int.Parse(segs[1]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Vector2Int value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int ReadFrom(BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            return new Vector2Int(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Vector2Int value)
            => value.ToString();
    }

    public sealed class Vector3Converter : IMultiSegConfigValueConverter<Vector3>
    {
        public Vector3 Parse(string[] segs)
        {
            if (segs.Length != 3) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to {nameof(Vector3)}, segments count must be 3");
            }
            return new Vector3(float.Parse(segs[0]), float.Parse(segs[1]), float.Parse(segs[2]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ReadFrom(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Vector3 value)
            => value.ToString();
    }
    
    public sealed class Vector3IntConverter : IMultiSegConfigValueConverter<Vector3Int>
    {
        public Vector3Int Parse(string[] segs)
        {
            if (segs.Length != 3) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to {nameof(Vector3Int)}, segments count must be 3");
            }
            return new Vector3Int(int.Parse(segs[0]), int.Parse(segs[1]), int.Parse(segs[2]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Vector3Int value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int ReadFrom(BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var z = reader.ReadInt32();
            return new Vector3Int(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Vector3Int value)
            => value.ToString();
    }

    public sealed class Vector4Converter : IMultiSegConfigValueConverter<Vector4>
    {
        public Vector4 Parse(string[] segs)
        {
            if (segs.Length != 4) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to Vector4, segments count must be 4");
            }
            return new Vector4(float.Parse(segs[0]), float.Parse(segs[1]), float.Parse(segs[2]), float.Parse(segs[3]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Vector4 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ReadFrom(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            var w = reader.ReadSingle();
            return new Vector4(x, y, z, w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Vector4 value)
            => value.ToString();
    }

    public sealed class ColorConverter : IMultiSegConfigValueConverter<Color>
    {
        public Color Parse(string[] segs)
        {
            if (segs.Length != 4) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to Color, segments count must be 4 (r, g, b, a)");
            }
            return new Color(float.Parse(segs[0]), float.Parse(segs[1]), float.Parse(segs[2]), float.Parse(segs[3]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Color value)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadFrom(BinaryReader reader)
        {
            var r = reader.ReadSingle();
            var g = reader.ReadSingle();
            var b = reader.ReadSingle();
            var a = reader.ReadSingle();
            return new Color(r, g, b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Color value)
            => value.ToString();
    }

    public sealed class Color32Converter : IMultiSegConfigValueConverter<Color32>
    {
        public Color32 Parse(string[] segs)
        {
            if (segs.Length != 4) {
                throw new InvalidDataException($"Cannot convert \"{string.Join(",", segs)}\" to Color32, segments count must be 4 (r, g, b, a)");
            }
            return new Color32(byte.Parse(segs[0]), byte.Parse(segs[1]), byte.Parse(segs[2]), byte.Parse(segs[3]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(BinaryWriter writer, Color32 value)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 ReadFrom(BinaryReader reader)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            return new Color32(r, g, b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(Color32 value)
            => value.ToString();
    }

    public sealed class StringConverter : IConfigValueConverter<string>
    {
        public string Parse(string value)
            => value;

        public void WriteTo(BinaryWriter writer, string value)
        {
            if (value != null) {
                writer.Write(true);
                writer.Write(value);
            }
            else {
                writer.Write(false);
            }
        }

        public string ReadFrom(BinaryReader reader)
            => reader.ReadBoolean() ? reader.ReadString() : null;

        public string ToString(string value)
            => value;
    }
}
