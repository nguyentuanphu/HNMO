// <copyright file="BackupInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Information about a backup archive in <c>save/backups</c>.
/// </summary>
/// <param name="FileName">Archive file name.</param>
/// <param name="SizeBytes">Archive size in bytes.</param>
/// <param name="CreatedSydney">Creation timestamp formatted in Sydney time.</param>
public sealed record BackupInfo(string FileName, long SizeBytes, string CreatedSydney);
