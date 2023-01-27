// <copyright file="TransientDependenciesTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

// This test is defined in NET7.0 because the tool is written in .NET 7.0
// The actual test is testing .NET 462 context.
#if NET7_0_OR_GREATER

using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using DependencyListGenerator;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace OpenTelemetry.AutoInstrumentation.Tests;

public class TransientDependenciesTests
{
    [SkippableFact]
    public void DefinedTransientDeps_Are_MatchingGeneratedDeps()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Supported only on Windows.");

        var currentTestLocation = Assembly.GetExecutingAssembly().Location;
        var testDir = FindParentDir(currentTestLocation, "test");
        var codeDir = Path.Combine(Directory.GetParent(testDir)!.FullName, "src", "OpenTelemetry.AutoInstrumentation");
        var projectPath = Path.Combine(codeDir, "OpenTelemetry.AutoInstrumentation.csproj");
        var projectGenPath = Path.Combine(codeDir, "OpenTelemetry.AutoInstrumentation.g.csproj");

        File.Copy(projectPath, projectGenPath, overwrite: true);

        var deps = ReadTransientDeps(projectGenPath);

        CleanTransientDeps(projectGenPath);

        var generatedDeps = Generator
            .EnumerateDependencies(projectGenPath)
            .Select(x => x.Name)
            .ToList();

        File.Delete(projectGenPath);

        using (new AssertionScope())
        {
            deps.Count.Should().Be(generatedDeps.Count);
            deps.Should().BeEquivalentTo(generatedDeps);
        }
    }

    private static XElement? GetTransientDepsGroup(XElement projXml)
    {
        const string label = "Transient dependencies auto-generated by GenerateNetFxTransientDependencies";

        return projXml
            .Elements("ItemGroup")
            .FirstOrDefault(x =>
                x.HasAttributes &&
                x.Attribute("Label")?.Value == label);
    }

    private static void CleanTransientDeps(string projPath)
    {
        var projXml = XElement.Load(projPath);
        var depsGroup = GetTransientDepsGroup(projXml);
        if (depsGroup != null)
        {
            depsGroup.Remove();
        }

        projXml.Save(projPath);
    }

    private static ICollection<string> ReadTransientDeps(string projPath)
    {
        var projXml = XElement.Load(projPath);
        var depsGroup = GetTransientDepsGroup(projXml);
        if (depsGroup == null)
        {
            return Array.Empty<string>();
        }

        return depsGroup
            .Descendants("PackageReference")
            .Where(x => x.HasAttributes && x.Attribute("Include") != null)
            .Select(x => x.Attribute("Include")!.Value)
            .ToList();
    }

    private static string FindParentDir(string location, string parentName)
    {
        var parent = Directory.GetParent(location);
        if (parent == null)
        {
            throw new InvalidOperationException("Could not find parent test directory");
        }

        if (parent.Name == parentName)
        {
            return parent.FullName;
        }

        return FindParentDir(parent.FullName, parentName);
    }
}

#endif