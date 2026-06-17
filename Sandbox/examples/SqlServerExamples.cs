using Microsoft.Data.SqlClient;
using Sandbox.Core.Sql;

namespace Sandbox.examples
{
    public class SqlServerExamples
    {
        private const string Server = @"DESKTOP-424PEIH\SQLEXPRESS";
        private const string Database = "SandboxLinqSql";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;Encrypt=False";
        private static string DbConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;Encrypt=False";

        public static void Main()
        {
            try
            {
                Bootstrap();
                RunQueries();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Could not reach SQL Server at {Server}: {ex.Message}");
                Console.WriteLine("Is the SQLEXPRESS instance running, with TCP/IP or named pipes enabled?");
            }
        }

        private static void RunQueries()
        {
            var executor = new SqlServerExecutor(DbConnectionString);

            Console.WriteLine("=== Price > 50, cheapest first ===");
            Print(SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .OrderBy(r => r["Price"])
                .ToSql(), executor);

            Console.WriteLine("\n=== Name contains 'o', top 2 by price desc ===");
            Print(SqlQuery.From("Products")
                .Where(r => ((string)r["Name"]).Contains("o"))
                .OrderByDescending(r => r["Price"])
                .Take(2)
                .ToSql(), executor);

            Console.WriteLine("\n=== Available tools, projected to Name + Price ===");
            Print(SqlQuery.From("Products")
                .Where(r => (string)r["Category"] == "Tools" && (bool)r["IsAvailable"] == true)
                .Select(r => new { Name = r["Name"], Price = r["Price"] })
                .ToSql(), executor);
        }

        private static void Print(CompiledSql query, SqlServerExecutor executor)
        {
            Console.WriteLine($"SQL: {query.Sql}");
            var rows = executor.Execute(query);
            foreach (var row in rows)
                Console.WriteLine("  " + string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}")));
            Console.WriteLine($"  ({rows.Count} row(s))");
        }

        private static void Bootstrap()
        {
            using (var master = new SqlConnection(MasterConnectionString))
            {
                master.Open();
                ExecuteNonQuery(master, $"IF DB_ID('{Database}') IS NULL CREATE DATABASE [{Database}];");
            }

            using var db = new SqlConnection(DbConnectionString);
            db.Open();

            ExecuteNonQuery(db, """
                IF OBJECT_ID('dbo.Products', 'U') IS NULL
                CREATE TABLE [Products] (
                    [Id]            INT            NOT NULL PRIMARY KEY,
                    [Name]          NVARCHAR(100)  NOT NULL,
                    [Price]         DECIMAL(18,2)  NOT NULL,
                    [Category]      NVARCHAR(50)   NOT NULL,
                    [StockQuantity] INT            NOT NULL,
                    [IsAvailable]   BIT            NOT NULL
                );
                """);

            // Deterministic re-seed so repeated runs are idempotent.
            ExecuteNonQuery(db, "DELETE FROM [Products];");
            ExecuteNonQuery(db, """
                INSERT INTO [Products] ([Id],[Name],[Price],[Category],[StockQuantity],[IsAvailable]) VALUES
                    (1, N'Widget',   30.00, N'Tools',     12, 1),
                    (2, N'Sprocket', 10.00, N'Tools',      0, 0),
                    (3, N'Cog',      75.00, N'Tools',      5, 1),
                    (4, N'Bolt',      5.00, N'Hardware', 200, 1),
                    (5, N'Nut',       2.00, N'Hardware', 500, 1),
                    (6, N'Hammer',   55.00, N'Tools',      8, 1);
                """);

            Console.WriteLine($"Database [{Database}] ready with seeded [Products].\n");
        }

        private static void ExecuteNonQuery(SqlConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
