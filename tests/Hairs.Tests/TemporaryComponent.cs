// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using Hairs.Cli.Assurance;

namespace Hairs.Tests;

/// <summary>
/// A component root on disk for one test, deleted afterwards.
/// </summary>
internal sealed class TemporaryComponent : IDisposable
{
    public TemporaryComponent()
    {
        Root = Path.Combine(Path.GetTempPath(), "hairs-assurance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void Write(string relative, string text, bool byteOrderMark = false) =>
        WriteBytes(relative, AssuranceSourceText.Encode(text, byteOrderMark));

    public void WriteBytes(string relative, byte[] bytes)
    {
        string path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Folder(string relative) => Directory.CreateDirectory(PathOf(relative));

    public bool TryLinkDirectory(string relative, string target)
    {
        string link = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);

        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!OperatingSystem.IsWindows())
                return false;
        }

        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (string argument in new[] { "/c", "mklink", "/J", link, target })
            start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start);
        process?.StandardOutput.ReadToEnd();
        process?.WaitForExit();
        return Directory.Exists(link);
    }

    public byte[] ReadBytes(string relative) => File.ReadAllBytes(PathOf(relative));

    public string Read(string relative) => Encoding.UTF8.GetString(ReadBytes(relative));

    public void Project(string relative, string? assemblyName = null, bool isTest = false)
    {
        var properties = new StringBuilder();
        if (assemblyName is not null)
            properties.Append("<AssemblyName>").Append(assemblyName).Append("</AssemblyName>");

        if (isTest)
            properties.Append("<IsTestProject>true</IsTestProject>");

        Write(relative,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{properties}</PropertyGroup></Project>\n");
    }

    public (int Exit, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = AssuranceCommand.Run(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}
