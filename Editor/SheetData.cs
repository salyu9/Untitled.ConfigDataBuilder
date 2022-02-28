using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class SheetData
    {
        private static readonly Regex IdReg = new Regex(@"^[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

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

        private static char GetUnescapedCharFor(char c)
        {
            return c switch {
                'a' => '\a',
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'v' => '\v',
                'f' => '\f',
                'r' => '\r',
                'e' => (char)27,
                _   => c
            };
        }

        private static string UnescapeString(string input)
        {
            var i = 0;
            var len = input.Length;
            var escaping = false;
            var builder = new StringBuilder();
            while (i < len) {
                var c = input[i];

                if (escaping) {
                    builder.Append(GetUnescapedCharFor(c));
                    escaping = false;
                }
                else if (c == '\\') {
                    escaping = true;
                }
                else {
                    builder.Append(c);
                }
                ++i;
            }
            if (escaping) {
                throw new ArgumentException($"Invalid escaping end of '{input}'");
            }
            return builder.ToString();
        }

        private static IEnumerable<string> EscapableSplitString(string input, char separator)
        {
            var i = 0;
            var len = input.Length;
            var escaping = false;
            var builder = new StringBuilder();
            while (i < len) {
                var c = input[i];

                if (escaping) {
                    builder.Append(GetUnescapedCharFor(c));
                    escaping = false;
                }
                else if (c == '\\') {
                    escaping = true;
                }
                else if (c == separator) {
                    yield return builder.ToString();
                    builder = new StringBuilder();
                }
                else {
                    builder.Append(c);
                }
                ++i;
            }
            if (escaping) {
                throw new ArgumentException($"Invalid escaping end of '{input}'");
            }
            if (builder.Length > 0) {
                yield return builder.ToString();
            }
        }

        private static InternalSheetData ReadSheet(ISheetValueConverterCollection converters, ISheetReader reader, string path, bool headerOnly)
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
                    var colName = reader.GetValue(colIndex).ToString().Trim();
                    if (string.IsNullOrEmpty(colName) || colName[0] == '(' || colName[0] == '（') {
                        header.Add(new ColumnInfo { IsIgnored = true });
                    }
                    else {
                        if (!IdReg.IsMatch(colName)) {
                            throw new InvalidOperationException(
                                $"{path}({sheetName}) has invalid property name '{colName}'.");
                        }
                        if (colNameSet.Contains(colName)) {
                            throw new InvalidOperationException(
                                $"{path}({sheetName}) has duplicated property '{colName}'.");
                        }
                        colNameSet.Add(colName);

                        header.Add(new ColumnInfo { ColIndex = colIndex, Name = colName });
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
                    colInfo.ConfigTypeName = reader.GetValue(colIndex).ToString();
                    try {
                        colInfo.Converter = converters.GetConverter(colInfo.ConfigTypeName);
                    }
                    catch (Exception exc) {
                        throw new InvalidDataException($"{path}({sheetName}): Cannot create converter for type {colInfo.Name}", exc);
                    }
                }
            }

            // Flag row
            if (!reader.ReadNextRow()) {
                throw new InvalidDataException($"{path}({sheetName}) has no flag row");
            }
            for (var colIndex = 0; colIndex < reader.CellCount; ++colIndex) {
                var colInfo = header[colIndex];
                var colDebugName = $"{path}({sheetName}) '{colInfo.Name}'";
                if (colInfo.IsIgnored || reader.IsNull(colIndex)) {
                    continue;
                }
                var flagKey = false;
                var flagIgnore = false;
                var flagDefault = false;
                var flagSeparator = false;
                foreach (var flag in EscapableSplitString(reader.GetValue(colIndex).ToString(), '|')) {
                    var trimmed = flag.Trim();
                    if (string.IsNullOrEmpty(trimmed)) {
                        continue;
                    }
                    if (string.Equals(trimmed, "key", StringComparison.OrdinalIgnoreCase)) {
                        if (flagKey) {
                            throw new InvalidDataException($"{colDebugName} has duplicated 'key' flag");
                        }
                        flagKey = true;
                        if (!colInfo.Converter.CanBeKey) {
                            throw new InvalidDataException($"{colDebugName} has invalid key type '{colInfo.Converter.Type.Name}'");
                        }
                        colInfo.IsKey = true;
                        colInfo.Keys = new HashSet<object>();
                    }
                    else if (string.Equals(trimmed, "ignore", StringComparison.OrdinalIgnoreCase)) {
                        if (flagIgnore) {
                            throw new InvalidDataException($"{colDebugName} has duplicated 'ignore' flag");
                        }
                        flagIgnore = true;
                        colInfo.IsIgnored = true;
                    }
                    else if (trimmed.StartsWith("info:", StringComparison.OrdinalIgnoreCase)) {
                        var rest = trimmed.Substring("info:".Length);
                        if (!Enum.TryParse<InfoType>(rest, true, out var value)) {
                            throw new InvalidDataException($"{colDebugName} has invalid info type '{rest}'");
                        }
                        colInfo.Info |= value;
                    }
                    else if (trimmed.StartsWith("ref:", StringComparison.OrdinalIgnoreCase)) {
                        Debug.LogWarning($"Deprecated 'ref' flag for {colDebugName}");
                    }
                    else if (trimmed.StartsWith("elem-ref:", StringComparison.OrdinalIgnoreCase)) {
                        Debug.LogWarning($"Deprecated 'elem-ref' flag for {colDebugName}");
                    }
                    else if (trimmed.StartsWith("default:", StringComparison.OrdinalIgnoreCase)) {
                        if (flagDefault) {
                            throw new InvalidDataException($"{colDebugName} has duplicated 'default' flag");
                        }
                        flagDefault = true;
                        var rest = trimmed.Substring("default:".Length);
                        try {
                            colInfo.DefaultValue = colInfo.Converter.Convert(rest);
                        }
                        catch (Exception exc) {
                            throw new InvalidDataException(
                                $"{colDebugName} default value ({rest}) cannot be converted to {colInfo.Converter.TypeName}: {exc.Message}", exc);
                        }
                    }
                    else if (trimmed.StartsWith("separator:", StringComparison.OrdinalIgnoreCase)) {
                        if (flagSeparator) {
                            throw new InvalidDataException($"{colDebugName} has duplicated 'separator' flag");
                        }
                        if (!colInfo.Converter.HasSeparator) {
                            throw new InvalidDataException($"{colDebugName} invalid 'separator' flag for type '{colInfo.Converter.TypeName}'");
                        }
                        flagSeparator = true;
                        var rest = trimmed.Substring("separator:".Length);
                        colInfo.Converter.Separator = rest;
                    }
                    else if (trimmed.Equals("escape", StringComparison.OrdinalIgnoreCase)) {
                        colInfo.AllowEscape = true;
                    }
                    else {
                        throw new InvalidDataException($"{colDebugName} has unknown flag '{trimmed}'");
                    }
                }
                if (colInfo.IsKey && colInfo.Info != InfoType.None) {
                    throw new InvalidDataException($"{colDebugName} is key AND info");
                }
                if (colInfo.IsKey && colInfo.IsIgnored) {
                    throw new InvalidDataException($"{colDebugName} is key AND ignore");
                }
                if (colInfo.Info != InfoType.None && colInfo.IsIgnored) {
                    throw new InvalidDataException($"{colDebugName} is info AND ignore");
                }
            }
            
            // Densify
            header.RemoveAll(info => info.IsIgnored);

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
                foreach (var columnInfo in header) {
                    if (reader.IsNull(columnInfo.ColIndex) && columnInfo.DefaultValue is {} v) {
                        currentRow[index] = v;
                    }
                    else {
                        var raw = reader.IsNull(columnInfo.ColIndex) ? null : reader.GetValue(columnInfo.ColIndex).ToString();
                        if (columnInfo.AllowEscape) {
                            raw = UnescapeString(raw);
                        }
                        var converter = columnInfo.Converter;
                        object result;
                        try {
                            result = converter.Convert(raw);
                            System.Diagnostics.Debug.Assert(result == null || result.GetType() == converter.Type);
                        }
                        catch (Exception exc) {
                            throw new InvalidDataException(
                                $"{path}({sheetName})(row: {rowIndex + 1}, col: {columnInfo.ColIndex + 1}): cannot parse data '{raw ?? "null"}' to {converter.TypeName}: {exc.Message}");
                        }
                        if (columnInfo.Keys is {} set) {
                            if (set.Contains(result)) {
                                throw new InvalidDataException(
                                    $"{path}({sheetName})(row: {rowIndex + 1}, col: {columnInfo.ColIndex + 1}): duplicated key '{result}'");
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
            for (var i = 0 ; i < min; ++i) {
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
        private static List<SheetData> InternalReadSheets(ISheetValueConverterCollection converters, IEnumerable<string> paths, bool headerOnly)
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
                    if (!IdReg.IsMatch(className)) {
                        throw new InvalidOperationException($"{className} is not valid identifier name.");
                    }
                    var newSheetData = ReadSheet(converters, reader, path, headerOnly);
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
                    var lowerNameChars = colInfo.Name.ToCharArray();
                    for (var i = 0; i < lowerNameChars.Length && char.IsUpper(lowerNameChars[i]); ++i) {
                        lowerNameChars[i] = char.ToLower(lowerNameChars[i]);
                    }
                    var columnInfo = new ColumnInfo {
                        ColIndex = colIndex++,
                        Name = colInfo.Name,
                        LowerCamelName = new string(lowerNameChars),
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

        public static List<SheetData> ReadAllSheets(ISheetValueConverterCollection converters, IEnumerable<string> paths)
        {
            return InternalReadSheets(converters, paths, false);
        }

        public static List<SheetData> ReadAllHeaders(ISheetValueConverterCollection converters, IEnumerable<string> paths)
        {
            return InternalReadSheets(converters, paths, true);
        }
    }
}
