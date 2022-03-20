using System.Collections.Generic;
using System.Diagnostics;

namespace Untitled.ConfigDataBuilder.Editor
{
    /// <summary>
    /// Information about a column in sheet data.
    /// </summary>
    [DebuggerDisplay("ColumnInfo ({ColIndex}): {Name}")]
    public class ColumnInfo
    {
        internal ISheetValueConverter Converter { get; set; }

        /// <summary>
        /// Get the debug log name of this column.
        /// </summary>
        public string DebugName { get; internal set; }

        /// <summary>
        /// Get the column index of this column.
        /// </summary>
        public int ColIndex { get; internal set; }

        /// <summary>
        /// Get the name of this column.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Get whether this column is treated as a key column.
        /// </summary>
        public bool IsKey { get; internal set; }

        internal HashSet<object> Keys { get; set; }

        /// <summary>
        /// Get or set whether this column is ignored.
        /// </summary>
        public bool IsIgnored { get; set; }

        /// <summary>
        /// Get or set the info type of this column.
        /// </summary>
        public InfoType Info { get; set; }

        /// <summary>
        /// Get or set the config type name of this column. <br />
        /// This name will be used to create the corresponding converter.
        /// </summary>
        public string ConfigTypeName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string RawDefaultValue { get; set; }

        /// <summary>
        /// Actual default value of this column. <br />
        /// If this value is set, <see cref="RawDefaultValue"/> will be ignored.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Separators used in multi-segment values.
        /// </summary>
        public string Separators { get; set; }
    }
}
