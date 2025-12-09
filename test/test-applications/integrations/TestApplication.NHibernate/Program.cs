// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;

namespace TestApplication.NHibernate;

public class Program
{
    private const string DatabaseName = "NHibernateTestDb";
    private const string ConnectionStringMaster = "Server=localhost,1433;Database=master;User Id=sa;Password=YourPassword123;Encrypt=false;";
    private const string ConnectionString = "Server=localhost,1433;Database=NHibernateTestDb;User Id=sa;Password=YourPassword123;Encrypt=false;";

    public static void Main(string[] args)
    {
        Shared.ConsoleHelper.WriteSplashScreen(args);
        try
        {
            Console.WriteLine("Initializing NHibernate...");

            // Ensure database exists
            EnsureDatabaseExists();

            // Configure NHibernate
            var cfg = new Configuration();
            cfg.DataBaseIntegration(db =>
            {
                // Connection string for SQL Server running in Docker
                // Make sure your Docker container is running on localhost:1433
                db.ConnectionString = ConnectionString;
                db.Dialect<global::NHibernate.Dialect.MsSql2012Dialect>();
                db.Driver<global::NHibernate.Driver.SqlClientDriver>();
                db.Timeout = 15;
            });

            // Add assembly to scan for mapping files
            cfg.AddAssembly(Assembly.GetExecutingAssembly());

            Console.WriteLine("Creating database schema...");

            // Create/update schema
            var schemaExport = new SchemaExport(cfg);
            schemaExport.Create(false, true);

            Console.WriteLine("Building session factory...");

            // Build session factory
            using (ISessionFactory sessionFactory = cfg.BuildSessionFactory())
            {
                // Test 1: Insert a person
                Console.WriteLine("\n--- Test 1: Inserting Person ---");
                using (ISession session = sessionFactory.OpenSession())
                using (ITransaction tx = session.BeginTransaction())
                {
                    var person = new Person
                    {
                        Name = "John Doe",
                        Age = 30
                    };
                    session.Save(person);
                    tx.Commit();
                    Console.WriteLine($"✓ Person saved with Id: {person.Id}");
                }

                // Test 2: Query persons
                Console.WriteLine("\n--- Test 2: Querying Persons ---");
                using (ISession session = sessionFactory.OpenSession())
                {
                    var persons = session.QueryOver<Person>().List();
                    Console.WriteLine($"✓ Found {persons.Count} person(s):");
                    foreach (var p in persons)
                    {
                        Console.WriteLine($"  - Id: {p.Id}, Name: {p.Name}, Age: {p.Age}");
                    }
                }

                // Test 3: Update a person
                Console.WriteLine("\n--- Test 3: Updating Person ---");
                using (ISession session = sessionFactory.OpenSession())
                using (ITransaction tx = session.BeginTransaction())
                {
                    var person = session.Get<Person>(1);
                    if (person != null)
                    {
                        person.Age = 31;
                        session.Update(person);
                        tx.Commit();
                        Console.WriteLine($"✓ Person updated - Age changed to {person.Age}");
                    }
                }

                // Test 4: Delete a person
                Console.WriteLine("\n--- Test 4: Deleting Person ---");
                using (ISession session = sessionFactory.OpenSession())
                using (ITransaction tx = session.BeginTransaction())
                {
                    var person = session.Get<Person>(1);
                    if (person != null)
                    {
                        session.Delete(person);
                        tx.Commit();
                        Console.WriteLine($"✓ Person deleted");
                    }
                }
            }

            Console.WriteLine("\n✓ All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void EnsureDatabaseExists()
    {
        try
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionStringMaster))
            {
                connection.Open();

                // Check if database exists
                string checkDbQuery = $"SELECT database_id FROM sys.databases WHERE name = N'{DatabaseName}';";
                using (var command = new System.Data.SqlClient.SqlCommand(checkDbQuery, connection))
                {
                    var result = command.ExecuteScalar();
                    if (result == null)
                    {
                        // Database doesn't exist, create it
                        Console.WriteLine($"Creating database '{DatabaseName}'...");
                        string createDbQuery = $"CREATE DATABASE [{DatabaseName}];";
                        using (var createCommand = new System.Data.SqlClient.SqlCommand(createDbQuery, connection))
                        {
                            createCommand.ExecuteNonQuery();
                            Console.WriteLine($"✓ Database '{DatabaseName}' created successfully.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"✓ Database '{DatabaseName}' already exists.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error ensuring database exists: {ex.Message}");
            throw;
        }
    }
}
