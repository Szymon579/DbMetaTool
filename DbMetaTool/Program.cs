using System;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");

                Console.ReadLine();
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                    {
                        var dbDir = GetArgValue(args, "--db-dir");
                        var scriptsDir = GetArgValue(args, "--scripts-dir");

                        BuildDatabase(dbDir, scriptsDir);
                        Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                        return 0;
                    }

                    case "export-scripts":
                    {
                        var connStr = GetArgValue(args, "--connection-string");
                        var outputDir = GetArgValue(args, "--output-dir");

                        ExportScripts(connStr, outputDir);
                        Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                        return 0;
                    }

                    case "update-db":
                    {
                        var connStr = GetArgValue(args, "--connection-string");
                        var scriptsDir = GetArgValue(args, "--scripts-dir");

                        UpdateDatabase(connStr, scriptsDir);
                        Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                        return 0;
                    }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            var idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            // TODO:
            // 1) Utwórz pustą bazę danych FB 5.0 w katalogu databaseDirectory.
            // 2) Wczytaj i wykonaj kolejno skrypty z katalogu scriptsDirectory
            //    (tylko domeny, tabele, procedury).
            // 3) Obsłuż błędy i wyświetl raport.

            DbHelper.BuildDatabase(databaseDirectory, scriptsDirectory);
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Pobierz metadane domen, tabel (z kolumnami) i procedur.
            // 3) Wygeneruj pliki .sql / .json / .txt w outputDirectory.

            DbHelper.ExportScripts(connectionString, outputDirectory);
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.

            DbHelper.UpdateDatabase(connectionString, scriptsDirectory);
        }
    }
    
    public static class DbHelper
    {
        /// <summary>
        /// Creates new database and executes specified script on it.
        /// </summary>
        /// <param name="databaseDirectory">Directory in which the database will be created.</param>
        /// <param name="scriptsDirectory">Directory containing SQL script that will be executed on created database.</param>
        /// <exception cref="Exception">Throws if error during database creation occurs.</exception>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            Console.WriteLine($"Creating new database in: {databaseDirectory}...");
            Directory.CreateDirectory(databaseDirectory);

            var dbPath = Path.Combine(databaseDirectory, "database_1.fdb");
            var builder = new FbConnectionStringBuilder
            {
                UserID = "SYSDBA",
                Password = "root",
                Database = dbPath,
                DataSource = "localhost",
                Port = 3050,
                ServerType = FbServerType.Default,
                Charset = "UTF8",
                ClientLibrary = "fbclient.dll"
            };

            try
            {
                FbConnection.CreateDatabase(builder.ToString(), pageSize: 8192, forcedWrites: true, overwrite: true);
                Console.WriteLine("Database created.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during database file creation: {ex.Message}", ex);
            }

            UpdateDatabase(builder.ToString(), scriptsDirectory);
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            Console.WriteLine("Generating script files...");

            var metadataService = new MetadataRetrievalService(connectionString);
            var domains = metadataService.GetDomains();
            var tables = metadataService.GetTables();
            var procedures = metadataService.GetProcedures();

            var sb = new StringBuilder();
            sb.AppendLine("/* --- GENERATED METADATA SCRIPT --- */");

            DDLScriptBuilder.BuildDomains(domains, sb);
            DDLScriptBuilder.BuildTables(tables, sb);
            DDLScriptBuilder.BuildProcedures(procedures, sb);

            Directory.CreateDirectory(outputDirectory);
            var filePath = Path.Combine(outputDirectory, "schema.sql");
            File.WriteAllText(filePath, sb.ToString());

            Console.WriteLine($"{filePath} created.");
        }

        /// <summary>
        /// Updates existing database specified in connection string with provided script.
        /// </summary>
        /// <param name="connectionString">Connection string to the database.</param>
        /// <param name="scriptsDirectory">Directory with script to execute.</param>
        /// <remarks>This method implements an all or nothing approach, by wrapping all executed commands into a transaction,
        /// which is rolled back if anything fails.</remarks>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            Console.WriteLine("Updating database...");

            var scriptContent = GetScriptFromDirectoryContent(scriptsDirectory);
            var commands = SplitScript(scriptContent);
            ExecuteCommandsAsTransaction(connectionString, commands);

            Console.WriteLine("Database updated.");
        }

        /// <summary>
        /// Gets the first sql script from the specified directory.
        /// </summary>
        /// <param name="scriptsDirectory">Directory to search.</param>
        /// <returns>Contents of sql file.</returns>
        private static string GetScriptFromDirectoryContent(string scriptsDirectory)
        {
            // get first sql file from specified directory
            var fileName = Directory.GetFiles(scriptsDirectory, "*.sql").FirstOrDefault();
            if (fileName == null)
            {
                throw new FileNotFoundException($"No .sql file found in {scriptsDirectory}");
            }

            var scriptPath = Path.Combine(scriptsDirectory, fileName);
            string scriptContent;
            try
            {
                scriptContent = File.ReadAllText(scriptPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not read file {scriptPath}: {ex.Message}", ex);
            }

            return scriptContent;
        }

        /// <summary>
        /// Parses a SQL script and splits it into individual executable commands, 
        /// while correctly handling Firebird's <c>SET TERM</c> delimiter logic.
        /// </summary>
        /// <param name="script">Script to split.</param>
        private static List<string> SplitScript(string script)
        {
            var commands = new List<string>();
            var currentCommand = new StringBuilder();
            var currentTerminator = ";";

            using var reader = new StringReader(script);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmedLine = line.Trim();

                // handle terminator change
                if (trimmedLine.StartsWith("SET TERM", StringComparison.OrdinalIgnoreCase))
                {
                    if (trimmedLine.Contains('^') && !trimmedLine.Contains(';')) currentTerminator = "^";
                    else if (trimmedLine.Contains("^ ;")) currentTerminator = "^";
                    else if (trimmedLine.Contains("; ^")) currentTerminator = ";";

                    continue;
                }

                currentCommand.AppendLine(line);

                if (currentCommand.ToString().TrimEnd().EndsWith(currentTerminator))
                {
                    var cmdText = currentCommand.ToString().Trim();
                    cmdText = cmdText.Substring(0, cmdText.LastIndexOf(currentTerminator));

                    if (!string.IsNullOrWhiteSpace(cmdText))
                    {
                        commands.Add(cmdText.Trim());
                    }

                    currentCommand.Clear();
                }
            }

            return commands;
        }

        private static void ExecuteCommandsAsTransaction(string connectionString, List<string> commands)
        {
            using var conn = new FbConnection(connectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (var sql in commands)
                {
                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        continue;
                    }

                    // ignore all COMMIT / ROLLBACK commands, we are in charge of transaction handling
                    var normalized = sql.Trim().ToUpperInvariant();
                    if (normalized is "COMMIT" or "ROLLBACK")
                    {
                        continue;
                    }

                    using var cmd = new FbCommand(sql, conn, transaction);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                Console.WriteLine("Transaction commited.");
            }
            catch (Exception ex)
            {
                // any error occured, rollback all changes
                try
                {
                    Console.WriteLine($"Error occured during database update: {ex.Message}");
                    Console.WriteLine("Restoring previous database state...");
                    transaction.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine($"Error during rollback: {rollbackEx.Message}");
                }

                throw;
            }
        }
    }
}