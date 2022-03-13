using System;

namespace Untitled.ConfigDataBuilder.Base
{
    public sealed class ConfigValueConverterAttribute : Attribute
    {
        public Type Type { get; }

        public char DefaultSeparator { get; set; } = ',';

        public string[] Aliases { get; }

        public ConfigValueConverterAttribute(Type type, params string[] aliases)
        {
            Type = type;
            Aliases = aliases;
        }
    }
}
