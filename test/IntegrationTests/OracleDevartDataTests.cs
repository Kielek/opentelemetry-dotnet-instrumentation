// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using IntegrationTests.Helpers;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
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

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Containers", "Linux")]
    public void SubmitTraces()
    {
        // Skip the test if fixture does not support current platform
        _oracle.SkipIfUnsupportedPlatform();

        EnableBytecodeInstrumentation();
        SetEnvironmentVariable("DEVART_ORACLE_LICENSE_KEY", "cSJFeks+2qclz/l3rQ4E9mdiNJRX0f+tJzrm/bwl5oulcEWYFX+jM7aAg11jpPZ3\r\n8NKMZ6FqHa/Ql9kqIPafyMY0nuBmYaOXxx0FnfrOR9vf3bL2Ft/4A14aJqfHnnNC\r\nnkTW+ihBpdjkiDODVZlkibOlj3AV1rp7d2iGCdADri/KnfY33BkWCeGnxTZUVhh0\r\nrRyd2LTiOwDN/FaL2VNAN4Fh9/8PALN6sOmNYPfVneZH5EG+nFEgdbW0fKiIjRtl\r\nFYq1Tm8D06nkbQfYJ+HAvNVRXSEFY/JchaT/cdQtS74=");

        using var collector = new MockSpansCollector(Output);
        SetExporter(collector);

        // Sync: ExecuteNonQuery
        collector.ExpectAdoNet(
            "INSERT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "INSERT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "INSERT INTO otel_test VALUES (?, ?)" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Sync: ExecuteScalar
        collector.ExpectAdoNet(
            "SELECT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "SELECT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "SELECT COUNT(*) FROM otel_test" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Sync: ExecuteReader
        collector.ExpectAdoNet(
            "SELECT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "SELECT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "SELECT * FROM otel_test" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Async: ExecuteNonQueryAsync
        collector.ExpectAdoNet(
            "INSERT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "INSERT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "INSERT INTO otel_test VALUES (?, ?)" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Async: ExecuteScalarAsync
        collector.ExpectAdoNet(
            "SELECT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "SELECT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "SELECT COUNT(*) FROM otel_test" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Async: ExecuteReaderAsync
        collector.ExpectAdoNet(
            "SELECT otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "SELECT otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "SELECT * FROM otel_test" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        // Cleanup method
        collector.ExpectAdoNet(
            "DROP TABLE otel_test",
            [
                new() { Key = "db.system.name", Value = new AnyValue { StringValue = "oracle" } },
                new() { Key = "db.query.summary", Value = new AnyValue { StringValue = "DROP TABLE otel_test" } },
                new() { Key = "db.query.text", Value = new AnyValue { StringValue = "DROP TABLE otel_test" } },
                new() { Key = "server.address", Value = new AnyValue { StringValue = "localhost" } }
            ]);

        RunTestApplication(new()
        {
            Arguments = $"--port {_oracle.Port} --password {_oracle.Password}",
        });

        collector.AssertExpectations();
        collector.AssertEmpty();
    }
}

file static class AdoNetMockSpansCollectorExtensions
{
    public static void ExpectAdoNet(this MockSpansCollector collector, string expectedSpanName, List<KeyValue>? expectedAttributes = null)
    {
        const string expectedScopeName = "OpenTelemetry.AutoInstrumentation.AdoNet";
        const string expectedSchemaUrl = "https://opentelemetry.io/schemas/1.40.0";
        collector.Expect(expectedScopeName, null, x => AssertSpan(x, expectedSpanName, expectedAttributes), GetSpanDescription(expectedSpanName, expectedAttributes), expectedSchemaUrl);
    }

    private static string GetSpanDescription(string expectedSpanName, List<KeyValue>? expectedAttributes)
    {
        return $"Instrumentation Scope Name: 'OpenTelemetry.AutoInstrumentation.AdoNet', Span Name: '{expectedSpanName}', Attributes: '{(expectedAttributes != null ? string.Join(", ", expectedAttributes.Select(attr => $"{attr.Key}={attr.Value}")) : "<none>")}'";
    }

    private static bool AssertSpan(Span span, string expectedSpanName, List<KeyValue>? expectedAttributes)
    {
        expectedAttributes ??= [];

        if (expectedSpanName != span.Name || Span.Types.SpanKind.Client != span.Kind)
        {
            return false;
        }

        return expectedAttributes.SequenceEqual(span.Attributes);
    }
}
