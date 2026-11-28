// <copyright file="ByteArrayDownloadControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using HNMO.DataModel.Configuration;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Mvc;
using Moq;

/// <summary>
/// Tests request validation for <see cref="ByteArrayDownloadController"/>.
/// </summary>
[TestFixture]
public class ByteArrayDownloadControllerTests
{
    /// <summary>
    /// Unrecognized type and property names result in a missing download response before storage is opened.
    /// </summary>
    /// <param name="typeName">The requested object type.</param>
    /// <param name="propertyName">The requested object property.</param>
    /// <returns>The test task.</returns>
    [TestCase("UnknownType", "UnknownProperty")]
    [TestCase("HNMO.DataModel.Configuration.GameMapDefinition", "UnknownProperty")]
    [TestCase("HNMO.DataModel.Configuration.GameMapDefinition", nameof(GameMapDefinition.Name))]
    public async Task UnknownDownloadReturnsNotFoundAsync(string typeName, string propertyName)
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);
        var controller = new ByteArrayDownloadController(persistence.Object);

        var response = await controller.GetAsync(typeName, Guid.NewGuid(), propertyName).ConfigureAwait(false);

        Assert.That(response, Is.TypeOf<NotFoundResult>());
        persistence.VerifyNoOtherCalls();
    }
}
