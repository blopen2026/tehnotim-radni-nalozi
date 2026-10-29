using System.Data;
using Microsoft.Data.SqlClient;

namespace Tehnotim.RadniNalozi.Prijava;

public record Korisnik(
    int Id,
    string KorisnickiNalog,
    string? ImePrezime,
    string? Email,
    string Uloga,
    bool Aktivan,
    DateTime? ZadnjaPrijava);

public record KorisnikUnos(
    string KorisnickiNalog,
    string? ImePrezime,
    string? Email,
    string Uloga,
    bool Aktivan);

/// <summary>Rad s tabelom dbo.Korisnik.</summary>
public class Korisnici(string connectionString)
{
    private const string Kolone = "Id, KorisnickiNalog, ImePrezime, Email, Uloga, Aktivan, ZadnjaPrijava";

    private static Korisnik Procitaj(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetString(1),
        r.IsDBNull(2) ? null : r.GetString(2),
        r.IsDBNull(3) ? null : r.GetString(3),
        r.GetString(4),
        r.GetBoolean(5),
        r.IsDBNull(6) ? null : r.GetDateTime(6));

    /// <summary>
    /// Poziva se pri svakom zahtjevu prijavljenog korisnika. Ako u tabeli još nema nikoga,
    /// ovaj korisnik postaje prvi administrator. Bilježi i vrijeme zadnje prijave.
    /// </summary>
    public async Task<Korisnik?> PrijavaAsync(string nalog)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        await using (var cmd = new SqlCommand(@"
            INSERT INTO dbo.Korisnik (KorisnickiNalog, Uloga)
            SELECT @nalog, 'administrator'
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Korisnik WITH (UPDLOCK, HOLDLOCK));

            UPDATE dbo.Korisnik SET ZadnjaPrijava = SYSUTCDATETIME()
            WHERE KorisnickiNalog = @nalog
              AND (ZadnjaPrijava IS NULL OR ZadnjaPrijava < DATEADD(minute, -10, SYSUTCDATETIME()));", conn))
        {
            cmd.Parameters.AddWithValue("@nalog", nalog);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var cmd2 = new SqlCommand($"SELECT {Kolone} FROM dbo.Korisnik WHERE KorisnickiNalog = @nalog", conn);
        cmd2.Parameters.AddWithValue("@nalog", nalog);
        await using var r = await cmd2.ExecuteReaderAsync();
        return await r.ReadAsync() ? Procitaj(r) : null;
    }

    public async Task<List<Korisnik>> SviAsync()
    {
        var lista = new List<Korisnik>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT {Kolone} FROM dbo.Korisnik ORDER BY Aktivan DESC, ImePrezime, KorisnickiNalog", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(Procitaj(r));
        return lista;
    }

    /// <summary>Dodaje (id = null) ili mijenja korisnika. Vraća poruku greške ili null ako je sve u redu.</summary>
    public async Task<string?> SacuvajAsync(int? id, KorisnikUnos u)
    {
        var nalog = (u.KorisnickiNalog ?? "").Trim();
        if (!nalog.Contains('\\') || nalog.StartsWith('\\') || nalog.EndsWith('\\'))
            return "Nalog upišite u obliku DOMEN\\korisnik, npr. TEHNOTIM\\marko.";
        if (!Uloge.Sve.Contains(u.Uloga))
            return "Nepoznata uloga.";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        await using (var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Korisnik WITH (UPDLOCK) WHERE KorisnickiNalog = @nalog AND (@id IS NULL OR Id <> @id)", conn, tx))
        {
            cmd.Parameters.AddWithValue("@nalog", nalog);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = (object?)id ?? DBNull.Value;
            if ((int)(await cmd.ExecuteScalarAsync())! > 0)
                return "Taj nalog je već upisan.";
        }

        var sql = id is null
            ? "INSERT INTO dbo.Korisnik (KorisnickiNalog, ImePrezime, Email, Uloga, Aktivan) VALUES (@nalog, @ime, @email, @uloga, @aktivan)"
            : "UPDATE dbo.Korisnik SET KorisnickiNalog = @nalog, ImePrezime = @ime, Email = @email, Uloga = @uloga, Aktivan = @aktivan WHERE Id = @id";
        await using (var cmd = new SqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@nalog", nalog);
            cmd.Parameters.AddWithValue("@ime", string.IsNullOrWhiteSpace(u.ImePrezime) ? DBNull.Value : (object)u.ImePrezime.Trim());
            cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(u.Email) ? DBNull.Value : (object)u.Email.Trim());
            cmd.Parameters.AddWithValue("@uloga", u.Uloga);
            cmd.Parameters.AddWithValue("@aktivan", u.Aktivan);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = (object?)id ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        // Mora ostati bar jedan aktivan administrator
        await using (var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Korisnik WHERE Uloga = 'administrator' AND Aktivan = 1", conn, tx))
        {
            if ((int)(await cmd.ExecuteScalarAsync())! == 0)
            {
                await tx.RollbackAsync();
                return "Mora ostati bar jedan aktivan administrator.";
            }
        }

        await tx.CommitAsync();
        return null;
    }
}
