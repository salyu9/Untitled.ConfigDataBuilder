using System;

namespace Untitled.ConfigDataBuilder.Base
{
    public sealed class ConfigValueConverterAttribute : Attribute
    {
        public Type Type { get; }
        
        public ConfigValueConverterAttribute(Type type)
        {
            Type = type;
        }
    }
}
