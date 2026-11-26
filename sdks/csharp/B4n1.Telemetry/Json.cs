using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace B4n1.Telemetry
{
    /// <summary>
    /// Minimal JSON encoder for the SDK's own output.
    ///
    /// <para>Hand-rolling the encoder is what lets the library target
    /// netstandard2.0 with zero package references: an observability client
    /// that forces a JSON package on every consumer is a dependency the user
    /// did not ask for.</para>
    /// </summary>
    public static class Json
    {
        /// <summary>Encode a dictionary, list, string, number, bool or null.</summary>
        public static string Encode(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }

        /// <summary>One JSON array of envelopes — the collector's batch body.</summary>
        public static string EncodeBatch(IList<IDictionary<string, object>> envelopes)
        {
            var builder = new StringBuilder("[");
            for (var i = 0; i < envelopes.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                Write(builder, envelopes[i]);
            }

            return builder.Append(']').ToString();
        }

        private static void Write(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string text)
            {
                Quote(builder, text);
                return;
            }

            if (value is bool flag)
            {
                builder.Append(flag ? "true" : "false");
                return;
            }

            if (value is IDictionary<string, object> map)
            {
                WriteMap(builder, map);
                return;
            }

            if (value is IDictionary<string, string> strings)
            {
                var converted = new Dictionary<string, object>();
                foreach (var pair in strings)
                {
                    converted[pair.Key] = pair.Value;
                }

                WriteMap(builder, converted);
                return;
            }

            if (value is IEnumerable list && !(value is string))
            {
                WriteList(builder, list);
                return;
            }

            if (value is IFormattable formattable)
            {
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            }

            Quote(builder, value.ToString());
        }

        private static void WriteMap(StringBuilder builder, IDictionary<string, object> map)
        {
            builder.Append('{');
            var first = true;
            foreach (var pair in map)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                Quote(builder, pair.Key);
                builder.Append(':');
                Write(builder, pair.Value);
            }

            builder.Append('}');
        }

        private static void WriteList(StringBuilder builder, IEnumerable items)
        {
            builder.Append('[');
            var first = true;
            foreach (var item in items)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                Write(builder, item);
            }

            builder.Append(']');
        }

        private static void Quote(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
