// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;

namespace Hairs.Cli.Assurance;

/// <summary>
/// A source file's text together with what it takes to write it back unchanged.
/// </summary>
internal sealed class AssuranceSourceText
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16LittleEndianBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BigEndianBom = [0xFE, 0xFF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private AssuranceSourceText(string text, bool hasByteOrderMark, byte[] original)
    {
        Text = text;
        HasByteOrderMark = hasByteOrderMark;
        Original = original;
    }

    /// <summary>The decoded text, without the byte-order mark.</summary>
    public string Text { get; }

    /// <summary>Whether the file opened with a UTF-8 byte-order mark.</summary>
    public bool HasByteOrderMark { get; }

    /// <summary>The bytes as read, to detect a change on disk before writing.</summary>
    public byte[] Original { get; }

    /// <summary>
    /// Reads <paramref name="path"/> as UTF-8, or explains why it cannot be
    /// rewritten safely.
    /// </summary>
    public static bool TryRead(string path, out AssuranceSourceText? source, out string? problem)
    {
        source = null;
        problem = null;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException exception)
        {
            problem = $"could not be read: {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            problem = $"could not be read: {exception.Message}";
            return false;
        }

        bool bom = bytes.AsSpan().StartsWith(Utf8Bom);
        if (!bom && (bytes.AsSpan().StartsWith(Utf16LittleEndianBom) || bytes.AsSpan().StartsWith(Utf16BigEndianBom)))
        {
            problem = "is UTF-16 or UTF-32; only UTF-8 files are handled";
            return false;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes, bom ? Utf8Bom.Length : 0, bytes.Length - (bom ? Utf8Bom.Length : 0));
        }
        catch (DecoderFallbackException)
        {
            problem = "is not valid UTF-8";
            return false;
        }

        if (!Encode(text, bom).AsSpan().SequenceEqual(bytes))
        {
            problem = "does not round-trip through UTF-8";
            return false;
        }

        source = new AssuranceSourceText(text, bom, bytes);
        return true;
    }

    /// <summary>The bytes for <paramref name="text"/>, with the mark if the file had one.</summary>
    public static byte[] Encode(string text, bool hasByteOrderMark)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] body = StrictUtf8.GetBytes(text);
        if (!hasByteOrderMark)
            return body;

        byte[] withMark = new byte[Utf8Bom.Length + body.Length];
        Utf8Bom.CopyTo(withMark, 0);
        body.CopyTo(withMark, Utf8Bom.Length);
        return withMark;
    }

    /// <summary>
    /// Writes <paramref name="text"/> in place of this file, keeping its
    /// byte-order mark. Refuses when the file changed on disk since it was read,
    /// so an edit made meanwhile is not overwritten.
    /// </summary>
    public bool TryWrite(string path, string text, out string? problem)
    {
        problem = null;

        try
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(Original))
            {
                problem = "changed on disk since it was read; nothing was written to it";
                return false;
            }

            File.WriteAllBytes(path, Encode(text, HasByteOrderMark));
            return true;
        }
        catch (IOException exception)
        {
            problem = $"could not be written: {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            problem = $"could not be written: {exception.Message}";
            return false;
        }
    }
}
