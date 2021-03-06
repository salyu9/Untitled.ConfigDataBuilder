using System.IO;

namespace Untitled.ConfigDataBuilder.Base
{
    public interface IConfigValueConverter<T>
    {
        /// <summary>
        /// Parse string to value of type <typeparamref name="T"/>.
        /// </summary>
        T Parse(string value);

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
    }
}
