// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Security.Cryptography;
using System.Text;

namespace Hairs;

/// <summary>
/// Normalization and hashing for review content verification.
/// Normalizes line endings to LF and strips one leading BOM.
/// </summary>
public static class ReviewContentHash
{
    public const string Algorithm = "sha256-nlf";
    private const char ByteOrderMark = (char)0xFEFF;

    public static string Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        byte[] bytes = Encoding.UTF8.GetBytes(Normalize(text));
        return string.Concat(Algorithm, ":", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    public static bool Matches(string? recorded, string text)
    {
        if (string.IsNullOrEmpty(recorded) || !IsKnownAlgorithm(recorded))
            return false;

        return string.Equals(recorded, Compute(text), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKnownAlgorithm(string? recorded) =>
        recorded is not null && recorded.StartsWith(Algorithm + ":", StringComparison.Ordinal);

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == ByteOrderMark)
            text = text[1..];

        if (text.IndexOf('\r') < 0)
            return text;

        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '\r')
            {
                builder.Append(c);
                continue;
            }

            builder.Append('\n');
            if (i + 1 < text.Length && text[i + 1] == '\n')
                i++;
        }

        return builder.ToString();
    }
}
