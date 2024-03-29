﻿using ExcelDataReader;
using System.IO;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal sealed class ExcelSheetReader : ISheetReader
    {
        private readonly IExcelDataReader _reader;
        public ExcelSheetReader(string path)
        {
            _reader = ExcelReaderFactory.CreateReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        }

        public string SheetName => _reader.Name;

        public bool ReadNextSheet() => _reader.NextResult();

        public bool ReadNextRow() => _reader.Read();

        public int CellCount => _reader.FieldCount;

        public bool IsNull(int index) => _reader.IsDBNull(index);

        public string Get(int index) => _reader.GetValue(index).ToString();

        public void Dispose()
        {
            _reader.Dispose();
        }

    }
}
