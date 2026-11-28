// <copyright file="VietnameseBanks.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>
/// The common banks of the NAPAS 247 (VietQR) network with their bank ids. A host whose bank isn't listed
/// can still enter its 6-digit id by hand.
/// </summary>
public static class VietnameseBanks
{
    /// <summary>Gets the banks: NAPAS id, short code and the name players know.</summary>
    public static IReadOnlyList<(string Bin, string Code, string Name)> All { get; } =
    [
        ("970436", "VCB", "Vietcombank"),
        ("970407", "TCB", "Techcombank"),
        ("970415", "ICB", "VietinBank"),
        ("970418", "BIDV", "BIDV"),
        ("970405", "VBA", "Agribank"),
        ("970422", "MB", "MB Bank"),
        ("970416", "ACB", "ACB"),
        ("970432", "VPB", "VPBank"),
        ("970423", "TPB", "TPBank"),
        ("970403", "STB", "Sacombank"),
        ("970437", "HDB", "HDBank"),
        ("970441", "VIB", "VIB"),
        ("970443", "SHB", "SHB"),
        ("970431", "EIB", "Eximbank"),
        ("970426", "MSB", "MSB"),
        ("970448", "OCB", "OCB"),
        ("970440", "SEAB", "SeABank"),
        ("970449", "LPB", "LPBank"),
        ("970454", "VCCB", "Bản Việt (BVBank)"),
        ("970429", "SCB", "SCB"),
        ("970452", "KLB", "KienlongBank"),
        ("970409", "BAB", "Bac A Bank"),
        ("970425", "ABB", "ABBANK"),
        ("970428", "NAB", "Nam A Bank"),
        ("970433", "VIETBANK", "VietBank"),
        ("970427", "VAB", "VietABank"),
        ("970412", "PVCB", "PVcomBank"),
        ("970419", "NCB", "NCB"),
        ("970430", "PGB", "PGBank"),
        ("970400", "SGICB", "Saigonbank"),
        ("970406", "DOB", "DongA Bank"),
        ("970424", "SHBVN", "Shinhan Bank"),
        ("970458", "UOB", "UOB"),
        ("970457", "WVN", "Woori Bank"),
        ("963388", "TIMO", "Timo"),
        ("546034", "CAKE", "CAKE by VPBank"),
        ("546035", "UBANK", "Ubank by VPBank"),
    ];

    /// <summary>The name players know for a bank id, or the id itself when the bank isn't listed.</summary>
    /// <param name="bin">The NAPAS bank id.</param>
    /// <returns>The bank's name.</returns>
    public static string NameOf(string? bin) =>
        All.FirstOrDefault(b => b.Bin == bin?.Trim()).Name ?? bin?.Trim() ?? string.Empty;

    /// <summary>The short code of a bank id, or the id itself when the bank isn't listed.</summary>
    /// <param name="bin">The NAPAS bank id.</param>
    /// <returns>The bank's code.</returns>
    public static string CodeOf(string? bin) =>
        All.FirstOrDefault(b => b.Bin == bin?.Trim()).Code ?? bin?.Trim() ?? string.Empty;
}
