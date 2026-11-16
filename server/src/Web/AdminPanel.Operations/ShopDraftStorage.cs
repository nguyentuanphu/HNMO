// <copyright file="ShopDraftStorage.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.DataModel.Entities;

/// <summary>A detached merchant storage used only by a graphical draft.</summary>
public sealed class ShopDraftStorage : ItemStorage
{
    /// <summary>Initializes a new instance of the <see cref="ShopDraftStorage"/> class.</summary>
    public ShopDraftStorage() => this.Items = new List<Item>();
}
