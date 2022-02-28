using System.Collections.Generic;

namespace Untitled.ConfigDataBuilder.Editor
{
    /// <summary>
    /// Information about a column in sheet data.
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// The column index of this column.
        /// </summary>
        public int ColIndex { get; internal set; }

        /// <summary>
        /// The name of this column.
        /// </summary>
        public string Name { get; internal set; }

        internal string LowerCamelName { get; set; }

        public bool IsKey { get; internal set; }

        internal HashSet<object> Keys { get; set; }

        internal SheetData.RefInfo Ref { get; set; }

        public bool IsIgnored { get; set; }

        public InfoType Info { get; set; }

        public string ConfigTypeName { get; set; }

        internal SheetValueConverter Converter { get; set; }

        public object DefaultValue { get; set; }

        public bool AllowEscape { get; set; }
    }
}
