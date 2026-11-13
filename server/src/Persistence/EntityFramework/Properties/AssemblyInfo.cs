// <copyright file="AssemblyInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("HNMO.Persistence.EntityFramework")]
[assembly: InternalsVisibleTo("HNMO.Persistence.Initialization.Tests")]
[assembly: InternalsVisibleTo("HNMO.Tests")]
[assembly: InternalsVisibleTo("HNMO.Persistence.EntityFramework.DesignTime")]
[assembly: InternalsVisibleTo("HNMO.Measurements")]
[assembly: HNMO.Persistence.GeneratePersistentTypeRegistry("HNMO.Persistence.EntityFramework.Model")]
