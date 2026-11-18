// <copyright file="DiskUsageReport.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Disk capacity and save-folder breakdown shown on <c>/control</c>.
/// </summary>
/// <param name="DriveRoot">Mount/drive root path.</param>
/// <param name="TotalDriveBytes">Total drive capacity in bytes.</param>
/// <param name="FreeDriveBytes">Available drive space in bytes.</param>
/// <param name="SaveBytes">Combined size of the save folder in bytes.</param>
/// <param name="LogsBytes">Size of <c>save/logs</c> in bytes.</param>
/// <param name="BackupsBytes">Size of <c>save/backups</c> in bytes.</param>
/// <param name="DatabaseBytes">Size of <c>save/database</c> in bytes.</param>
/// <param name="LowDiskWarningMb">Configured low-disk threshold in MB.</param>
/// <param name="IsLowDisk">True if free space is below <paramref name="LowDiskWarningMb"/> MB or below 5% of capacity.</param>
public sealed record DiskUsageReport(
    string DriveRoot,
    long TotalDriveBytes,
    long FreeDriveBytes,
    long SaveBytes,
    long LogsBytes,
    long BackupsBytes,
    long DatabaseBytes,
    int LowDiskWarningMb,
    bool IsLowDisk);
