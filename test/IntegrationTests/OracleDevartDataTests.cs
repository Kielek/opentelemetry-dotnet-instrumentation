// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using IntegrationTests.Helpers;
using Xunit.Abstractions;

namespace IntegrationTests;

[Collection(OracleCollectionFixture.Name)]
public class OracleDevartDataTests : TestHelper
{
    private readonly OracleFixture _oracle;

    public OracleDevartDataTests(ITestOutputHelper output, OracleFixture oracle)
        : base("Devart.Oracle", output)
    {
        _oracle = oracle;
    }

    public static TheoryData<string, bool> TestData()
    {
        var theoryData = new TheoryData<string, bool>();

#if NETFRAMEWORK
        foreach (var version in LibraryVersion.OracleMda)
#else
        foreach (var version in LibraryVersion.GetPlatformVersions(nameof(LibraryVersion.OracleMdaCore)))
#endif
        {
            theoryData.Add(version, true);
            theoryData.Add(version, false);
        }

        return theoryData;
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("Containers", "Linux")]
    [MemberData(nameof(TestData))]
    public void SubmitTraces(string packageVersion, bool dbStatementForText)
    {
        // Skip the test if fixture does not support current platform
        _oracle.SkipIfUnsupportedPlatform();

        SetEnvironmentVariable("OTEL_DOTNET_AUTO_ORACLEMDA_SET_DBSTATEMENT_FOR_TEXT", dbStatementForText.ToString());

        using var collector = new MockSpansCollector(Output);
        SetExporter(collector);

#if  NETFRAMEWORK
        const string instrumentationScopeName = "Oracle.ManagedDataAccess";
#else
        const string instrumentationScopeName = "Oracle.ManagedDataAccess.Core";
#endif

        if (dbStatementForText)
        {
            collector.Expect(instrumentationScopeName, span => span.Attributes.Any(attr => attr.Key == "db.statement" && !string.IsNullOrWhiteSpace(attr.Value?.StringValue)));
        }
        else
        {
            collector.Expect(instrumentationScopeName, span => span.Attributes.All(attr => attr.Key != "db.statement"));
        }

        RunTestApplication(new()
        {
            Arguments = $"--port {_oracle.Port} --password {_oracle.Password}",
            PackageVersion = packageVersion
        });

        collector.AssertExpectations();
    }
}
