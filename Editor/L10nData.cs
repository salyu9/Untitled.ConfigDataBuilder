namespace Untitled.ConfigDataBuilder.Editor
{
    public sealed class L10nProperty
    {
        public string Name;

        /// <summary>
        /// string or string[]
        /// </summary>
        public object Value;
    }

    public sealed class L10nRow
    {
        public string Key;
        public L10nProperty[] Properties;
    }

    public sealed class L10nData
    {
        public string SheetName;

        public L10nRow[] Rows;
    }
}
