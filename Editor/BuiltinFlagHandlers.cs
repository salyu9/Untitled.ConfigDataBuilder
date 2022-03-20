using System;
using System.Collections.Generic;
using System.IO;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class KeyFlagHandler : IFlagHandler
    {
        public void HandleColumn(ColumnInfo columnInfo)
        {
            columnInfo.IsKey = true;
            columnInfo.Keys = new HashSet<object>();
        }
    }

    internal class IgnoreFlagHandler : IFlagHandler
    {
        public void HandleColumn(ColumnInfo columnInfo)
        {
            columnInfo.IsIgnored = true;
        }
    }

    internal class DefaultValueFlagHandler : IFlagHandlerWithArgument
    {
        public void HandleColumn(ColumnInfo columnInfo, string arg)
        {
            columnInfo.RawDefaultValue = arg;
        }
    }

    internal class SeparatorFlagHandler : IFlagHandlerWithArgument
    {
        public void HandleColumn(ColumnInfo columnInfo, string arg)
        {
            columnInfo.Separators = arg;
        }
    }

    internal class InfoFlagHandler : IFlagHandlerWithArgument
    {
        public void HandleColumn(ColumnInfo columnInfo, string arg)
        {
            if (!Enum.TryParse<InfoType>(arg, true, out var info)) {
                throw new InvalidDataException($"{columnInfo.DebugName} has invalid info type '{arg}'");
            }
            columnInfo.Info = info;
        }
    }
}
