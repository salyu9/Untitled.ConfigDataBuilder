using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal sealed class FodsSheetReader : ISheetReader
    {
        private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

        private readonly XmlReader _reader;
        private string _currentName;
        private object[] _currentRow;
        private int _currentRowRepeat;
        private int _rowGroupLevel;

        public FodsSheetReader(string path)
        {
            // _reader = new XmlTextReader(path, File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
            //     WhitespaceHandling = WhitespaceHandling.None
            // };
            _reader = XmlReader.Create(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                new XmlReaderSettings {
                    CloseInput = true,
                    IgnoreWhitespace = true,
                    IgnoreComments = true,
                    
                }, path);
            try {
                if (!FindElement(Office + "document")) {
                    throw new InvalidDataException($"{GetPositionInfo()}: <office:document> not found");
                }
                _reader.ReadStartElement();
                if (!FindElement(Office + "body")) {
                    throw new InvalidDataException($"{GetPositionInfo()}: <office:body> not found");
                }
                _reader.ReadStartElement();
                if (!FindElement(Office + "spreadsheet")) {
                    throw new InvalidDataException($"{GetPositionInfo()}: <office:spreadsheet> not found");
                }
                _reader.ReadStartElement();
                if (!ReadNextSheet()) {
                    throw new InvalidDataException($"{GetPositionInfo()}: <table:table> not found");
                }
            }
            catch {
                _reader.Dispose();
                throw;
            }
        }

        public string SheetName =>
            _currentName ?? throw new InvalidOperationException("Cannot read row while no sheets read, previous operation has failed");

        public bool ReadNextSheet()
        {
            if (_currentName != null) {
                if (!ExitElement(Table + "table")) {
                    return false;
                }
                _currentName = null;
                _currentRow = null;
            }
            _rowGroupLevel = 1;
            while (FindElement(Table + "table")) {
                var name = _reader.GetAttribute("name", Table.NamespaceName);
                _reader.ReadStartElement();
                if (_reader.IsStartElement("table-source", Table.NamespaceName)) {
                    ExitElement(Table + "table");
                    continue;
                }

                _currentName = name ?? throw new InvalidDataException($"{GetPositionInfo()}: Table has no table:name attribute");
                return true;
            }

            return false;
        }

        public bool ReadNextRow()
        {
            if (_currentRowRepeat > 0) {
                --_currentRowRepeat;
                return true;
            }
            _currentRow = null;
            if (_currentName == null) {
                throw new InvalidOperationException("Cannot read row while no sheets read, previous operation has failed");
            }
            while (!_reader.EOF) {
                if (_reader.IsStartElement("table-row", Table.NamespaceName)) {
                    ReadSingleRow();
                    return true;
                }
                if (_reader.IsStartElement("table-row-group", Table.NamespaceName)) {
                    // enter child
                    _reader.ReadStartElement();
                    ++_rowGroupLevel;
                }
                else if (_reader.NodeType == XmlNodeType.EndElement) {
                    // return to parent level
                    --_rowGroupLevel;
                    if (_rowGroupLevel == 0) {
                        return false;
                    }
                    _reader.ReadEndElement();
                }
                else {
                    _reader.Skip();
                }
            }
            return false;
        }

        private void ReadSingleRow()
        {
            if (_reader.MoveToAttribute("number-rows-repeated", Table.NamespaceName)) {
                _currentRowRepeat = int.Parse(_reader.Value) - 1;
                _reader.MoveToElement();
            }
            _reader.ReadStartElement();
            var data = new List<object>();
            while (FindElement(Table + "table-cell")) {
                var elem = (XElement)XNode.ReadFrom(_reader);
                elem.Elements().Where(e => e.Name == Office + "annotation").Remove();
                var repeatAttr = elem.Attribute(Table + "number-columns-repeated");
                var repeat = repeatAttr != null ? int.Parse(repeatAttr.Value) : 1;
                object cellValue;
                if (elem.IsEmpty) {
                    cellValue = null;
                }
                else {
                    var type = elem.Attribute(Office + "value-type");
                    if (type is null) {
                        throw new InvalidDataException($"{GetPositionInfo()}: value type of '{elem}' not found)");
                    }
                    cellValue = type.Value switch {
                        "string"  => GetStringFromXElement(elem),
                        "float"   => double.Parse(GetStringFromXElement(elem)),
                        "boolean" => bool.Parse(GetStringFromXElement(elem)),
                        _         => throw new InvalidDataException($"{GetPositionInfo()}: Unknown type '{type}'")
                    };
                }
                data.AddRange(Enumerable.Repeat(cellValue, repeat));
            }
            _reader.ReadEndElement();
            _currentRow = data.ToArray();
        }

        private string GetStringFromXElement(XElement element)
        {
            var paragraphs = new List<string>();
            foreach (var p in element.Elements(Text + "p")) {
                if (p.HasElements) {
                    var span = new List<string>();
                    foreach (var node in p.Nodes()) {
                        switch (node) {
                            case XText text: {
                                span.Add(text.Value);
                                break;
                            }
                            case XElement elem: {
                                if (elem.Name == Text + "span") {
                                    span.Add(elem.Value);
                                }
                                else if (elem.Name == Text + "s") {
                                    var countAttr = elem.Attribute(Text + "c");
                                    var count = countAttr != null ? int.Parse(countAttr.Value) : 1;
                                    span.Add(new string(' ', count));
                                }
                                else if (elem.Name == Text + "a") {
                                    span.Add(elem.Value);
                                }
                                else {
                                    throw new InvalidDataException($"{GetPositionInfo()}: unknown tag '{elem.Name}'");
                                }
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    paragraphs.Add(string.Concat(span));
                }
                else {
                    paragraphs.Add(p.Value);
                }
            }
            return string.Join("\n", paragraphs);
        }

        private bool FindElement(XName name)
        {
            while (!_reader.EOF) {
                if (_reader.IsStartElement(name.LocalName, name.NamespaceName)) {
                    return true;
                }
                if (_reader.NodeType == XmlNodeType.EndElement) {
                    return false;
                }
                _reader.Skip();
            }
            return false;
        }

        private bool ExitElement(XName name)
        {
            while (!_reader.EOF) {
                if (_reader.NodeType == XmlNodeType.EndElement) {
                    if (_reader.LocalName == name.LocalName && _reader.NamespaceURI == name.NamespaceName) {
                        _reader.ReadEndElement();
                        return true;
                    }
                    _reader.ReadEndElement();
                }
                else {
                    _reader.Skip();
                }
            }
            return false;
        }

        private string GetPositionInfo()
        {
            if (_reader is IXmlLineInfo lineInfo) {
                return $"{_reader.BaseURI}({lineInfo.LineNumber}, {lineInfo.LinePosition})";
            }
            return $"{_reader.BaseURI}";
        }

        public int CellCount => _currentRow.Length;

        public bool IsNull(int index) => _currentRow[index] is null;

        public object GetValue(int index) => _currentRow[index];

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
