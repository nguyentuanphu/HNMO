// <copyright file="NewsAdminOperationsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Operations;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Moq;

/// <summary>Tests for <see cref="NewsAdminOperations"/>.</summary>
[TestFixture]
public class NewsAdminOperationsTests
{
    /// <summary>
    /// The database provider refuses a cached context without a game configuration; loading the news must not ask for one.
    /// </summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LoadDoesNotRequestACachedContextWithoutGameConfigurationAsync()
    {
        var inner = new InMemoryPersistenceContextProvider();
        var provider = new Mock<IPersistenceContextProvider>();
        provider.Setup(p => p.CreateNewTypedContext(It.IsAny<Type>(), It.IsAny<bool>(), It.IsAny<GameConfiguration?>()))
            .Returns((Type type, bool useCache, GameConfiguration? config) => useCache && config is null
                ? throw new ArgumentNullException(nameof(config), "When cache should be used, the game configuration must be provided.")
                : inner.CreateNewTypedContext(type, useCache, config));
        var servers = new Mock<IServerProvider>();
        servers.SetupGet(s => s.Servers).Returns([]);
        var operations = new NewsAdminOperations(provider.Object, servers.Object, AdminOperationTestServices.CreateExecutor());

        var loaded = await operations.LoadAsync(default).ConfigureAwait(false);

        Assert.That(loaded.Configuration, Is.Not.Null);
    }
}
