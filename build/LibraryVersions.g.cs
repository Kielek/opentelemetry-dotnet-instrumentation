//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the LibraryVersionsGenerator tool. To safely
//     modify this file, edit PackageVersionDefinitions.cs and
//     re-run the LibraryVersionsGenerator project in Visual Studio.
// 
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated. 
// </auto-generated>
//------------------------------------------------------------------------------

using Models;

public static class LibraryVersion
{
    public static IReadOnlyDictionary<string, IReadOnlyCollection<PackageBuildInfo>> Versions = new Dictionary<string, IReadOnlyCollection<PackageBuildInfo>>
    {
        {
            "TestApplication.Azure",
            new List<PackageBuildInfo>
            {
                new("12.13.0", Array.Empty<string>()),
                new("12.19.1", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.Elasticsearch",
            new List<PackageBuildInfo>
            {
                new("8.0.0", Array.Empty<string>()),
                new("8.10.0", Array.Empty<string>()),
                new("8.12.0", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.EntityFrameworkCore",
            new List<PackageBuildInfo>
            {
                new("6.0.27", Array.Empty<string>()),
                new("7.0.16", Array.Empty<string>()),
                new("8.0.2", new string[] {"net8.0"}),
            }
        },
        {
            "TestApplication.EntityFrameworkCore.Pomelo.MySql",
            new List<PackageBuildInfo>
            {
                new("6.0.2", Array.Empty<string>()),
                new("7.0.0", Array.Empty<string>()),
                new("8.0.0", new string[] {"net8.0"}),
            }
        },
        {
            "TestApplication.GraphQL",
            new List<PackageBuildInfo>
            {
                new("7.5.0", Array.Empty<string>(), new() {{"GraphQLMicrosoftDI","7.5.0"},{"GraphQLServerTransportsAspNetCore","7.5.0"},{"GraphQLServerUIPlayground","7.5.0"}}),
                new("7.8.0", Array.Empty<string>(), new() {{"GraphQLMicrosoftDI","7.8.0"},{"GraphQLServerTransportsAspNetCore","7.7.0"},{"GraphQLServerUIPlayground","7.7.0"}}),
            }
        },
        {
            "TestApplication.GrpcNetClient",
            new List<PackageBuildInfo>
            {
                new("2.52.0", Array.Empty<string>()),
                new("2.60.0", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.MassTransit",
            new List<PackageBuildInfo>
            {
                new("8.0.0", Array.Empty<string>()),
                new("8.1.3", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.SqlClient.Microsoft",
            new List<PackageBuildInfo>
            {
                new("2.1.7", Array.Empty<string>()),
                new("3.1.5", Array.Empty<string>()),
                new("4.0.5", Array.Empty<string>()),
                new("5.1.5", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.SqlClient.System",
            new List<PackageBuildInfo>
            {
                new("4.8.6", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.MongoDB",
            new List<PackageBuildInfo>
            {
                new("2.19.0", Array.Empty<string>()),
                new("2.23.1", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.MySqlConnector",
            new List<PackageBuildInfo>
            {
                new("2.0.0", Array.Empty<string>()),
                new("2.3.5", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.MySqlData",
            new List<PackageBuildInfo>
            {
                new("8.1.0", Array.Empty<string>()),
                new("8.3.0", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.Npgsql",
            new List<PackageBuildInfo>
            {
                new("6.0.0", Array.Empty<string>()),
                new("8.0.2", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.NServiceBus",
            new List<PackageBuildInfo>
            {
                new("8.0.0", Array.Empty<string>()),
                new("8.1.6", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.Quartz",
            new List<PackageBuildInfo>
            {
                new("3.4.0", Array.Empty<string>()),
                new("3.8.0", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.StackExchangeRedis",
            new List<PackageBuildInfo>
            {
                new("2.0.495", Array.Empty<string>()),
                new("2.1.50", Array.Empty<string>()),
                new("2.5.61", Array.Empty<string>()),
                new("2.6.66", Array.Empty<string>()),
                new("2.7.17", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.Wcf.Client.DotNet",
            new List<PackageBuildInfo>
            {
                new("4.10.2", Array.Empty<string>()),
                new("6.2.0", Array.Empty<string>()),
            }
        },
        {
            "TestApplication.Kafka",
            new List<PackageBuildInfo>
            {
                new("1.4.0", Array.Empty<string>()),
                new("2.3.0", Array.Empty<string>()),
            }
        },
    };
}
