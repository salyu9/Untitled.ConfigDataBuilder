using System;
using System.Collections.Generic;
using System.Text;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal static class Helper
    {
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

        /// <summary>
        /// Unescape string.
        /// </summary>
        public static string UnescapeString(string input)
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

        /// <summary>
        /// Split input by separator, and unescape segments.
        /// </summary>
        public static IEnumerable<string> SplitAndUnescapeString(string input, char separator)
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
            if (builder.Length == 0) {
                yield break;
            }
            var tail = builder.ToString();
            if (!string.IsNullOrWhiteSpace(tail)) {
                yield return tail;
            }
        }

        /// <summary>
        /// Split input by separator, keep segments escaped.
        /// </summary>
        public static IEnumerable<string> SplitEscapedString(string input, char separator)
        {
            var i = 0;
            var len = input.Length;
            var escaping = false;
            var builder = new StringBuilder();
            while (i < len) {
                var c = input[i];

                if (escaping) {
                    builder.Append(c);
                    escaping = false;
                }
                else if (c == '\\') {
                    escaping = true;
                    builder.Append('\\');
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
            if (builder.Length == 0) {
                yield break;
            }
            var tail = builder.ToString();
            if (!string.IsNullOrWhiteSpace(tail)) {
                yield return tail;
            }
        }
    }
}
