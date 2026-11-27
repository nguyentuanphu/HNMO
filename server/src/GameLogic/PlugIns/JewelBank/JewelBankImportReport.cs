// <copyright file="JewelBankImportReport.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

/// <summary>What <see cref="JewelBankDatabaseImport.RunAsync"/> did, or would do in a dry run.</summary>
public sealed class JewelBankImportReport
{
    /// <summary>Gets or sets a value indicating whether nothing was written.</summary>
    public bool DryRun { get; set; }

    /// <summary>Gets or sets the SHA-256 of the JSON entries read, to tell two runs over the same input.</summary>
    public string InputChecksum { get; set; } = string.Empty;

    /// <summary>Gets the JSON balance totals of the known accounts, by jewel key.</summary>
    public Dictionary<string, long> JsonTotals { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the JSON waiting website withdrawals of the known accounts, by jewel key.</summary>
    public Dictionary<string, long> JsonPending { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the entries imported by this run.</summary>
    public List<string> Imported { get; } = [];

    /// <summary>Gets the entries a dry run would import.</summary>
    public List<string> WouldImport { get; } = [];

    /// <summary>Gets or sets the number of entries an earlier run imported already.</summary>
    public int AlreadyImported { get; set; }

    /// <summary>Gets the entries which differ from what an earlier run imported (left for an operator).</summary>
    public List<string> Conflicts { get; } = [];

    /// <summary>Gets the logins of the JSON file without an account in this database (not imported).</summary>
    public List<string> UnknownAccounts { get; } = [];
}
