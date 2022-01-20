using System;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal interface ISheetReader : IDisposable
    {
        public string Name { get; }

        public bool ReadNextSheet();

        public bool ReadNextRow();

        public int CellCount { get; }

        public bool IsNull(int index);

        public object GetValue(int index);
    }
}
