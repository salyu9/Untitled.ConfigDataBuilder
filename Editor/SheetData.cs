using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class SheetData
    {
        public string MergedSheetName { get; private set; }
        public string ClassName { get; private set; }
        public ColumnInfo[] Header { get; private set; }
        public ColumnInfo[] Keys { get; private set; }
        public object[][] Rows { get; private set; }

        private class InternalSheetData
        {
            public string Path { get; set; }
            public string SheetName { get; set; }
            public List<ColumnInfo> Header { get; set; }
            public List<object[]> Rows { get; set; }
        }

        private static InternalSheetData ReadSheet(SheetDataReaderContext context, ISheetReader reader, string path, bool headerOnly)
        {
            var sheetName = reader.SheetName;
            // Name row
            if (!reader.ReadNextRow()) {
                throw new InvalidDataException($"{path}({sheetName}) has no header row.");
            }
            var header = new List<ColumnInfo>();
            var colNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var colIndex = 0; colIndex < reader.CellCount; ++colIndex) {
                if (reader.IsNull(colIndex)) {
                    header.Add(new ColumnInfo { IsIgnored = true });
                }
                else {
                    var colName = reader.Get(colIndex).Trim();
                    if (string.IsNullOrEmpty(colName) || colName[0] == '(' || colName[0] == '（') {
                        header.Add(new ColumnInfo { IsIgnored = true });
                    }
                    else {
                        if (!Helper.CodeProvider.IsValidIdentifier(colName)) {
                            throw new InvalidOperationException(
                                $"{path}({sheetName}) has invalid property name '{colName}'.");
                        }
                        if (colNameSet.Contains(colName)) {
                            throw new InvalidOperationException(
                                $"{path}({sheetName}) has duplicated property '{colName}'.");
                        }
                        colNameSet.Add(colName);

                        header.Add(new ColumnInfo {
                            ColIndex = colIndex,
                            Name = colName,
                            DebugName = $"{path}({sheetName}) '{colName}'"
                        });
                    }
                }
            }

            // Type row
            if (!reader.ReadNextRow()) {
                throw new InvalidDataException($"{path}({sheetName}) has no type row");
            }
            for (var colIndex = 0; colIndex < reader.CellCount; ++colIndex) {
                var colInfo = header[colIndex];
                if (reader.IsNull(colIndex)) {
                    // ignore columns with empty type
                    colInfo.IsIgnored = true;
                }
                else if (!colInfo.IsIgnored) {
                    colInfo.ConfigTypeName = reader.Get(colIndex);
                }
            }

            // Flag row
            for (var flagRowIndex = 0; flagRowIndex < context.FlagRowCount; ++flagRowIndex) {
                if (!reader.ReadNextRow()) {
                    throw flagRowIndex == 0
                        ? new InvalidDataException($"{path}({sheetName}) has no flag rows")
                        : new InvalidDataException($"{path}({sheetName}) only has {flagRowIndex} flag rows (required: {context.FlagRowCount})");
                }
                for (var colIndex = 0; colIndex < reader.CellCount; ++colIndex) {
                    var colInfo = header[colIndex];
                    if (colInfo.IsIgnored) {
                        continue;
                    }

                    if (!reader.IsNull(colIndex)) {
                        foreach (var flag in Helper.SplitEscapedString(reader.Get(colIndex), '|')) {
                            if (string.IsNullOrWhiteSpace(flag)) {
                                continue;
                            }
                            var colonIndex = flag.IndexOf(':');
                            if (colonIndex >= 0) {
                                var flagName = flag.Substring(0, colonIndex).Trim().ToLower();
                                var flagArg = Helper.UnescapeString(flag.Substring(colonIndex + 1).Trim());
                                if (context.GetArgumentedFlagHandler(flagName, out var handler)) {
                                    handler.HandleColumn(colInfo, flagArg);
                                }
                                else {
                                    throw new InvalidDataException($"{colInfo.DebugName} has unknown flag '{flagName}:{flagArg}'");
                                }
                            }
                            else {
                                var flagName = flag.Trim().ToLower();
                                if (context.GetFlagHandler(flagName, out var handler)) {
                                    handler.HandleColumn(colInfo);
                                }
                                else {
                                    throw new InvalidDataException($"{colInfo.DebugName} has unknown flag '{flagName}'");
                                }
                            }
                        }
                    }
                }
            }

            // Densify
            header.RemoveAll(info => info.IsIgnored);

            // Post flags
            foreach (var colInfo in header) {
                if (colInfo.IsKey && colInfo.Info != InfoType.None) {
                    throw new InvalidDataException($"{colInfo.DebugName} is key AND info.");
                }
                if (colInfo.IsKey && colInfo.IsIgnored) {
                    throw new InvalidDataException($"{colInfo.DebugName} is key AND ignored.");
                }
                if (colInfo.Info != InfoType.None && colInfo.IsIgnored) {
                    throw new InvalidDataException($"{colInfo.DebugName} is info AND ignored.");
                }
                try {
                    colInfo.Converter = context.GetConverter(colInfo.ConfigTypeName);
                }
                catch (Exception exc) {
                    throw new InvalidDataException($"{path}({sheetName}): Cannot create converter for type {colInfo.Name}", exc);
                }
                if (colInfo.Separators != null) {
                    if (colInfo.Separators.Length != colInfo.Converter.SeparatorLevel) {
                        throw new InvalidDataException(
                            $"{colInfo.DebugName} separators {colInfo.Separators} size does not match the separator-level of '{colInfo.Converter.TypeName}' (level = {colInfo.Converter.SeparatorLevel})");
                    }
                }
                else {
                    if (!colInfo.Converter.TryCreateDefaultSeparators(out var separators)) {
                        throw new InvalidDataException($"{colInfo.DebugName} need to specify separators.");
                    }
                    colInfo.Separators = separators;
                }
                if (colInfo.DefaultValue != null) {
                    if (!colInfo.Converter.Type.IsInstanceOfType(colInfo.DefaultValue)) {
                        throw new InvalidOperationException($"{colInfo.DebugName} default value {colInfo.DefaultValue} is not instance of {colInfo.Converter.TypeName}");
                    }
                }
                else {
                    if (!string.IsNullOrWhiteSpace(colInfo.RawDefaultValue)) {
                        try {
                            colInfo.DefaultValue = colInfo.Converter.ParseEscaped(colInfo.RawDefaultValue, colInfo.Separators);
                        }
                        catch (Exception exc) {
                            throw new InvalidDataException(
                                $"{colInfo.DebugName} default value ({colInfo.RawDefaultValue}) cannot be converted to {colInfo.Converter.TypeName}: {exc.Message}",
                                exc);
                        }
                    }
                }
            }

            if (headerOnly) {
                return new InternalSheetData {
                    Path = path,
                    SheetName = sheetName,
                    Header = header,
                    Rows = new List<object[]>()
                };
            }

            // Rows
            var rowIndex = 3;
            var rows = new List<object[]>();
            while (reader.ReadNextRow()) {
                // skip rows with empty key
                if (header.Any(col => col.IsKey && reader.IsNull(col.ColIndex))) {
                    ++rowIndex;
                    continue;
                }

                var currentRow = new object[header.Count];
                var index = 0;
                foreach (var colInfo in header) {
                    if (reader.IsNull(colInfo.ColIndex) && colInfo.DefaultValue is { } v) {
                        currentRow[index] = v;
                    }
                    else {
                        var raw = reader.IsNull(colInfo.ColIndex) ? null : reader.Get(colInfo.ColIndex);
                        var converter = colInfo.Converter;
                        object result;
                        try {
                            result = converter.ParseEscaped(raw, colInfo.Separators);
                            System.Diagnostics.Debug.Assert(result == null || result.GetType() == converter.Type);
                        }
                        catch (Exception exc) {
                            throw new InvalidDataException(
                                $"{path}({sheetName})(row: {rowIndex + 1}, col: {colInfo.ColIndex + 1}): cannot parse  {(raw != null ? "data \'" + raw + "\'" : "null")} to {converter.TypeName}: {exc.Message}");
                        }
                        if (colInfo.Keys is { } set) {
                            if (set.Contains(result)) {
                                throw new InvalidDataException(
                                    $"{path}({sheetName})(row: {rowIndex + 1}, col: {colInfo.ColIndex + 1}): duplicated key '{result}'");
                            }
                            set.Add(result);
                        }
                        currentRow[index] = result;
                    }
                    ++index;
                }
                rows.Add(currentRow);
                ++rowIndex;
            }

            return new InternalSheetData {
                Path = path,
                SheetName = sheetName,
                Header = header,
                Rows = rows
            };
        }

        /// <summary>
        /// Merge data from sheet2 to sheet1
        /// </summary>
        private static void MergeSheet(InternalSheetData sheet1, InternalSheetData sheet2)
        {
            // header & keys check
            var min = Math.Min(sheet1.Header.Count, sheet2.Header.Count);
            for (var i = 0; i < min; ++i) {
                var col1 = sheet1.Header[i];
                var col2 = sheet2.Header[i];
                if (col1.Name != col2.Name) {
                    throw new InvalidDataException(
                        $"{sheet2.Path}({sheet2.SheetName}) has inconsistent column name '{col2.Name}', should be {col1.Name}");
                }
                if (col1.Converter.Type != col2.Converter.Type) {
                    throw new InvalidDataException(
                        $"{sheet2.Path}({sheet2.SheetName}) column '{col2.Name}' has inconsistent type '{col2.Converter.TypeName}', should be {col1.Converter.TypeName}");
                }
                if (col1.IsKey != col2.IsKey) {
                    throw new InvalidDataException(
                        $"{sheet2.Path}({sheet2.SheetName}) column '{col2.Name}' has inconsistent key flag");
                }
                if (col1.IsKey) {
                    foreach (var key in col2.Keys) {
                        if (col1.Keys.Contains(key)) {
                            throw new InvalidDataException(
                                $"{sheet2.Path}({sheet2.SheetName}) column '{col2.Name}' has duplicated key '{key}' with other sheet");
                        }
                    }
                }
                if (col1.Info != col2.Info) {
                    throw new InvalidDataException(
                        $"{sheet2.Path}({sheet2.SheetName}) column '{col2.Name}' has inconsistent info flag");
                }
            }
            if (min < sheet1.Header.Count) {
                throw new InvalidDataException($"{sheet2.Path}({sheet2.SheetName}) missing columns '{sheet1.Header[min].Name}'");
            }
            if (min < sheet2.Header.Count) {
                throw new InvalidDataException(
                    $"{sheet2.Path}({sheet2.SheetName}) has more columns '{sheet2.Header[min].Name}'");
            }

            // merge
            foreach (var (col1, col2) in sheet1.Header.Zip(sheet2.Header, (c1, c2) => (c1, c2))) {
                if (col1.IsKey) {
                    foreach (var key in col2.Keys) {
                        col1.Keys.Add(key);
                    }
                }
            }
            foreach (var row in sheet2.Rows) {
                sheet1.Rows.Add(row);
            }
        }

        /// <summary>
        /// Read all sheets from all files.
        /// </summary>
        private static List<SheetData> InternalReadSheets(SheetDataReaderContext context, IEnumerable<string> paths, bool headerOnly)
        {
            var internalResult = new Dictionary<string, InternalSheetData>();

            foreach (var path in paths) {
                var ext = Path.GetExtension(path);
                using ISheetReader reader = ext switch {
                    ".xls"  => new ExcelSheetReader(path),
                    ".xlsx" => new ExcelSheetReader(path),
                    ".fods" => new FodsSheetReader(path),
                    _       => throw new ArgumentOutOfRangeException()
                };
                do {
                    var sheetName = reader.SheetName.Trim();
                    if (string.IsNullOrEmpty(sheetName) || sheetName[0] == '(' || sheetName[0] == '（') {
                        continue;
                    }
                    var periodIndex = sheetName.IndexOf('.');
                    var className = periodIndex >= 0 ? sheetName.Substring(0, periodIndex) : sheetName;
                    if (!Helper.CodeProvider.IsValidIdentifier(className)) {
                        throw new InvalidOperationException($"{className} is not valid identifier name.");
                    }
                    var newSheetData = ReadSheet(context, reader, path, headerOnly);
                    if (internalResult.TryGetValue(className, out var sheetData)) {
                        MergeSheet(sheetData, newSheetData);
                    }
                    else {
                        internalResult.Add(className, newSheetData);
                    }

                } while (reader.ReadNextSheet());
            }

            var result = new List<SheetData>();
            foreach (var kv in internalResult) {
                var name = kv.Key;
                var data = kv.Value;
                var header = new List<ColumnInfo>();
                var keys = new List<ColumnInfo>();
                var colIndex = 0;
                foreach (var colInfo in data.Header) {
                    var columnInfo = new ColumnInfo {
                        ColIndex = colIndex++,
                        Name = colInfo.Name,
                        Converter = colInfo.Converter,
                        Keys = colInfo.Keys,
                        Info = colInfo.Info
                    };
                    header.Add(columnInfo);
                    if (colInfo.IsKey) {
                        keys.Add(columnInfo);
                    }
                }
                result.Add(new SheetData {
                    MergedSheetName = name,
                    ClassName = name + "Config",
                    Header = header.ToArray(),
                    Keys = keys.ToArray(),
                    Rows = data.Rows.ToArray(),
                });
            }
            return result;
        }

        public static List<SheetData> ReadAllSheets(SheetDataReaderContext converters, IEnumerable<string> paths)
        {
            return InternalReadSheets(converters, paths, false);
        }

        public static List<SheetData> ReadAllHeaders(SheetDataReaderContext converters, IEnumerable<string> paths)
        {
            return InternalReadSheets(converters, paths, true);
        }
    }
}
