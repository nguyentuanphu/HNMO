// <copyright file="AccountStateChange.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Entities;

/// <summary>An offline account state change and its mandatory audit reason.</summary>
public sealed class AccountStateChange
{
    /// <summary>Gets or sets the desired state; privileged states require Administrator.</summary>
    [Required]
    [EnumDataType(typeof(AccountState))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountState? State { get; set; }

    /// <summary>Gets or sets the audit reason.</summary>
    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
