// <copyright file="BugReportEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>
/// The rules of the bug reports, without the game: sending (a daily limit per account in Sydney days), the server owner's
/// decisions and answers, and what a player sees of their own reports.
/// </summary>
public static class BugReportEngine
{
    /// <summary>The shortest text of a report.</summary>
    public const int MinTextLength = 10;

    /// <summary>The longest text the game adds.</summary>
    public const int MaxInfoLength = 2000;

    /// <summary>The categories a player picks from.</summary>
    public static readonly IReadOnlyList<string> Categories = ["display", "skill", "item", "quest", "connection", "other"];

    /// <summary>Adds a report, unless the account sent its reports for today or the text is too short.</summary>
    /// <param name="data">The data.</param>
    /// <param name="report">The report (its id and time are set here).</param>
    /// <param name="configuration">The settings.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="sydney">The Sydney time zone.</param>
    /// <returns>The added report, or the EN/VI reason it wasn't.</returns>
    public static (BugReport? Report, string En, string Vi) Submit(BugReportStoreData data, BugReport report, BugReportsConfiguration configuration, DateTime nowUtc, TimeZoneInfo sydney)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(report);
        var max = Math.Max(MinTextLength, configuration.MaxTextLength);
        var text = (report.Text ?? string.Empty).Trim();
        if (text.Length < MinTextLength)
        {
            return (null, $"Please describe the bug in at least {MinTextLength} characters.", $"Hãy mô tả lỗi ít nhất {MinTextLength} ký tự.");
        }

        var today = DayOf(nowUtc, sydney);
        var sentToday = data.Reports.Count(r => string.Equals(r.Login, report.Login, StringComparison.OrdinalIgnoreCase) && DayOf(r.CreatedUtc, sydney) == today);
        if (sentToday >= configuration.DailyLimitPerAccount)
        {
            return (null, $"You sent {sentToday} bug reports today; you can send more tomorrow (Sydney time).", $"Hôm nay bạn đã gửi {sentToday} báo lỗi; ngày mai (giờ Sydney) bạn gửi tiếp được.");
        }

        report.Text = text.Length > max ? text[..max] : text;
        report.Info = report.Info.Length > MaxInfoLength ? report.Info[..MaxInfoLength] : report.Info;
        report.Category = Categories.Contains(report.Category) ? report.Category : "other";
        report.Id = $"B{data.NextNumber++}";
        report.CreatedUtc = nowUtc;
        report.Status = BugReportStatus.New;
        report.Seen = true;
        data.Reports.Add(report);
        return (report, $"Thank you! Your bug report {report.Id} was sent to the server's owner.", $"Cảm ơn bạn! Báo lỗi {report.Id} đã được gửi tới chủ server.");
    }

    /// <summary>The reports of an account, newest first.</summary>
    /// <param name="data">The data.</param>
    /// <param name="login">The account.</param>
    /// <param name="count">How many.</param>
    /// <returns>The reports.</returns>
    public static List<BugReport> Mine(BugReportStoreData data, string login, int count = 20) =>
        data.Reports.Where(r => string.Equals(r.Login, login, StringComparison.OrdinalIgnoreCase)).Reverse().Take(count).ToList();

    /// <summary>How many reports an account may still send today.</summary>
    /// <param name="data">The data.</param>
    /// <param name="login">The account.</param>
    /// <param name="configuration">The settings.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="sydney">The Sydney time zone.</param>
    /// <returns>The number left.</returns>
    public static int LeftToday(BugReportStoreData data, string login, BugReportsConfiguration configuration, DateTime nowUtc, TimeZoneInfo sydney)
    {
        var today = DayOf(nowUtc, sydney);
        return Math.Max(0, configuration.DailyLimitPerAccount - data.Reports.Count(r => string.Equals(r.Login, login, StringComparison.OrdinalIgnoreCase) && DayOf(r.CreatedUtc, sydney) == today));
    }

    /// <summary>Sets a report's status (the player sees it next time).</summary>
    /// <param name="data">The data.</param>
    /// <param name="id">The report.</param>
    /// <param name="status">The status.</param>
    /// <param name="duplicateOf">For <see cref="BugReportStatus.Duplicate"/>: the report it repeats.</param>
    /// <returns>The report, or <c>null</c> when there's none (or the duplicate's original doesn't exist).</returns>
    public static BugReport? SetStatus(BugReportStoreData data, string id, BugReportStatus status, string? duplicateOf = null)
    {
        if (Find(data, id) is not { } report)
        {
            return null;
        }

        if (status == BugReportStatus.Duplicate && (Find(data, duplicateOf) is not { } original || original == report))
        {
            return null;
        }

        if (report.Status != status)
        {
            report.Seen = false;
        }

        report.Status = status;
        report.DuplicateOf = status == BugReportStatus.Duplicate ? Find(data, duplicateOf)!.Id : null;
        return report;
    }

    /// <summary>Answers a report.</summary>
    /// <param name="data">The data.</param>
    /// <param name="id">The report.</param>
    /// <param name="text">The answer.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The report, or <c>null</c>.</returns>
    public static BugReport? Reply(BugReportStoreData data, string id, string text, DateTime nowUtc)
    {
        if (Find(data, id) is not { } report || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        report.Reply = text.Trim();
        report.ReplyUtc = nowUtc;
        report.Seen = false;
        return report;
    }

    /// <summary>The reports of an account with news the player hasn't seen; marks them seen.</summary>
    /// <param name="data">The data.</param>
    /// <param name="login">The account.</param>
    /// <returns>The reports.</returns>
    public static List<BugReport> TakeUnseen(BugReportStoreData data, string login)
    {
        var unseen = data.Reports.Where(r => !r.Seen && string.Equals(r.Login, login, StringComparison.OrdinalIgnoreCase)).ToList();
        unseen.ForEach(r => r.Seen = true);
        return unseen;
    }

    /// <summary>Finds a report by its id.</summary>
    /// <param name="data">The data.</param>
    /// <param name="id">The id.</param>
    /// <returns>The report.</returns>
    public static BugReport? Find(BugReportStoreData data, string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : data.Reports.FirstOrDefault(r => string.Equals(r.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    private static DateOnly DayOf(DateTime utc, TimeZoneInfo sydney) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), sydney));
}
