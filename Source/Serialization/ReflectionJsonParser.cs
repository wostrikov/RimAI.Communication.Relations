using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Ustas.RimAI.Communication.Relations.Serialization
{
internal sealed class ReflectionJsonParser
    {
        private readonly string _json;
        private int _index;

        internal ReflectionJsonParser(string json)
        {
            _json = json ?? string.Empty;
        }

        internal object Parse()
        {
            SkipWhitespace();
            object result = ParseValue();
            SkipWhitespace();
            if (_index != _json.Length)
            {
                throw new FormatException("Unexpected trailing JSON token.");
            }

            return result;
        }

        private object ParseValue()
        {
            SkipWhitespace();
            if (_index >= _json.Length)
            {
                throw new FormatException("Unexpected end of JSON input.");
            }

            char token = _json[_index];
            switch (token)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': return ParseLiteral("true", true);
                case 'f': return ParseLiteral("false", false);
                case 'n': return ParseLiteral("null", null);
                default: return ParseNumber();
            }
        }

        private Dictionary<string, object> ParseObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                object value = ParseValue();
                result[key] = value;
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ParseArray()
        {
            Expect('[');
            var result = new List<object>();
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var chars = new List<char>();
            while (_index < _json.Length)
            {
                char c = Read();
                if (c == '"')
                {
                    return new string(chars.ToArray());
                }

                if (c != '\\')
                {
                    chars.Add(c);
                    continue;
                }

                chars.Add(ReadEscapedChar());
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private char ReadEscapedChar()
        {
            if (_index >= _json.Length)
            {
                throw new FormatException("Invalid JSON escape sequence.");
            }

            char escape = Read();
            switch (escape)
            {
                case '"': return '"';
                case '\\': return '\\';
                case '/': return '/';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'u': return ReadUnicodeChar();
                default: throw new FormatException("Unsupported JSON escape.");
            }
        }

        private char ReadUnicodeChar()
        {
            if (_index + 4 > _json.Length)
            {
                throw new FormatException("Invalid unicode escape length.");
            }

            string code = _json.Substring(_index, 4);
            _index += 4;
            return (char)int.Parse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private object ParseNumber()
        {
            int start = _index;
            if (TryConsume('-'))
            {
                // Optional leading minus consumed.
            }

            ConsumeDigits();
            bool hasFraction = TryConsume('.');
            if (hasFraction)
            {
                ConsumeDigits();
            }

            bool hasExponent = TryConsume('e') || TryConsume('E');
            if (hasExponent)
            {
                TryConsume('+');
                TryConsume('-');
                ConsumeDigits();
            }

            string text = _json.Substring(start, _index - start);
            if (text.Length == 0)
            {
                throw new FormatException("Invalid JSON number.");
            }

            if (hasFraction || hasExponent)
            {
                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private object ParseLiteral(string token, object value)
        {
            if (!Match(token))
            {
                throw new FormatException($"Invalid JSON literal '{token}'.");
            }

            return value;
        }

        private void ConsumeDigits()
        {
            int start = _index;
            while (_index < _json.Length && char.IsDigit(_json[_index]))
            {
                _index++;
            }

            if (start == _index)
            {
                throw new FormatException("Expected numeric digits.");
            }
        }

        private bool Match(string token)
        {
            if (_index + token.Length > _json.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                if (_json[_index + i] != token[i])
                {
                    return false;
                }
            }

            _index += token.Length;
            return true;
        }

        private bool TryConsume(char c)
        {
            if (_index >= _json.Length || _json[_index] != c)
            {
                return false;
            }

            _index++;
            return true;
        }

        private void Expect(char c)
        {
            SkipWhitespace();
            if (!TryConsume(c))
            {
                throw new FormatException($"Expected '{c}'.");
            }
        }

        private char Read()
        {
            if (_index >= _json.Length)
            {
                throw new FormatException("Unexpected end of JSON content.");
            }

            return _json[_index++];
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
            {
                _index++;
            }
        }
    }
}
