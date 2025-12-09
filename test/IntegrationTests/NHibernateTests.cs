// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using IntegrationTests.Helpers;
using Xunit.Abstractions;

#if NETFRAMEWORK
namespace IntegrationTests;

[Collection(SqlServerCollection.Name)]
public class NHibernateTests : TestHelper
{
    public NHibernateTests(ITestOutputHelper output)
        : base("NHibernate", output)
    {
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Containers", "Linux")]
    public void SubmitTraces()
    {
        using var collector = new MockSpansCollector(Output);
        SetExporter(collector);
        collector.Expect("OpenTelemetry.Instrumentation.SqlClient");

        SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp,console");
        SetEnvironmentVariable("OTEL_DOTNET_AUTO_SQLCLIENT_NETFX_ILREWRITE_ENABLED", "true");

        RunTestApplication(new()
        {
        });

        collector.AssertExpectations();
    }
}
#endif
