using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbMetaTool {
    public class BuildingDBUtils {
        public static void ExecuteScriptIfExists(FbConnection conn, string path, string nameForLog, List<string> errors) {
            if (!File.Exists(path)) {
                Console.WriteLine($"[Build-db] Plik {nameForLog} nie istnieje – pomijam.");
                return;
            }

            Console.WriteLine($"[Build-db] Wykonywanie {nameForLog}...");

            var sql = File.ReadAllText(path, Encoding.UTF8);
            var statements = SplitSqlStatements(sql);

            using var tx = conn.BeginTransaction();

            try {
                foreach (var stmt in statements) {
                    using var cmd = new FbCommand(stmt, conn, tx);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                Console.WriteLine($"[Build-db] {nameForLog} – OK");
            } catch (Exception ex) {
                tx.Rollback();
                var msg = $"{nameForLog}: {ex.Message}";
                Console.WriteLine($"[Build-db] BŁĄD: {msg}");
                errors.Add(msg);
            }
        }

        private static List<string> SplitSqlStatements(string script) {
            var result = new List<string>();
            var sb = new StringBuilder();

            using var reader = new StringReader(script);
            string? line;

            while ((line = reader.ReadLine()) != null) {
                // pomijamy puste linie i komentarze
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("--")) {
                    sb.AppendLine(line);
                    continue;
                }

                sb.AppendLine(line);

                if (line.TrimEnd().EndsWith(";")) {
                    var statement = sb.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(statement)) {
                        // usuwamy końcowy ';'
                        if (statement.EndsWith(";"))
                            statement = statement.Substring(0, statement.Length - 1);

                        if (!string.IsNullOrWhiteSpace(statement))
                            result.Add(statement);
                    }

                    sb.Clear();
                }
            }

            // Gdyby coś jeszcze zostało bez ';'
            var rest = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(rest))
                result.Add(rest);

            return result;
        }
    }
}
