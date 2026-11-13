// <copyright file="AssemblyInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.CompilerServices;

// Authorization attributes need compile-time policy names. Keep these constants internal
// and share them only with the admin front ends and their tests.
[assembly: InternalsVisibleTo("HNMO.Web.AdminPanel")]
[assembly: InternalsVisibleTo("HNMO.Web.AdminPanel.Mcp")]
[assembly: InternalsVisibleTo("HNMO.Web.Tests")]
