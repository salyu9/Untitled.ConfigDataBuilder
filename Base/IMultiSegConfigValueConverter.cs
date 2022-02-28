using System.IO;

namespace Untitled.ConfigDataBuilder.Base
{
    public interface IMultiSegConfigValueConverter<T>
    {
        /// <summary>
        /// Parse array of string to value of type <typeparamref name="T"/>.
        /// </summary>
        T Parse(string[] segs);

        /// <summary>
        /// Write value of T to <see cref="BinaryWriter"/>.
        /// </summary>
        void WriteTo(BinaryWriter writer, T value);

        /// <summary>
        /// Read value of T from <see cref="BinaryReader"/>.
        /// </summary>
        T ReadFrom(BinaryReader reader);

        /// <summary>
        /// Get a string that represents specified value.
        /// </summary>
        string ToString(T value);

        /// <summary>
        /// Determines whether this type is scalar (can be used in arrays/dictionaries).
        /// </summary>
        bool IsScalar { get; }
    }
}