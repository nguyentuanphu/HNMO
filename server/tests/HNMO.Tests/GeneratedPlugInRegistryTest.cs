// <copyright file="GeneratedPlugInRegistryTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using HNMO.PlugIns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for the plugin registries which are generated at compile time.
/// The <see cref="PlugInManager"/> has to register the same plugins in the same order with them as by searching the assemblies.
/// </summary>
[TestFixture]
[NonParallelizable]
public class GeneratedPlugInRegistryTest
{
    /// <summary>
    /// Gets the assemblies which contain plugins.
    /// </summary>
    private static IEnumerable<Assembly> PlugInAssemblies =>
    [
        typeof(GameLogic.GameContext).Assembly,
        typeof(GameServer.GameServer).Assembly,
        typeof(Persistence.Initialization.DataInitializationBase).Assembly,
        typeof(Network.Connection).Assembly,
    ];

    /// <summary>
    /// Tests that the registry contains the same plugins in the same order as <see cref="Assembly.DefinedTypes"/>.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    [TestCaseSource(nameof(PlugInAssemblies))]
    public void RegistryContainsPlugInsInDefinitionOrder(Assembly assembly)
    {
        var attribute = assembly.GetCustomAttribute<PlugInRegistryAttribute>();
        Assert.That(attribute, Is.Not.Null, $"No plugin registry was generated for {assembly.GetName().Name}.");

        var registry = (IPlugInRegistry)Activator.CreateInstance(attribute!.RegistryType)!;
        var expected = assembly.DefinedTypes.Where(type => type.GetCustomAttribute<PlugInAttribute>() != null).Select(type => type.AsType());

        Assert.That(registry.PlugIns.Select(plugIn => plugIn.Type), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that the registries register the same plugins at the same plugin points in the same order as the
    /// registration by reflection.
    /// </summary>
    [Test]
    public void RegistriesRegisterLikeReflection()
    {
        var generated = Register(useGeneratedRegistries: true);
        var reflected = Register(useGeneratedRegistries: false);

        Assert.That(generated.Manager.KnownPlugInTypes, Is.EquivalentTo(reflected.Manager.KnownPlugInTypes));
        Assert.That(generated.ActivatedPlugIns, Is.EqualTo(reflected.ActivatedPlugIns));

        var plugInPoints = reflected.Manager.KnownPlugInTypes
            .SelectMany(type => type.GetInterfaces())
            .Where(type => type.GetCustomAttribute<PlugInPointAttribute>() != null)
            .Distinct()
            .ToList();
        var registeredPlugIns = 0;
        foreach (var plugInPoint in plugInPoints)
        {
            var expected = GetActivePlugInTypes(reflected.Manager, plugInPoint);
            Assert.That(GetActivePlugInTypes(generated.Manager, plugInPoint), Is.EqualTo(expected), plugInPoint.FullName);
            registeredPlugIns += expected.Count;
        }

        Assert.That(registeredPlugIns, Is.GreaterThan(100));
        Assert.That(generated.ActivatedPlugIns, Is.Not.Empty);
        TestContext.Out.WriteLine($"{plugInPoints.Count} plugin points, {registeredPlugIns} registered plugins, {generated.ActivatedPlugIns.Count} activations.");
    }

    /// <summary>
    /// Tests that <see cref="PlugInManager.DiscoverPlugInTypes"/> finds all NPC talk plugins, like searching all types of the assembly.
    /// </summary>
    /// <summary>
    /// The plug-in manager creates plug-ins with <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>.
    /// A plug-in with two public constructors it could use is never created (the server only logs an error), so
    /// each plug-in must leave it one choice. Constructors for tests are internal.
    /// </summary>
    [Test]
    public void EveryPlugInHasOneConstructorTheManagerCanChoose()
    {
        var services = new PlainServiceProvider();
        var ambiguous = new List<string>();
        foreach (var type in PlugInAssemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type.GetCustomAttribute<PlugInAttribute>() != null && !type.IsAbstract))
        {
            try
            {
                ActivatorUtilities.CreateInstance(services, type);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Multiple constructors", StringComparison.Ordinal))
            {
                ambiguous.Add(type.FullName!);
            }
            catch (Exception)
            {
                // A plug-in which needs a service, or one whose constructor does real work, is created by the server
                // with its services; this test is only about the choice of constructor.
            }
        }

        Assert.That(ambiguous, Is.Empty);
    }

    [Test]
    public void NpcTalkPlugInsAreDiscovered()
    {
        static bool IsNpcTalkPlugIn(Type type) => type.IsSubclassOf(typeof(GameLogic.PlugIns.NpcTalkPlugInBase)) && !type.IsAbstract;
        var assembly = typeof(GameLogic.PlugIns.NpcTalkPlugInBase).Assembly;

        var expected = assembly.GetTypes().Where(IsNpcTalkPlugIn).ToList();

        Assert.That(PlugInManager.DiscoverPlugInTypes(assembly).Where(IsNpcTalkPlugIn), Is.EqualTo(expected));
        Assert.That(expected, Is.Not.Empty);
    }

    /// <summary>
    /// Tests that <see cref="PlugInConfiguration.Name"/> returns the same names with the registries as by searching the assemblies.
    /// </summary>
    [Test]
    public void PlugInConfigurationNamesAreEqual()
    {
        // Every 4th plugin, because searching the assemblies takes ~25 ms per name.
        var plugInTypes = PlugInAssemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type.GetCustomAttribute<PlugInAttribute>() != null)
            .Where((_, index) => index % 4 == 0)
            .ToList();
        var configurations = plugInTypes.Select(type => new PlugInConfiguration { TypeId = type.GUID }).ToList();

        var generated = configurations.Select(configuration => configuration.Name).ToList();
        AppContext.SetSwitch(PlugInManager.DisableGeneratedRegistriesSwitch, true);
        List<string> reflected;
        try
        {
            reflected = configurations.Select(configuration => configuration.Name).ToList();
        }
        finally
        {
            AppContext.SetSwitch(PlugInManager.DisableGeneratedRegistriesSwitch, false);
        }

        Assert.That(generated, Is.EqualTo(reflected));
        Assert.That(generated.Count(name => !Guid.TryParse(name, out _)), Is.GreaterThan(100), "Most plugins have a display name.");
    }

    private static (PlugInManager Manager, List<Type> ActivatedPlugIns) Register(bool useGeneratedRegistries)
    {
        var manager = new PlugInManager(null, NullLoggerFactory.Instance, null, null);
        var activatedPlugIns = new List<Type>();
        manager.PlugInActivated += (_, args) => activatedPlugIns.Add(args.PlugInType);

        AppContext.SetSwitch(PlugInManager.DisableGeneratedRegistriesSwitch, !useGeneratedRegistries);
        try
        {
            foreach (var assembly in PlugInAssemblies)
            {
                manager.DiscoverAndRegisterPlugIns(assembly);
            }
        }
        finally
        {
            AppContext.SetSwitch(PlugInManager.DisableGeneratedRegistriesSwitch, false);
        }

        return (manager, activatedPlugIns);
    }

    private static List<Type> GetActivePlugInTypes(PlugInManager manager, Type plugInPoint)
    {
        var plugIns = (IEnumerable<object>)typeof(PlugInManager).GetMethod(nameof(PlugInManager.GetActivePlugInsOf))!
            .MakeGenericMethod(plugInPoint)
            .Invoke(manager, [])!;
        return plugIns.Select(plugIn => plugIn.GetType()).ToList();
    }

    /// <summary>A plain service provider, as the plug-in manager's own container: no "is this a service" check.</summary>
    private sealed class PlainServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
