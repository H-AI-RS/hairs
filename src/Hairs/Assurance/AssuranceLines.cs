// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace Hairs.Assurance;

/// <summary>
/// A source file split into lines preserving individual line terminators.
/// </summary>
public sealed class AssuranceLines
{
    private readonly List<string> _lines = [];
    private readonly List<string> _separators = [];

    public AssuranceLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            string? separator = text[index] switch
            {
                '\r' when index + 1 < text.Length && text[index + 1] == '\n' => "\r\n",
                '\r' => "\r",
                '\n' => "\n",
                _ => null,
            };

            if (separator is null)
                continue;

            _lines.Add(text[start..index]);
            _separators.Add(separator);
            index += separator.Length - 1;
            start = index + 1;
        }

        _lines.Add(text[start..]);
        _separators.Add(string.Empty);
    }

    public int Count => _lines.Count;

    public string this[int index] => _lines[index];

    public string NewLine
    {
        get
        {
            foreach (string separator in _separators)
            {
                if (separator.Length > 0)
                    return separator;
            }

            return "\n";
        }
    }

    public void Replace(int index, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines[index] = line;
    }

    public string SeparatorOf(int index) => _separators[index];

    public void Insert(int index, IReadOnlyList<string> lines) => Insert(index, lines, NewLine);

    public void Insert(int index, IReadOnlyList<string> lines, string separator)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(separator);

        if (separator is not ("\r\n" or "\n" or "\r"))
            throw new ArgumentException("A separator is CRLF, LF or CR.", nameof(separator));

        for (int offset = 0; offset < lines.Count; offset++)
        {
            _lines.Insert(index + offset, lines[offset]);
            _separators.Insert(index + offset, separator);
        }
    }

    public void RemoveRange(int index, int count)
    {
        _lines.RemoveRange(index, count);
        _separators.RemoveRange(index, count);
    }

    public static string IndentOf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        int index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;

        return line[..index];
    }

    public string Render()
    {
        var builder = new StringBuilder();
        for (int index = 0; index < _lines.Count; index++)
            builder.Append(_lines[index]).Append(_separators[index]);

        return builder.ToString();
    }
}
