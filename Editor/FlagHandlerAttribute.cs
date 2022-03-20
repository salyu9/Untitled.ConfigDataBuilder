using System;

namespace Untitled.ConfigDataBuilder.Editor
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class FlagHandlerAttribute : Attribute
    {
        public string Name { get; }

        public FlagHandlerAttribute(string name)
        {
            Name = name;
        }
    }
}
