﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the InstrumentationDefinitionsGenerator tool. To safely
//     modify this file, edit InstrumentMethodAttribute on the classes and
//     compile project.

//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated. 
// </auto-generated>
//------------------------------------------------------------------------------

using OpenTelemetry.AutoInstrumentation.Configurations;

namespace OpenTelemetry.AutoInstrumentation;

internal static partial class InstrumentationDefinitions
{
    private static NativeCallTargetDefinition[] GetDerivedDefinitionsArray()
    {
        var nativeCallTargetDefinitions = new List<NativeCallTargetDefinition>(2);
        // Traces
        var tracerSettings = Instrumentation.TracerSettings.Value;
        if (tracerSettings.TracesEnabled)
        {
            // RabbitMq
            if (tracerSettings.EnabledInstrumentations.Contains(TracerInstrumentation.RabbitMq))
            {
                nativeCallTargetDefinitions.Add(new("RabbitMQ.Client", "RabbitMQ.Client.AsyncDefaultBasicConsumer", "HandleBasicDeliver", new[] {"System.Threading.Tasks.Task", "System.String", "System.UInt64", "System.Boolean", "System.String", "System.String", "RabbitMQ.Client.IBasicProperties", "System.ReadOnlyMemory`1[System.Byte]"}, 6, 0, 0, 6, 65535, 65535, AssemblyFullName, "OpenTelemetry.AutoInstrumentation.Instrumentations.RabbitMq6.Integrations.AsyncDefaultBasicConsumerIntegration"));
                nativeCallTargetDefinitions.Add(new("RabbitMQ.Client", "RabbitMQ.Client.DefaultBasicConsumer", "HandleBasicDeliver", new[] {"System.Void", "System.String", "System.UInt64", "System.Boolean", "System.String", "System.String", "RabbitMQ.Client.IBasicProperties", "System.ReadOnlyMemory`1[System.Byte]"}, 6, 0, 0, 6, 65535, 65535, AssemblyFullName, "OpenTelemetry.AutoInstrumentation.Instrumentations.RabbitMq6.Integrations.DefaultBasicConsumerIntegration"));
            }
        }

        return nativeCallTargetDefinitions.ToArray();
    }
}