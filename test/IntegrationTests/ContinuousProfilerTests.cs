// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER

using IntegrationTests.Helpers;
using OpenTelemetry.Proto.Profiles.V1Experimental;
using Xunit.Abstractions;

namespace IntegrationTests;

public class ContinuousProfilerTests : TestHelper
{
    public ContinuousProfilerTests(ITestOutputHelper output)
        : base("ContinuousProfiler", output)
    {
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void ExportAllocationSamples()
    {
        EnableBytecodeInstrumentation();
        using var collector = new MockProfilesCollector(Output);
        SetExporter(collector);
        SetEnvironmentVariable("OTLP_DOTNET_AUTO_CONTINUOUS_PROFILER_ALLOCATION_SAMPLING_ENABLED", "true");
        SetEnvironmentVariable("OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES", "TestApplication.ContinuousProfiler");
        RunTestApplication();

        collector.Expect(profileData => profileData.ResourceProfiles.Any(resourceProfiles => resourceProfiles.ScopeProfiles.Any(scopeProfile => scopeProfile.Profiles.Any(profileContainer => ContainAttributes(profileContainer, "allocation") && profileContainer.Profile.Sample[0].Value[0] != 0.0))));
        collector.ResourceExpector.Expect("todo.resource.detector.key", "todo.resource.detector.value");

        collector.AssertExpectations();
        collector.ResourceExpector.AssertExpectations();
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void ExportThreadSamples()
    {
        EnableBytecodeInstrumentation();
        using var collector = new MockProfilesCollector(Output);
        SetExporter(collector);
        SetEnvironmentVariable("OTLP_DOTNET_AUTO_CONTINUOUS_PROFILER_THREAD_SAMPLING_ENABLED", "true");
        SetEnvironmentVariable("OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES", "TestApplication.ContinuousProfiler");
        RunTestApplication();

        var expectedStackTrace = string.Join("\n", CreateExpectedStackTrace());

        collector.Expect(profileData => profileData.ResourceProfiles.Any(resourceProfiles => resourceProfiles.ScopeProfiles.Any(scopeProfile => scopeProfile.Profiles.Any(profileContainer => ContainStackTraceForClassHierarchy(profileContainer.Profile, expectedStackTrace) && ContainAttributes(profileContainer, "cpu")))));
        collector.ResourceExpector.Expect("todo.resource.detector.key", "todo.resource.detector.value");

        collector.AssertExpectations();
        collector.ResourceExpector.AssertExpectations();
    }

    private static bool ContainAttributes(ProfileContainer profileContainer, string profilingDataType)
    {
        return profileContainer.Attributes.Any(x => x.Key == "todo.profiling.data.type" && x.Value.StringValue == profilingDataType);
    }

    private static List<string> CreateExpectedStackTrace()
    {
        var stackTrace = new List<string>
        {
            "System.Threading.Thread.Sleep(System.TimeSpan)",
            "TestApplication.ContinuousProfiler.Fs.ClassFs.methodFs(System.String)",
            "TestApplication.ContinuousProfiler.Vb.ClassVb.MethodVb(System.String)",
            "My.Custom.Test.Namespace.TestDynamicClass.TryInvoke(System.Dynamic.InvokeBinder, System.Object[], System.Object\u0026)",
            "System.Dynamic.UpdateDelegates.UpdateAndExecuteVoid3[T0, T1, T2](System.Runtime.CompilerServices.CallSite, T0, T1, T2)",
            "My.Custom.Test.Namespace.ClassENonStandardCharacters\u0104\u0118\u00D3\u0141\u017B\u0179\u0106\u0105\u0119\u00F3\u0142\u017C\u017A\u015B\u0107\u011C\u0416\u13F3\u2CC4\u02A4\u01CB\u2093\u06BF\u0B1F\u0D10\u1250\u3023\u203F\u0A6E\u1FAD_\u00601.GenericMethodDFromGenericClass[TMethod, TMethod2](TClass, TMethod, TMethod2)",
            "My.Custom.Test.Namespace.ClassD`21.MethodD(T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19, T20, Unknown)",
            "My.Custom.Test.Namespace.GenericClassC`1.GenericMethodCFromGenericClass[T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19, T20](T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19, T20, Unknown)",
            "My.Custom.Test.Namespace.GenericClassC`1.GenericMethodCFromGenericClass(T)"
        };

#if DEBUG
        stackTrace.Add("Unknown_Native_Function(unknown)");
#else
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            stackTrace.Add("Unknown_Native_Function(unknown)");
        }
#endif
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.InternalClassB`2.DoubleInternalClassB.TripleInternalClassB`1.MethodB[TB](System.Int32, TC[], TB, TD, System.Collections.Generic.IList`1[TA], System.Collections.Generic.IList`1[System.String])");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.<MethodAOthers>g__Action|7_0[T](System.Int32)");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodAOthers[T](System.String, System.Object, My.Custom.Test.Namespace.CustomClass, My.Custom.Test.Namespace.CustomStruct, My.Custom.Test.Namespace.CustomClass[], My.Custom.Test.Namespace.CustomStruct[], System.Collections.Generic.List`1[T])");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodAPointer(System.Int32*)");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodAFloats(System.Single, System.Double)");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodAInts(System.UInt16, System.Int16, System.UInt32, System.Int32, System.UInt64, System.Int64, System.IntPtr, System.UIntPtr)");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodABytes(System.Boolean, System.Char, System.SByte, System.Byte)");
        stackTrace.Add("My.Custom.Test.Namespace.ClassA.MethodA()");

        return stackTrace;
    }

    private bool ContainStackTraceForClassHierarchy(Profile profile, string expectedStackTrace)
    {
        var frames = profile.Location
            .SelectMany(location => location.Line)
            .Select(line => line.FunctionIndex)
            .Select(functionId => profile.Function[(int)functionId - 1])
            .Select(function => profile.StringTable[(int)function.Name]);

        var stackTrace = string.Join("\n", frames);

        return stackTrace.Contains(expectedStackTrace);
    }
}
#endif
