// <copyright file="EscrowLoginPrivacyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Escrow;

/// <summary>An escrow contract never shows a viewer the other parties' login names (audit 2.8).</summary>
[TestFixture]
public class EscrowLoginPrivacyTest
{
    /// <summary>The seller sees its own login only; the buyer its own; anyone else neither.</summary>
    [Test]
    public void EachViewerSeesOnlyItsOwnLogin()
    {
        var contract = new EscrowContract { SellerLogin = "seller1", SellerCharacter = "SellerDk", AcceptedByLogin = "buyer1", AcceptedByCharacter = "BuyerElf" };

        var seller = contract.WithoutOtherLogins("SELLER1");
        var buyer = contract.WithoutOtherLogins("buyer1");
        var other = contract.WithoutOtherLogins("someone");

        Assert.Multiple(() =>
        {
            Assert.That((seller.SellerLogin, seller.AcceptedByLogin), Is.EqualTo(("seller1", string.Empty)));
            Assert.That((buyer.SellerLogin, buyer.AcceptedByLogin), Is.EqualTo((string.Empty, "buyer1")));
            Assert.That((other.SellerLogin, other.AcceptedByLogin), Is.EqualTo((string.Empty, string.Empty)));
            Assert.That(other.SellerCharacter, Is.EqualTo("SellerDk"), "characters stay visible");
            Assert.That(contract.SellerLogin, Is.EqualTo("seller1"), "the stored contract is unchanged");
        });
    }
}
