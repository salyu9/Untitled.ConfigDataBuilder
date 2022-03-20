using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Untitled.ConfigDataBuilder.Base;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal sealed class ConverterInfo
    {
        public string ConverterTypeName { get; }
        public string VariableName { get; }

        public ConverterInfo(string converterTypeName, string variableName)
            => (ConverterTypeName, VariableName) = (converterTypeName, variableName);
    }

    /// <summary>
    /// Wrapper for all config type converters. <br />
    /// Inner converter can be: <br />
    ///   - IConfigValueConverter <br />
    ///   - IMultiSegConfigValueConverter <br />
    ///   - EnumConverter <br />
    ///   - NullableConverter <br />
    ///   - ArrayConverter <br />
    ///   - DictionaryConverter <br />
    /// </summary>
    internal interface ISheetValueConverter
    {
        string TypeName { get; }
        Type Type { get; }
        int SeparatorLevel { get; }
        bool TryCreateDefaultSeparators(out string separators);
        object ParseEscaped(string rawValue, string separators);
        string ReadBinaryExp(string readerVarName);
        void WriteBinary(BinaryWriter writer, object value);
        string ToStringExp(string varName);
    }
}
