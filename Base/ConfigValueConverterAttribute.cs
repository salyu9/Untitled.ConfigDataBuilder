using System;

namespace Untitled.ConfigDataBuilder.Base
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface)]
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
