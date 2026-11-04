// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Hairs.Storage;

/// <summary>
/// Normalization and containment checks for workspace-relative paths.
/// Ensures security against path traversal and reserved device names.
/// </summary>
public static class HairsPath
{
    /// <summary>
    /// Normalizes to forward slashes and resolves '.' and '..' segments.
    /// Returns null when the path escapes the root.
    /// </summary>
    public static string? Normalize(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (relativePath.Length >= 2 && relativePath[1] == ':')
            return null;
        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
            return null;

        var segments = new List<string>();
        foreach (string raw in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == ".")
                continue;
            if (raw == "..")
            {
                if (segments.Count == 0)
                    return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (raw.TrimEnd('.', ' ').Length != raw.Length)
                return null;

            if (IsReservedDeviceName(raw))
                return null;

            segments.Add(raw);
        }

        return segments.Count == 0 ? string.Empty : string.Join('/', segments);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is inside <paramref name="root"/>.
    /// </summary>
    public static bool IsContained(string candidate, string root)
    {
        if (root.Length == 0)
            return true;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;
        return candidate.Length == root.Length || candidate[root.Length] == '/';
    }

    public static StringComparer GetComparer(bool caseInsensitive) =>
        caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool IsReservedDeviceName(string segment)
    {
        int dot = segment.IndexOf('.');
        string stem = dot < 0 ? segment : segment[..dot];
        return stem.ToUpperInvariant() switch
        {
            "CON" or "PRN" or "AUX" or "NUL" => true,
            ['C', 'O', 'M', >= '1' and <= '9'] => true,
            ['L', 'P', 'T', >= '1' and <= '9'] => true,
            _ => false,
        };
    }
}
