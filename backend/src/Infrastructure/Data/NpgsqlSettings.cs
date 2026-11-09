namespace TaxNavi.Infrastructure.Data;

/// <summary>Process-wide Npgsql settings. Must run before the first database connection.</summary>
public static class NpgsqlSettings
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        // All timestamps are UTC by convention. Map DateTime to "timestamp without time zone" (the same semantics
        // as SQL Server's datetime2) so DateTime.Kind never causes runtime errors.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _configured = true;
    }

    /// <summary>
    /// Accepts either an Npgsql connection string or a URI such as Render's
    /// postgres://user:password@host:5432/database and returns an Npgsql connection string.
    /// </summary>
    public static string NormalizeConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Render's internal hostnames have no dot and use the private network; external hostnames require TLS.
        builder.SslMode = uri.Host.Contains('.') ? Npgsql.SslMode.Require : Npgsql.SslMode.Prefer;
        return builder.ConnectionString;
    }
}
