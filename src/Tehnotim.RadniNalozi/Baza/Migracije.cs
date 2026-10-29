using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Tehnotim.RadniNalozi.Baza;

/// <summary>
/// Pri pokretanju aplikacije: napravi bazu ako ne postoji i izvrši SQL skripte iz foldera
/// Baza/Skripte koje još nisu izvršene (redom po imenu fajla). Izvršene skripte se pamte
/// u tabeli dbo.SemaVerzija. Svaka skripta ide u jednoj transakciji: ili cijela, ili ništa.
/// Pravilo: izvršena skripta se više nikad ne mijenja; svaka izmjena baze je nova skripta.
/// </summary>
public static class Migracije
{
    public static async Task<List<string>> PrimijeniAsync(string connectionString, ILogger logger)
    {
        var veza = new SqlConnectionStringBuilder(connectionString);
        var imeBaze = veza.InitialCatalog;
        if (string.IsNullOrWhiteSpace(imeBaze) || imeBaze.Equals("master", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("U vezi s bazom (ConnectionStrings:Baza) treba da piše Database=RadniNalozi.");

        // 1. Baza
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using (var conn = new SqlConnection(master.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "IF DB_ID(@ime) IS NULL BEGIN DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@ime); EXEC(@sql); END",
                conn);
            cmd.Parameters.AddWithValue("@ime", imeBaze);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Tabela s izvršenim skriptama
        await using var db = new SqlConnection(connectionString);
        await db.OpenAsync();
        await using (var cmd = new SqlCommand(
            "IF OBJECT_ID(N'dbo.SemaVerzija') IS NULL CREATE TABLE dbo.SemaVerzija (Skripta nvarchar(200) NOT NULL PRIMARY KEY, Primijenjeno datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME());",
            db))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var izvrsene = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new SqlCommand("SELECT Skripta FROM dbo.SemaVerzija", db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) izvrsene.Add(r.GetString(0));
        }

        // 3. Nove skripte
        var folder = Path.Combine(AppContext.BaseDirectory, "Baza", "Skripte");
        var skripte = Directory.GetFiles(folder, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .ToList();

        var novoIzvrsene = new List<string>();
        foreach (var putanja in skripte)
        {
            var ime = Path.GetFileName(putanja);
            if (izvrsene.Contains(ime)) continue;

            var tekst = await File.ReadAllTextAsync(putanja);
            var dijelovi = Regex.Split(tekst, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
            try
            {
                foreach (var dio in dijelovi)
                {
                    await using var cmd = new SqlCommand(dio, db, tx) { CommandTimeout = 120 };
                    await cmd.ExecuteNonQueryAsync();
                }
                await using (var cmd = new SqlCommand("INSERT INTO dbo.SemaVerzija (Skripta) VALUES (@s)", db, tx))
                {
                    cmd.Parameters.AddWithValue("@s", ime);
                    await cmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                logger.LogInformation("Baza: izvršena skripta {Skripta}", ime);
                novoIzvrsene.Add(ime);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException($"Skripta {ime} nije izvršena: {ex.Message}", ex);
            }
        }
        return novoIzvrsene;
    }
}
