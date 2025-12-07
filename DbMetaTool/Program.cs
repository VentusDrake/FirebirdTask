using FirebirdSql.Data.FirebirdClient;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

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
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

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
            int idx = Array.IndexOf(args, name);
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

            if (string.IsNullOrWhiteSpace(databaseDirectory))
                throw new ArgumentException("Brak ścieżki katalogu bazy danych.", nameof(databaseDirectory));

            if (string.IsNullOrWhiteSpace(scriptsDirectory))
                throw new ArgumentException("Brak ścieżki katalogu skryptów.", nameof(scriptsDirectory));

            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Katalog skryptów nie istnieje: {scriptsDirectory}");

            var dbPath = Path.Combine(databaseDirectory, "FBTASKGENERATED.fdb");
            if (File.Exists(dbPath))
                throw new InvalidOperationException($"Plik bazy już istnieje: {dbPath}");

            var json = File.ReadAllText("config.json");
            using var doc = JsonDocument.Parse(json);

            string ConfigUser = doc.RootElement.GetProperty("SYSDBA_USER").GetString();
            string ConfigPassword = doc.RootElement.GetProperty("SYSDBA_PASSWORD").GetString();

            Console.WriteLine($"[Build-db] Tworzenie bazy danych: {dbPath}");

            var csb = new FbConnectionStringBuilder {
                Database = dbPath,
                UserID = ConfigUser,
                Password = ConfigPassword,
                DataSource = "localhost",
                Charset = "UTF8",
                ServerType = FbServerType.Default
            };
            csb["WireCrypt"] = "Enabled";

            var connectionString = csb.ToString();

            FbConnection.CreateDatabase(connectionString);

            Console.WriteLine("[Build-db] Baza utworzona pomyślnie.");

            using var conn = new FbConnection(connectionString);
            conn.Open();

            var errors = new List<string>();

            BuildingDBUtils.ExecuteScriptIfExists(conn, Path.Combine(scriptsDirectory, "domains.sql"), "domains.sql", errors);
            BuildingDBUtils.ExecuteScriptIfExists(conn, Path.Combine(scriptsDirectory, "tables.sql"), "tables.sql", errors);
            //BuildingDBUtils.ExecuteScriptIfExists(conn, Path.Combine(scriptsDirectory, "procedures.sql"), "procedures.sql", errors);

            Console.WriteLine();
            Console.WriteLine("===== Raport BuildDatabase =====");
            Console.WriteLine($"Ścieżka bazy: {dbPath}");
            Console.WriteLine($"Katalog skryptów: {scriptsDirectory}");

            if (errors.Count == 0) {
                Console.WriteLine("Wszystkie skrypty wykonane pomyślnie.");
            } else {
                Console.WriteLine("Wystąpiły błędy podczas wykonywania skryptów:");
                foreach (var e in errors)
                    Console.WriteLine(" - " + e);
            }
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
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using var conn = new FbConnection(connectionString);
            conn.Open();

            var domains = MetaDataUtils.ExportDomains(conn);

            var tables = MetaDataUtils.ExportTables(conn);

            var procedures = MetaDataUtils.ExportProcedures(conn);

            File.WriteAllLines(Path.Combine(outputDirectory, "domains.sql"), domains, Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outputDirectory, "tables.sql"), tables, Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outputDirectory, "procedures.sql"), procedures, Encoding.UTF8);

            var jsonMeta = new {
                Domains = domains,
                Tables = tables,
                Procedures = procedures
            };

            File.WriteAllText(
                Path.Combine(outputDirectory, "metadata.json"),
                JsonSerializer.Serialize(jsonMeta, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8
            );
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory) {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.
            throw new NotImplementedException();
        }        
    }
}
