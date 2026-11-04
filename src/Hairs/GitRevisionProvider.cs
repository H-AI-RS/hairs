// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hairs;

public sealed class GitRevisionProvider : IRevisionProvider
{
    private readonly string _workingDirectory;

    public GitRevisionProvider(string workingDirectory) =>
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));

    public async ValueTask<string?> GetCurrentRevisionAsync(CancellationToken cancellationToken = default)
    {
        string? revision = await GitCommand
            .RunAsync(_workingDirectory, "rev-parse HEAD", cancellationToken)
            .ConfigureAwait(false);

        return revision is not null && IsHex(revision) ? revision : null;
    }

    private static bool IsHex(string value)
    {
        if (value.Length is < 7 or > 64)
            return false;

        foreach (char c in value)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }
}

internal static class GitCommand
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async ValueTask<string?> RunAsync(
        string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
                return null;

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(Timeout);

            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            Task<string> errors = process.StandardError.ReadToEndAsync(deadline.Token);

            await Task.WhenAll(output, errors).ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);

            return process.ExitCode == 0 ? output.Result.Trim() : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException
                or PlatformNotSupportedException or ObjectDisposedException)
        {
            return null;
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or System.ComponentModel.Win32Exception
                        or NotSupportedException)
                {
                }

                process.Dispose();
            }
        }
    }
}

public static class GitIdentity
{
    public static async ValueTask<string> ResolveReviewerAsync(
        string workingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);

        string? name = await GitCommand
            .RunAsync(workingDirectory, "config user.name", cancellationToken)
            .ConfigureAwait(false);

        return name is { Length: > 0 } ? name : Environment.UserName;
    }
}
