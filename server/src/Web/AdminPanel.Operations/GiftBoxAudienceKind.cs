// <copyright file="GiftBoxAudienceKind.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>The explicit host selection, with a safe single-account default.</summary>
public enum GiftBoxAudienceKind
{
    /// <summary>One exact account login, including any allowed punctuation.</summary>
    Account,

    /// <summary>One exact account login per pasted line.</summary>
    List,

    /// <summary>Every existing account, optionally filtered by observed activity.</summary>
    Everyone,
}
