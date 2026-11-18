// <copyright file="MssqlLegacyDatabaseReader.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Reads legacy server tables from a local Microsoft SQL Server instance on Windows
/// without adding external database client package dependencies.
/// Password columns are intentionally excluded from every query.
/// </summary>
public sealed class MssqlLegacyDatabaseReader : ILegacyDatabaseReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<LegacyDatabaseSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Direct local MSSQL migration requires Windows with .NET Framework System.Data.SqlClient.");
        }

        const string script = """
            $ErrorActionPreference = 'Stop'
            Add-Type -AssemblyName System.Data
            $conn = New-Object System.Data.SqlClient.SqlConnection($env:HNMO_LEGACY_SQL_CS)
            $conn.Open()
            try {
                function Read-Rows($sql, $mapper) {
                    $cmd = $conn.CreateCommand()
                    $cmd.CommandTimeout = 15
                    $cmd.CommandText = $sql
                    $reader = $cmd.ExecuteReader()
                    $list = @()
                    try {
                        while ($reader.Read()) {
                            $list += (& $mapper $reader)
                        }
                    } finally {
                        $reader.Dispose()
                    }
                    return ,$list
                }

                $accounts = @()
                try {
                    $accounts = Read-Rows "SELECT memb___id, mail_addr, bloc_code FROM MEMB_INFO" {
                        param($r)
                        [pscustomobject]@{
                            loginName = [string]$r["memb___id"]
                            email = if ($r["mail_addr"] -is [DBNull]) { "" } else { [string]$r["mail_addr"] }
                            isBanned = if ($r["bloc_code"] -is [DBNull]) { $false } else { [string]$r["bloc_code"] -eq "1" }
                        }
                    }
                } catch {}

                $chars = @()
                try {
                    $chars = Read-Rows "SELECT AccountID, Name, Class, cLevel, LevelUpPoint, Experience, Strength, Dexterity, Vitality, Energy, Leadership, Money, MapNumber, MapPosX, MapPosY, Inventory FROM [Character]" {
                        param($r)
                        $inv = if ($r["Inventory"] -is [DBNull]) { "" } else { [Convert]::ToBase64String([byte[]]$r["Inventory"]) }
                        [pscustomobject]@{
                            accountId = [string]$r["AccountID"]
                            name = [string]$r["Name"]
                            classNumber = [int]$r["Class"]
                            level = [int]$r["cLevel"]
                            levelUpPoints = [int]$r["LevelUpPoint"]
                            experience = [long]$r["Experience"]
                            strength = [int]$r["Strength"]
                            agility = [int]$r["Dexterity"]
                            vitality = [int]$r["Vitality"]
                            energy = [int]$r["Energy"]
                            leadership = if ($r["Leadership"] -is [DBNull]) { 0 } else { [int]$r["Leadership"] }
                            money = if ($r["Money"] -is [DBNull]) { 0 } else { [int]$r["Money"] }
                            mapNumber = if ($r["MapNumber"] -is [DBNull]) { 0 } else { [int]$r["MapNumber"] }
                            mapPosX = if ($r["MapPosX"] -is [DBNull]) { 125 } else { [int]$r["MapPosX"] }
                            mapPosY = if ($r["MapPosY"] -is [DBNull]) { 125 } else { [int]$r["MapPosY"] }
                            inventoryBase64 = $inv
                        }
                    }
                } catch {}

                $vaults = @()
                try {
                    $vaults = Read-Rows "SELECT AccountID, Money, Items FROM warehouse" {
                        param($r)
                        $items = if ($r["Items"] -is [DBNull]) { "" } else { [Convert]::ToBase64String([byte[]]$r["Items"]) }
                        [pscustomobject]@{
                            accountId = [string]$r["AccountID"]
                            money = if ($r["Money"] -is [DBNull]) { 0 } else { [int]$r["Money"] }
                            itemsBase64 = $items
                        }
                    }
                } catch {}

                $guilds = @()
                try {
                    $guilds = Read-Rows "SELECT G_Name, G_Master, G_Score, G_Notice, G_Mark FROM Guild" {
                        param($r)
                        $mark = if ($r["G_Mark"] -is [DBNull]) { "" } else { [Convert]::ToBase64String([byte[]]$r["G_Mark"]) }
                        [pscustomobject]@{
                            name = [string]$r["G_Name"]
                            masterName = if ($r["G_Master"] -is [DBNull]) { "" } else { [string]$r["G_Master"] }
                            score = if ($r["G_Score"] -is [DBNull]) { 0 } else { [int]$r["G_Score"] }
                            notice = if ($r["G_Notice"] -is [DBNull]) { "" } else { [string]$r["G_Notice"] }
                            logoBase64 = $mark
                        }
                    }
                } catch {}

                $members = @()
                try {
                    $members = Read-Rows "SELECT G_Name, Name, G_Status FROM GuildMember" {
                        param($r)
                        [pscustomobject]@{
                            guildName = [string]$r["G_Name"]
                            characterName = [string]$r["Name"]
                            status = if ($r["G_Status"] -is [DBNull]) { 0 } else { [int]$r["G_Status"] }
                        }
                    }
                } catch {}

                [pscustomobject]@{
                    accounts = $accounts
                    characters = $chars
                    vaults = $vaults
                    guilds = $guilds
                    guildMembers = $members
                } | ConvertTo-Json -Depth 6 -Compress
            } finally {
                $conn.Dispose()
            }
            """;

        var tempScript = Path.Combine(Path.GetTempPath(), $"hnmo-sql-read-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(tempScript, script, cancellationToken).ConfigureAwait(false);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(tempScript);
            psi.Environment["HNMO_LEGACY_SQL_CS"] = connectionString;

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start local PowerShell SQL reader.");
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException($"MSSQL query failed: {stderr.Trim()}");
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return new LegacyDatabaseSnapshot();
            }

            var raw = JsonSerializer.Deserialize<RawSnapshot>(stdout, JsonOptions) ?? new RawSnapshot();
            var snapshot = new LegacyDatabaseSnapshot();

            foreach (var a in raw.Accounts ?? [])
            {
                if (!string.IsNullOrWhiteSpace(a.LoginName))
                {
                    snapshot.Accounts.Add(new LegacyAccountRow
                    {
                        LoginName = a.LoginName.Trim(),
                        Email = a.Email?.Trim() ?? string.Empty,
                        IsBanned = a.IsBanned,
                    });
                }
            }

            foreach (var c in raw.Characters ?? [])
            {
                if (!string.IsNullOrWhiteSpace(c.AccountId) && !string.IsNullOrWhiteSpace(c.Name))
                {
                    snapshot.Characters.Add(new LegacyCharacterRow
                    {
                        AccountId = c.AccountId.Trim(),
                        Name = c.Name.Trim(),
                        ClassNumber = (byte)Math.Clamp(c.ClassNumber, 0, 255),
                        Level = Math.Max(1, c.Level),
                        LevelUpPoints = Math.Max(0, c.LevelUpPoints),
                        Experience = Math.Max(0L, c.Experience),
                        Strength = Math.Max(0, c.Strength),
                        Agility = Math.Max(0, c.Agility),
                        Vitality = Math.Max(0, c.Vitality),
                        Energy = Math.Max(0, c.Energy),
                        Leadership = Math.Max(0, c.Leadership),
                        Money = Math.Max(0, c.Money),
                        MapNumber = (short)Math.Max(0, c.MapNumber),
                        MapPosX = (byte)Math.Clamp(c.MapPosX, 0, 255),
                        MapPosY = (byte)Math.Clamp(c.MapPosY, 0, 255),
                        InventoryBlob = DecodeBase64(c.InventoryBase64),
                    });
                }
            }

            foreach (var v in raw.Vaults ?? [])
            {
                if (!string.IsNullOrWhiteSpace(v.AccountId))
                {
                    snapshot.Vaults.Add(new LegacyVaultRow
                    {
                        AccountId = v.AccountId.Trim(),
                        Money = Math.Max(0, v.Money),
                        ItemsBlob = DecodeBase64(v.ItemsBase64),
                    });
                }
            }

            foreach (var g in raw.Guilds ?? [])
            {
                if (!string.IsNullOrWhiteSpace(g.Name))
                {
                    snapshot.Guilds.Add(new LegacyGuildRow
                    {
                        Name = g.Name.Trim(),
                        MasterName = g.MasterName?.Trim() ?? string.Empty,
                        Score = Math.Max(0, g.Score),
                        Notice = g.Notice?.Trim() ?? string.Empty,
                        Logo = DecodeBase64(g.LogoBase64),
                    });
                }
            }

            foreach (var m in raw.GuildMembers ?? [])
            {
                if (!string.IsNullOrWhiteSpace(m.GuildName) && !string.IsNullOrWhiteSpace(m.CharacterName))
                {
                    snapshot.GuildMembers.Add(new LegacyGuildMemberRow
                    {
                        GuildName = m.GuildName.Trim(),
                        CharacterName = m.CharacterName.Trim(),
                        Status = (byte)Math.Clamp(m.Status, 0, 255),
                    });
                }
            }

            return snapshot;
        }
        finally
        {
            if (File.Exists(tempScript))
            {
                File.Delete(tempScript);
            }
        }
    }

    private static byte[] DecodeBase64(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private sealed class RawSnapshot
    {
        public List<RawAccount>? Accounts { get; set; }

        public List<RawCharacter>? Characters { get; set; }

        public List<RawVault>? Vaults { get; set; }

        public List<RawGuild>? Guilds { get; set; }

        public List<RawGuildMember>? GuildMembers { get; set; }
    }

    private sealed class RawAccount
    {
        public string? LoginName { get; set; }

        public string? Email { get; set; }

        public bool IsBanned { get; set; }
    }

    private sealed class RawCharacter
    {
        public string? AccountId { get; set; }

        public string? Name { get; set; }

        public int ClassNumber { get; set; }

        public int Level { get; set; }

        public int LevelUpPoints { get; set; }

        public long Experience { get; set; }

        public int Strength { get; set; }

        public int Agility { get; set; }

        public int Vitality { get; set; }

        public int Energy { get; set; }

        public int Leadership { get; set; }

        public int Money { get; set; }

        public int MapNumber { get; set; }

        public int MapPosX { get; set; }

        public int MapPosY { get; set; }

        public string? InventoryBase64 { get; set; }
    }

    private sealed class RawVault
    {
        public string? AccountId { get; set; }

        public int Money { get; set; }

        public string? ItemsBase64 { get; set; }
    }

    private sealed class RawGuild
    {
        public string? Name { get; set; }

        public string? MasterName { get; set; }

        public int Score { get; set; }

        public string? Notice { get; set; }

        public string? LogoBase64 { get; set; }
    }

    private sealed class RawGuildMember
    {
        public string? GuildName { get; set; }

        public string? CharacterName { get; set; }

        public int Status { get; set; }
    }
}
