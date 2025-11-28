// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using IntegrationTests.Helpers;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
using Xunit.Abstractions;

namespace IntegrationTests;

public class NoCodeTests : TestHelper
{
    public NoCodeTests(ITestOutputHelper output)
        : base("NoCode", output)
    {
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void SubmitsTraces()
    {
        EnableBytecodeInstrumentation();
        EnableFileBasedConfigWithDefaultPath();
        using var collector = new MockSpansCollector(Output);
        SetFileBasedExporter(collector);

        collector.ExpectNoCode("Span-Do");

        RunTestApplication();

        collector.AssertExpectations();
    }
}

file static class NoCodeMockSpansCollectorExtensions
{
    public static void ExpectNoCode(this MockSpansCollector collector, string expectedSpanName, Span.Types.SpanKind expectedSpanKind = Span.Types.SpanKind.Internal, List<KeyValue>? expectedAttributes = null)
    {
        collector.ExpectNoCode(AssertSpan, expectedSpanName, expectedSpanKind, expectedAttributes);
    }

    public static void ExpectAsyncNoCode(this MockSpansCollector collector, string expectedSpanName, Span.Types.SpanKind expectedSpanKind = Span.Types.SpanKind.Internal, List<KeyValue>? expectedAttributes = null)
    {
        collector.ExpectNoCode(AssertAsyncSpan, expectedSpanName, expectedSpanKind, expectedAttributes);
    }

    private static void ExpectNoCode(this MockSpansCollector collector, Func<Span, string, Span.Types.SpanKind, List<KeyValue>?, bool> assert, string expectedSpanName, Span.Types.SpanKind expectedSpanKind, List<KeyValue>? expectedAttributes)
    {
        collector.Expect("OpenTelemetry.AutoInstrumentation.NoCode", x => assert(x, expectedSpanName, expectedSpanKind, expectedAttributes), GetSpanDescription(expectedSpanName, expectedSpanKind, expectedAttributes));
    }

    private static string GetSpanDescription(string expectedSpanName, Span.Types.SpanKind expectedSpanKind, List<KeyValue>? expectedAttributes)
    {
        return $"Instrumentation Scope Name: 'OpenTelemetry.AutoInstrumentation.NoCode', Span Name: '{expectedSpanName}', Span Kind: '{expectedSpanKind}', Attributes: '{(expectedAttributes != null ? string.Join(", ", expectedAttributes.Select(attr => $"{attr.Key}={attr.Value}")) : "<none>")}'";
    }

    private static bool AssertSpan(Span span, string expectedSpanName, Span.Types.SpanKind expectedSpanKind, List<KeyValue>? expectedAttributes)
    {
        expectedAttributes ??= [];

        return expectedSpanName == span.Name && expectedSpanKind == span.Kind && expectedAttributes.SequenceEqual(span.Attributes);
    }

    private static bool AssertAsyncSpan(Span span, string expectedSpanName, Span.Types.SpanKind expectedSpanKind, List<KeyValue>? expectedAttributes)
    {
        return AssertSpan(span, expectedSpanName, expectedSpanKind, expectedAttributes) && AssertSpanDuration(span);
    }

    private static bool AssertSpanDuration(Span span)
    {
        var ticks = (long)((span.EndTimeUnixNano - span.StartTimeUnixNano) / 100); // 100ns = 1 tick

        var duration = TimeSpan.FromTicks(ticks);

        return duration > TimeSpan.FromMilliseconds(98); // all async methods have a 100ms delay, need to be a bit lower (due to timer resolution)
    }
}
