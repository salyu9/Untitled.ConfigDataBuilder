using System;
using System.Text;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class IndentedStringBuilder
    {
        private readonly StringBuilder _builder = new StringBuilder();

        private const string Indentation = "    ";

        public IndentedStringBuilder Indent()
        {
            ++_indentLevel;
            return this;
        }

        public IndentedStringBuilder Dedent()
        {
            if (_indentLevel > 0) --_indentLevel;
            return this;
        }

        public string NewLine { get; set; } = Environment.NewLine;

        private int _indentLevel;
        private bool _pendingIndent;

        private void DoIndent()
        {
            if (_pendingIndent) {
                for (var i = 0; i < _indentLevel; ++i) _builder.Append(Indentation);
                _pendingIndent = false;
            }
        }

        public override string ToString()
            => _builder.ToString();

        public IndentedStringBuilder Append(string value)
        {
            if (value.Length > 0) {
                DoIndent();
                _builder.Append(value);
            }
            return this;
        }

        public IndentedStringBuilder AppendLine()
        {
            AppendLine(string.Empty);
            return this;
        }

        public IndentedStringBuilder AppendLine(string value)
        {
            if (value.Length > 0) {
                DoIndent();
                _builder.Append(value);
            }
            _builder.Append(NewLine);
            _pendingIndent = true;
            return this;
        }
    }
}
