using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Abs.FixedAssets.Services.Diagnostics;

/// <summary>
/// EF Core command interceptor that logs any database command exceeding the
/// configured threshold (default 500ms). The log line is emitted at Warning
/// level so it surfaces in production stdout without flooding normal traffic.
///
/// PII safety: parameter VALUES are never logged. Only the parameter name,
/// CLR type, and value-length-or-null are emitted, e.g.
///   "@p0:int32(len=4), @username:string(len=12), @password:REDACTED"
/// Names matching the sensitive denylist (password, token, secret, email, …)
/// are reported as REDACTED with no length to avoid even side-channel leaks.
/// SQL text is logged in full (truncated to 4000 chars) — the SQL itself
/// rarely contains literals once parameterized, and is required for triage.
///
/// The current RequestId is automatically picked up via the ILogger scope
/// established by RequestIdMiddleware, so slow queries can be traced back
/// to the originating HTTP request.
///
/// Both async AND sync EF execution paths are covered.
/// </summary>
public sealed class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger<SlowQueryInterceptor> _logger;
    private readonly TimeSpan _threshold;
    private const int MaxSqlLen = 4000;

    // Case-insensitive contains-match against parameter names.
    private static readonly string[] SensitiveNameFragments =
    {
        "password", "passwd", "pwd",
        "secret", "token", "apikey", "api_key", "auth",
        "ssn", "creditcard", "cardnumber", "cvv",
        "email", "phone",
    };

    public SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger, TimeSpan? threshold = null)
    {
        _logger = logger;
        _threshold = threshold ?? TimeSpan.FromMilliseconds(500);
    }

    // ---------- async overrides ----------
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { Report(command, eventData); return new ValueTask<DbDataReader>(result); }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Report(command, eventData); return new ValueTask<int>(result); }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { Report(command, eventData); return new ValueTask<object?>(result); }

    // ---------- sync overrides (EF still calls these on sync code paths) ----------
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Report(command, eventData); return result; }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Report(command, eventData); return result; }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Report(command, eventData); return result; }

    private void Report(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration < _threshold) return;

        var sql = command.CommandText ?? string.Empty;
        if (sql.Length > MaxSqlLen) sql = sql.Substring(0, MaxSqlLen) + "...[truncated]";

        var parameters = string.Join(", ", command.Parameters.Cast<DbParameter>().Select(SafeRender));

        _logger.LogWarning(
            "SlowQuery duration={DurationMs:F1}ms threshold={ThresholdMs:F0}ms commandType={CommandType} parameters=[{Parameters}] sql={Sql}",
            eventData.Duration.TotalMilliseconds,
            _threshold.TotalMilliseconds,
            command.CommandType,
            parameters,
            sql);
    }

    private static string SafeRender(DbParameter p)
    {
        var name = p.ParameterName ?? "?";
        if (IsSensitive(name)) return $"{name}:REDACTED";

        if (p.Value == null || p.Value == DBNull.Value)
            return $"{name}:null";

        var clrType = p.Value.GetType().Name;
        var len = p.Value switch
        {
            string s => s.Length.ToString(),
            byte[] b => b.Length.ToString(),
            _ => null,
        };
        return len != null ? $"{name}:{clrType}(len={len})" : $"{name}:{clrType}";
    }

    private static bool IsSensitive(string name)
    {
        foreach (var frag in SensitiveNameFragments)
        {
            if (name.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
