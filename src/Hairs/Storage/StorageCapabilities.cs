// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Hairs.Storage;

/// <summary>
/// Capabilities that a storage provider can support.
/// </summary>
[Flags]
public enum StorageCapabilities
{
    None = 0,
    Read = 1,
    Write = 2,
    Modify = 4,
    Rename = 8,
    AtomicWrite = 16,
    DurableFileIds = 32,
    ChangeNotification = 64,
    CaseInsensitivePaths = 128,
}
