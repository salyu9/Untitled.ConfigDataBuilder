using System;

namespace Untitled.ConfigDataBuilder.Base
{
    public sealed class ConfigValueConverterAttribute : Attribute
    {
        public Type Type { get; }

        public char DefaultSeparator { get; set; } = ',';
        
        public ConfigValueConverterAttribute(Type type)
        {
            Type = type;
        }
    }
}
