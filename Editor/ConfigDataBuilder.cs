using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    public static class ConfigDataBuilder
    {
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false, true);

        private const string BaseLibAssemblyName = "Untitled.ConfigDataBuilder.Base";
        private const string BaseLibNamespace = "Untitled.ConfigDataBuilder.Base";

        private static ConfigDataBuilderSettings GetSettings()
        {
            return ConfigDataBuilderSettings.GetOrCreateSettings();
        }

        private static string GetAssemblyName(ConfigDataBuilderSettings settings)
        {
            return Path.GetFileNameWithoutExtension(settings.assemblyOutputPath);
        }

        private static IEnumerable<string> EnumerateSheetFiles()
        {
            var set = new HashSet<string>();

            foreach (var file in Directory.EnumerateFiles(GetSettings().sourceFolder)) {
                var filename = Path.GetFileNameWithoutExtension(file);
                if (filename.StartsWith("~", StringComparison.Ordinal)) {
                    continue;
                }
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".xlsx" && ext != ".fods") {
                    continue;
                }
                if (set.Add(filename)) {
                    yield return file;
                }
            }
        }

        [MenuItem("Tools/Config Data/Rebuild Config", false, 101)]
        public static void RebuildConfig()
        {
            if (CheckDll(false)) {
                Debug.Log("Config dll type matches, skipped.");
            }
            else {
                ForceRebuildConfig();
            }
        }

        [MenuItem("Tools/Config Data/Force Rebuild Config", false, 102)]
        public static void ForceRebuildConfig()
        {
            if (InternalBuildConfig()) {
                InternalReimportData();
                Debug.Log("Config data reimported.");
            }
        }

        /// <summary>
        /// Reimport config data to resources.
        /// </summary>
        /// <returns>If reimport succeeded, returns true, otherwise returns false.</returns>
        [MenuItem("Tools/Config Data/Reimport Data", false, 201)]
        public static void Menu_ReimportData()
        {
            try {
                EditorUtility.DisplayProgressBar("Info", "Reimporting config data", 0);
                ReimportData();
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Config Data/Dump", false, 301)]
        public static void DumpSources()
        {
            var settings = GetSettings();
            try {
                var path = FileUtil.GetUniqueTempPathInProject();

                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                var converters = SheetValueConverter.GetSheetValueConverters();
                var sheets = SheetData.ReadAllSheets(converters, EnumerateSheetFiles());
                var classNames = new List<string>();
                foreach (var sheet in sheets) {
                    classNames.Add(sheet.ClassName);
                    File.WriteAllText(Path.Combine(path, sheet.ClassName + ".cs"), GenerateConfigClassContent(settings, sheet));
                }
                File.WriteAllText(Path.Combine(path, "ConfigDataManager.cs"), GenerateManagerClassContent(converters, settings, classNames));

                EditorUtility.RevealInFinder(Path.Combine(path, "ConfigDataManager.cs"));
            }
            catch (Exception e) {
                Debug.LogError($"Dump {GetSettings().assemblyOutputPath} failed");
                Debug.LogException(e);
            }
        }

        [MenuItem("Tools/Config Data/Runtime Reload", false, 302)]
        public static void RuntimeReload()
        {
            if (Application.isPlaying) {
                var settings = GetSettings();
                var asm = AppDomain.CurrentDomain
                    .GetAssemblies().FirstOrDefault(a => a.GetName().Name == GetAssemblyName(settings));
                if (asm == null) {
                    Debug.Log($"Cannot find {GetAssemblyName(settings)} at runtime");
                }
                else {
                    var method = asm.GetType($"{settings.assemblyNamespace}.ConfigDataManager").GetMethod("Reload");
                    System.Diagnostics.Debug.Assert(method != null, nameof(method) + " != null");
                    method.Invoke(null, null);
                    Debug.Log("Config data reloaded.");
                }
            }
        }

        public static bool ReimportData()
        {
            if (!CheckDll(true)) {
                Debug.LogWarning("Config dll types mismatch. Rebuilding config required.");
                return false;
            }
            InternalReimportData();
            Debug.Log("Config data reimported.");
            return true;
        }

        [InitializeOnEnterPlayMode]
        public static void EnterPlayModeCheck()
        {
            if (!CheckDll(true)) {
                Debug.LogWarning("Config dll types mismatch. Rebuilding config required.");
            }
        }

        private static bool CheckDll(bool logDifference)
        {
            var files = EnumerateSheetFiles().ToList();
            var settings = GetSettings();
            var converters = SheetValueConverter.GetSheetValueConverters();

            if (!File.Exists(settings.assemblyOutputPath)) {
                return files.Count == 0;
            }

            Dictionary<string, Type> typeTable;
            try {
                var assembly = Assembly.LoadFile(Path.GetFullPath(settings.assemblyOutputPath));
                typeTable = assembly.GetExportedTypes()
                    .Where(type => type.Name.EndsWith("Config", StringComparison.Ordinal))
                    .ToDictionary(type => type.Name);
            }
            catch (Exception exc) {
                Debug.LogError($"Load {settings.assemblyOutputPath} failed: {exc}");
                return false;
            }
            List<SheetData> headerInfoTable;
            try {
                headerInfoTable = SheetData.ReadAllHeaders(converters, files);
            }
            catch (Exception exc) {
                if (logDifference) {
                    Debug.LogError("Read config data failed");
                    Debug.LogException(exc);
                }
                return false;
            }

            foreach (var headerInfo in headerInfoTable) {
                if (!typeTable.TryGetValue(headerInfo.ClassName, out var type)) {
                    if (logDifference) {
                        Debug.Log($"Type {headerInfo.ClassName} missing in dll.");
                    }
                    return false;
                }
                var properties =
                    type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                var columns = headerInfo.Header.Where(col => col.Info == InfoType.None).ToArray();
                if (properties.Length != columns.Length) {
                    if (logDifference) {
                        Debug.Log(
                            $"Type {headerInfo.ClassName} property count (property[{properties.Length}] / column[{columns.Length}]) not match.");
                    }
                    return false;
                }
                foreach (var (p, c) in properties.Zip(columns, (p, c) => (p, c))) {
                    if (p.Name != c.Name) {
                        if (logDifference) {
                            Debug.Log($"{headerInfo.ClassName}.{p.Name} type not match column {c.Name}.");
                        }
                        return false;
                    }
                    if (p.PropertyType != c.Converter.Type) {
                        if (logDifference) {
                            Debug.Log(
                                $"{headerInfo.ClassName}.{p.Name} type not match ({p.PropertyType} / {c.Converter.Type}).");
                        }
                        return false;
                    }
                }
                typeTable.Remove(headerInfo.ClassName);
            }
            if (typeTable.Count > 0) {
                if (logDifference) {
                    Debug.Log($"Dll contains deleted config type: [{string.Join(", ", typeTable.Keys)}] .");
                }
                return false;
            }
            return true;
        }

        private static string GenerateConfigClassContent(ConfigDataBuilderSettings settings, SheetData sheet)
        {
            var resourcesPath = settings.dataOutputFolder.Replace("\\", "/");
            if (resourcesPath.Last() != '/') {
                resourcesPath += "/";
            }
            var builder = new IndentedStringBuilder { NewLine = "\n" };
            builder.AppendLine("using System.Linq;");
            builder.Append("using ").Append(BaseLibNamespace).AppendLine(";");
            builder.AppendLine();
            builder.AppendLine($"namespace {settings.assemblyNamespace}");
            builder.IndentWithOpenBrace();
            {
                builder.AppendLine($"public sealed class {sheet.ClassName}");
                builder.IndentWithOpenBrace();
                {
                    // properties
                    foreach (var col in sheet.Header) {
                        if (col.Info != InfoType.None) continue;
                        builder.AppendLine($"public {col.Converter.TypeName} {col.Name} {{ get; private set; }}");
                        builder.AppendLine();
                    }

                    builder.AppendLine($"private static {sheet.ClassName}[] _data = System.Array.Empty<{sheet.ClassName}>();");
                    builder.AppendLine();
                    builder.AppendLine($"public static System.Collections.Generic.IReadOnlyList<{sheet.ClassName}> AllConfig()");
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine("ConfigDataManager.Initialize();");
                        builder.AppendLine("return System.Array.AsReadOnly(_data);");
                    }
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();

                    // keys
                    foreach (var key in sheet.Keys) {
                        builder.AppendLine(
                            $"private static System.Collections.Generic.Dictionary<{key.Converter.TypeName}, {sheet.ClassName}> _{key.LowerCamelName}Table");
                        builder.Indent().AppendLine($"= _data.ToDictionary(elem => elem.{key.Name});").Dedent();
                        builder.AppendLine();
                        builder.AppendLine($"public static {sheet.ClassName} From{key.Name}({key.Converter.TypeName} {key.LowerCamelName})");
                        builder.IndentWithOpenBrace();
                        {
                            builder.AppendLine("ConfigDataManager.Initialize();");
                            builder.AppendLine(
                                $"return _{key.LowerCamelName}Table.TryGetValue({key.LowerCamelName}, out var result) ? result : null;");
                        }
                        builder.DedentWithCloseBrace();
                        builder.AppendLine();
                    }

                    // to-string
                    builder.AppendLine("public override string ToString()");
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine($"return \"{{{sheet.ClassName} \"");
                        var first = true;
                        foreach (var col in sheet.Header) {
                            if (col.Info != InfoType.None) continue;
                            if (first) {
                                first = false;
                                builder.Append("  + \"");
                            }
                            else {
                                builder.Append("  + \", ");
                            }
                            builder.Append($"{col.Name}=\" + ");
                            builder.AppendLine(col.Converter.ToStringExp(col.Name));
                        }
                        builder.AppendLine("  + \"}\";");
                    }
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();

                    // read-binary
                    builder.AppendLine($"private static {sheet.ClassName}[] ReadBinary(byte[] bytes)");
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine("using (var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(bytes))) ")
                            .IndentWithOpenBrace();
                        {
                            builder.AppendLine($"var result = new {sheet.ClassName}[reader.ReadInt32()];");
                            builder.AppendLine("for (var i = 0; i < result.Length; ++i) ");
                            builder.IndentWithOpenBrace();
                            {
                                builder.AppendLine($"result[i] = new {sheet.ClassName} {{");
                                builder.Indent();
                                foreach (var col in sheet.Header) {
                                    if (col.Info != InfoType.None) continue;
                                    builder.AppendLine($"{col.Name} = {col.Converter.ReadBinaryExp("reader")},");
                                }
                                builder.Dedent();
                                builder.AppendLine("};");
                            }
                        }
                        builder.DedentWithCloseBrace();
                        builder.AppendLine("return result;");
                        builder.DedentWithCloseBrace();
                    }
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();

                    // reload
                    builder.AppendLine("internal static void Reload()");
                    builder.IndentWithOpenBrace();
                    builder.AppendLine(
                        $"var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(\"{resourcesPath}{sheet.ClassName}\");");
                    builder.AppendLine("if (asset != null)");
                    builder.IndentWithOpenBrace();
                    builder.AppendLine("try");
                    builder.IndentWithOpenBrace();
                    builder.AppendLine("_data = ReadBinary(asset.bytes);");
                    builder.DedentWithCloseBrace();
                    builder.AppendLine("catch (System.Exception e)");
                    builder.IndentWithOpenBrace();
                    builder.AppendLine(
                        $"UnityEngine.Debug.LogError($\"Failed to load {sheet.ClassName} data: {{e.Message}}, try reimport config data\");");
                    builder.DedentWithCloseBrace();
                    builder.AppendLine("UnityEngine.Resources.UnloadAsset(asset);");
                    builder.DedentWithCloseBrace();
                    builder.AppendLine("else");
                    builder.IndentWithOpenBrace();
                    builder.AppendLine(
                        @$"UnityEngine.Debug.LogError(""Cannot load config data of '{sheet.ClassName}', please reimport config data"");");
                    builder.AppendLine($"_data = System.Array.Empty<{sheet.ClassName}>();");
                    builder.DedentWithCloseBrace();
                    foreach (var key in sheet.Keys) {
                        builder.Append($"_{key.LowerCamelName}Table = _data.ToDictionary(elem => elem.{key.Name});");
                    }
                    builder.AppendLine();
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();
                }
                builder.DedentWithCloseBrace();
            }
            builder.DedentWithCloseBrace();

            return builder.ToString();
        }

        private static string GenerateManagerClassContent(ISheetValueConverterCollection converters, ConfigDataBuilderSettings settings,
            IEnumerable<string> classNames)
        {
            var builder = new IndentedStringBuilder();

            builder.AppendLine($"namespace {settings.assemblyNamespace}");
            builder.IndentWithOpenBrace();
            {
                builder.AppendLine("public static class ConfigDataManager");
                builder.IndentWithOpenBrace();
                {
                    builder.AppendLine("private static bool _initialized;");
                    builder.AppendLine();
                    foreach (var info in converters.EnumerateConverterInfo()) {
                        builder.Append("internal static readonly ").Append(info.ConverterTypeName).Append(" ").AppendLine(info.VariableName);
                        builder.Indent().Append("= new ").Append(info.ConverterTypeName).Append("();").Dedent().AppendLine();
                    }
                    builder.AppendLine("public static void Reload()");
                    builder.IndentWithOpenBrace();
                    {
                        foreach (var name in classNames) {
                            builder.AppendLine($"{name}.Reload();");
                        }
                    }
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();

                    builder.AppendLine(
                        "internal static T[] ReadArray<T>(System.IO.BinaryReader reader, System.Func<System.IO.BinaryReader, T> readFunc)");
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine("var size = reader.ReadInt32();");
                        builder.AppendLine("var result = new T[size];");
                        builder.AppendLine("for (var i = 0; i < size; ++i)");
                        builder.IndentWithOpenBrace();
                        {
                            builder.AppendLine("result[i] = readFunc(reader);");
                        }
                        builder.DedentWithCloseBrace();
                        builder.AppendLine("return result;");
                    }
                    builder.DedentWithCloseBrace();
                    builder.AppendLine();

                    builder.AppendLine("internal static System.Collections.Generic.IReadOnlyDictionary<TKey, TValue> ReadDictionary<TKey, TValue>(");
                    builder.Indent();
                    builder.AppendLine("System.IO.BinaryReader reader,");
                    builder.AppendLine("System.Func<System.IO.BinaryReader, TKey> readKeyFunc,");
                    builder.AppendLine("System.Func<System.IO.BinaryReader, TValue> readValueFunc)");
                    builder.Dedent();
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine("var size = reader.ReadInt32();");
                        builder.AppendLine("var result = new System.Collections.Generic.Dictionary<TKey, TValue>();");
                        builder.AppendLine("for (var i = 0; i < size; ++i)");
                        builder.IndentWithOpenBrace();
                        {
                            builder.AppendLine("var key = readKeyFunc(reader);");
                            builder.AppendLine("var value = readValueFunc(reader);");
                            builder.AppendLine("result.Add(key, value);");
                        }
                        builder.DedentWithCloseBrace();
                        builder.AppendLine("return result;");
                    }
                    builder.DedentWithCloseBrace();

                    builder.AppendLine("public static void Initialize()");
                    builder.IndentWithOpenBrace();
                    {
                        builder.AppendLine("if (_initialized) return;");
                        builder.AppendLine("Reload();");
                        builder.AppendLine("_initialized = true;");
                    }
                    builder.DedentWithCloseBrace();
                }
                builder.DedentWithCloseBrace();
            }
            builder.DedentWithCloseBrace();

            return builder.ToString();
        }

        private static byte[] Compile(ConfigDataBuilderSettings settings, string folder, string[] fileNames)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .ToLookup(a => a.GetName().Name, a => a.Location)
                .ToDictionary(a => a.Key, a => a.First());

#if UNITY_2021_2_OR_NEWER
            var refAssemblies = new HashSet<string>(settings.importingAssemblies.Concat(settings.customTypesAssemblies)) {
                "netstandard",
                "System.Core",
                "UnityEngine.CoreModule",
                BaseLibAssemblyName,
            };
#elif UNITY_2019_4_OR_NEWER
            var refAssemblies = new HashSet<string>(settings.importingAssemblies.Concat(settings.customTypesAssemblies)) {
                "mscorlib",
                "netstandard",
                "System.Core",
                "UnityEngine.CoreModule",
                BaseLibAssemblyName,
            };
#else
            throw new InvalidOperationException($"Invalid Unity Version");
#endif

            var provider = CodeDomProvider.CreateProvider("C#");

            var refAsmLocations = new List<string>();
            foreach (var asmName in refAssemblies) {
                if (assemblies.TryGetValue(asmName, out var location)) {
                    refAsmLocations.Add(location);
                }
                else {
                    Debug.LogWarning($"Cannot find referenced assembly: {asmName}");
                }
            }

            var asmPath = folder + "/" + GetAssemblyName(settings) + ".dll";
            var options = new CompilerParameters(refAsmLocations.ToArray(), asmPath) {
                GenerateExecutable = false,
            };

            var result = provider.CompileAssemblyFromFile(options, fileNames);

            var errors = new List<string>();
            var warns = new List<string>();
            foreach (CompilerError v in result.Errors) {
                (v.IsWarning ? warns : errors).Add($"{v.ErrorNumber}({v.Line}: {v.Column}): {v.ErrorText}");
            }
            if (errors.Count > 0) {
                var builder = new StringBuilder("Compilation Failure: ").AppendLine();

                var lines = 0;
                foreach (var error in errors) {
                    ++lines;
                    if (lines < 10) {
                        builder.AppendLine("\t" + error);
                    }
                    else if (lines == 10) {
                        builder.AppendLine("...");
                    }
                }
                throw new InvalidDataException(builder.ToString());
            }
            foreach (var warn in warns) {
                Debug.LogWarning(warn);
            }

            return File.ReadAllBytes(asmPath);
        }

        private static bool InternalBuildConfig()
        {
            var settings = GetSettings();
            var folder = FileUtil.GetUniqueTempPathInProject();
            Directory.CreateDirectory(folder);
            try {
                EditorUtility.DisplayProgressBar("Info", "Building config", 0);
                var classNames = new List<string>();
                var fileNames = new List<string>();
                var converters = SheetValueConverter.GetSheetValueConverters();
                foreach (var sheet in SheetData.ReadAllHeaders(converters, EnumerateSheetFiles())) {
                    var fileName = folder + "/" + sheet.ClassName + ".cs";
                    File.WriteAllText(fileName, GenerateConfigClassContent(settings, sheet));
                    classNames.Add(sheet.ClassName);
                    fileNames.Add(fileName);
                }

                var managerFileName = folder + "/" + "ConfigDataManager.cs";
                File.WriteAllText(managerFileName, GenerateManagerClassContent(converters, settings, classNames));
                fileNames.Add(managerFileName);

                var bytes = Compile(settings, folder, fileNames.ToArray());
                File.WriteAllBytes(settings.assemblyOutputPath, bytes);
                Debug.Log($"Compile {settings.assemblyOutputPath} succeeded.");
                AssetDatabase.ImportAsset(settings.assemblyOutputPath);
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"Compile {settings.assemblyOutputPath} failed.");
                Debug.LogException(e);
                return false;
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void InternalReimportData()
        {
            var settings = GetSettings();
            var converters = SheetValueConverter.GetSheetValueConverters();
            var dataOutputPath = Path.Combine("Assets/Resources/", settings.dataOutputFolder).Replace("\\", "/");
            if (dataOutputPath.Length == 0) {
                Debug.LogError("Config 'Data Output Path' is not set properly.");
                return;
            }
            if (dataOutputPath.Last() != '/') {
                dataOutputPath += "/";
            }

            if (!Directory.Exists(dataOutputPath)) {
                Directory.CreateDirectory(dataOutputPath);
            }

            var sheetsDict = SheetData.ReadAllSheets(converters, EnumerateSheetFiles())
                .ToDictionary(sheet => sheet.MergedSheetName);

            // xref check
            foreach (var sheet in sheetsDict.Values) {
                for (var i = 0; i < sheet.Header.Length; ++i) {
                    var col = sheet.Header[i];
                    var refInfo = col.Ref;
                    if (refInfo == null) {
                        continue;
                    }
                    if (!sheetsDict.TryGetValue(refInfo.TableName, out var refSheet)) {
                        Debug.LogError(
                            $"Sheet {sheet.MergedSheetName} column '{col.Name}' refering sheet '{refInfo.TableName}' not exists");
                        continue;
                    }
                    var refCol = refSheet.Header.FirstOrDefault(c => c.Name == refInfo.ColumnName);
                    if (refCol == null) {
                        Debug.LogError(
                            $"Sheet {sheet.MergedSheetName} column '{col.Name}' refering column '{refInfo.TableName}.{refInfo.ColumnName}' not exists");
                        continue;
                    }
                    if (refCol.Keys == null) {
                        Debug.LogError(
                            $"Sheet {sheet.MergedSheetName} column '{col.Name}' refering column '{refInfo.TableName}.{refInfo.ColumnName}' is not key");
                        continue;
                    }
                    foreach (var row in sheet.Rows) {
                        var cell = row[i];
                        if (cell == null) {
                            continue;
                        }
                        if (refInfo.IsElem) {
                            var array = (Array)row[i];
                            foreach (var elem in array) {
                                if (elem == null) {
                                    continue;
                                }
                                var trimmed = refInfo.CanHavePlus && elem is string s && s.Last() == '+'
                                    ? s.Substring(0, s.Length - 1)
                                    : elem;
                                if (!refCol.Keys.Contains(trimmed)) {
                                    Debug.LogError(
                                        $"Sheet {sheet.MergedSheetName} column '{col.Name}' contains value '{elem}' that are not in referenced column '{refInfo.TableName}.{refInfo.ColumnName}'");
                                }
                            }
                        }
                        else {
                            var trimmed = refInfo.CanHavePlus && cell is string s && s.Last() == '+'
                                ? s.Substring(0, s.Length - 1)
                                : cell;
                            if (!refCol.Keys.Contains(trimmed)) {
                                Debug.LogError(
                                    $"Sheet {sheet.MergedSheetName} column '{col.Name}' contains value '{cell}' that are not in referenced column '{refInfo.TableName}.{refInfo.ColumnName}'");
                            }
                        }
                    }
                }
            }

            // generate output binary data
            foreach (var sheet in sheetsDict.Values) {
                var dataAssetPath = dataOutputPath + sheet.ClassName + ".bytes";
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, UTF8, true)) {
                    writer.Write(sheet.Rows.Length);
                    foreach (var row in sheet.Rows) {
                        for (var col = 0; col < sheet.Header.Length; ++col) {
                            var colInfo = sheet.Header[col];
                            if (colInfo.Info != InfoType.None) continue;
                            var cell = row[col];
                            colInfo.Converter.WriteBinary(writer, cell);
                        }
                    }
                }
                File.WriteAllBytes(dataAssetPath, stream.ToArray());
            }

            // export L10n
            if (!string.IsNullOrWhiteSpace(settings.l10nCustomExporterType)) {
                var l10nList = new List<L10nData>();
                foreach (var sheet in sheetsDict.Values) {
                    if (sheet.Keys.Length == 0) {
                        continue;
                    }
                    var l10nCols = new List<ColumnInfo>();
                    foreach (var col in sheet.Header.Where(h => h.Info.HasFlag(InfoType.L10n))) {
                        if (col.Converter.Type != typeof(string) && col.Converter.Type != typeof(string[])) {
                            Debug.LogError($"{sheet.ClassName}.{col.Name} has L10n info but is neither 'string' nor 'string[]'");
                            continue;
                        }
                        l10nCols.Add(col);
                    }
                    if (l10nCols.Count == 0) {
                        continue;
                    }
                    if (sheet.Keys.Length > 1) {
                        Debug.LogError(
                            $"{sheet.ClassName} has multiple keys and cannot be L10n source, but has L10n columns [{string.Join(", ", l10nCols.Select(c => c.Name))}]");
                        continue;
                    }
                    var keyCol = sheet.Keys[0];
                    if (!keyCol.Converter.IsScalar) {
                        Debug.LogError($"{sheet.ClassName} has non-scalar key and cannot be L10n source");
                        continue;
                    }

                    var l10nRows = new List<L10nRow>();

                    foreach (var row in sheet.Rows) {
                        var key = row[keyCol.ColIndex] is string s ? s : row[keyCol.ColIndex].ToString();
                        var values = new List<L10nProperty>();
                        foreach (var l10nCol in l10nCols) {
                            var rowKey = l10nCol.Name;

                            var obj = row[l10nCol.ColIndex];
                            values.Add(new L10nProperty { Name = rowKey, Value = obj });
                        }
                        l10nRows.Add(new L10nRow { Key = key, Properties = values.ToArray() });
                    }

                    l10nList.Add(new L10nData { SheetName = sheet.MergedSheetName, Rows = l10nRows.ToArray() });
                }
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    type = asm.GetType(settings.l10nCustomExporterType, false);
                    if (type != null) {
                        break;
                    }
                }
                if (type != null) {
                    try {
                        var exporter = (IL10nExporter)Activator.CreateInstance(type);
                        exporter.Export(l10nList);
                    }
                    catch (Exception exc) {
                        Debug.LogError("Failed to export localizations.");
                        Debug.LogException(exc);
                    }
                }
                else {
                    Debug.LogError($"Cannot find exporter type '{settings.l10nCustomExporterType}'");
                }
            }

            AssetDatabase.Refresh();
        }
    }

    internal static class IndentExtensions
    {
        public static void IndentWithOpenBrace(this IndentedStringBuilder builder)
        {
            builder.AppendLine("{");
            builder.Indent();
        }

        public static void DedentWithCloseBrace(this IndentedStringBuilder builder)
        {
            builder.Dedent();
            builder.AppendLine("}");
        }
    }

}
