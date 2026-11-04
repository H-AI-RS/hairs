// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Hairs.Assurance;

public sealed class AssuranceGlob
{
    private readonly Regex _regex;
    private readonly bool _nameOnly;

    private AssuranceGlob(string pattern, Regex regex, bool nameOnly)
    {
        Pattern = pattern;
        _regex = regex;
        _nameOnly = nameOnly;
    }

    public string Pattern { get; }

    public static bool TryParse(string? pattern, out AssuranceGlob? glob, out string? problem)
    {
        glob = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            problem = "a glob is empty";
            return false;
        }

        if (pattern.Contains('\\', StringComparison.Ordinal))
        {
            problem = $"the glob '{pattern}' uses '\\'; write paths with '/'";
            return false;
        }

        if (pattern.StartsWith('/') || (pattern.Length > 1 && pattern[1] == ':'))
        {
            problem = $"the glob '{pattern}' is rooted; globs are relative to the component root";
            return false;
        }

        foreach (string segment in pattern.Split('/'))
        {
            if (segment is ".." or ".")
            {
                problem = $"the glob '{pattern}' has a '{segment}' segment";
                return false;
            }
        }

        bool nameOnly = !pattern.Contains('/', StringComparison.Ordinal);
        glob = new AssuranceGlob(pattern, new Regex(ToRegex(pattern), RegexOptions.CultureInvariant), nameOnly);
        return true;
    }

    public bool IsMatch(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string subject = _nameOnly ? relativePath[(relativePath.LastIndexOf('/') + 1)..] : relativePath;
        return _regex.IsMatch(subject);
    }

    public override string ToString() => Pattern;

    private static string ToRegex(string pattern)
    {
        var regex = new StringBuilder("^");
        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];
            if (character == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                bool segmentStart = index == 0 || pattern[index - 1] == '/';
                bool slashAfter = index + 2 < pattern.Length && pattern[index + 2] == '/';
                if (segmentStart && slashAfter)
                {
                    regex.Append("(?:[^/]*/)*");
                    index += 2;
                }
                else
                {
                    regex.Append(".*");
                    index++;
                }
            }
            else if (character == '*')
            {
                regex.Append("[^/]*");
            }
            else if (character == '?')
            {
                regex.Append("[^/]");
            }
            else
            {
                regex.Append(Regex.Escape(character.ToString()));
            }
        }

        return regex.Append('$').ToString();
    }
}
