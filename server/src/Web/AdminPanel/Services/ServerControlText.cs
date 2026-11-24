// <copyright file="ServerControlText.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;

internal static class ServerControlText
{
    public static string Title => Translate("Server control", "Điều khiển máy chủ", "Serversteuerung", "服务器控制");

    public static string Help => Translate(
        "Manage local services, logs, updates, ports and database backups. Review the confirmation before stopping a service or applying an update.",
        "Quản lý dịch vụ cục bộ, nhật ký, bản cập nhật, cổng và sao lưu cơ sở dữ liệu. Đọc xác nhận trước khi dừng dịch vụ hoặc cập nhật.",
        "Verwalten Sie lokale Dienste, Protokolle, Updates, Ports und Datenbanksicherungen. Prüfen Sie die Bestätigung vor dem Beenden eines Dienstes oder vor einem Update.",
        "管理本地服务、日志、更新、端口和数据库备份。停止服务或应用更新前，请检查确认信息。");

    public static string Unavailable => Translate(
        "Open the primary admin panel from the launcher on the host computer to manage these services.",
        "Mở bảng quản trị chính từ trình khởi chạy trên máy host để quản lý các dịch vụ.",
        "Öffnen Sie das primäre Admin-Panel über das Startprogramm auf dem Hostcomputer, um diese Dienste zu verwalten.",
        "请在主机上通过启动器打开主管理面板，以管理这些服务。");

    private static string Translate(string en, string vi, string de, string zh) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "vi" => vi,
            "de" => de,
            "zh" => zh,
            _ => en,
        };
}
