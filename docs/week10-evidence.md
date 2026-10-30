# Week 10 Evidence: Relational Persistence, Multi-Container Orchestration, and Telemetry

Assignment Tracker API (ASP.NET Core 10 + PostgreSQL 17)

> Adaptation note: the assignment examples use a `service_records` table with an `environment` column.
> This app stores `assignments`, so the equivalents are the `/api/assignments` endpoint, the
> `assignments` table, and the `idx_assignments_status` index on the `status` column.

---

## Part 1: Volume persistence across container recreation

Records created, then `docker compose down` (no `-v`) and `docker compose up -d`:

```text
PASTE: output of GET http://localhost:8080/api/assignments after the restart
```

---

## Part 2: Successful Information transaction (structured log)

```json
{
    "Timestamp":  "2026-10-30T03:41:14.117Z ",
    "EventId":  0,
    "LogLevel":  "Information",
    "Category":  "AssignmentTracker.Services.AssignmentRepository",
    "Message":  "Successfully created assignment Outage test with generated ID 7 in 154ms",
    "State":  {
                  "AssignmentTitle":  "Outage test",
                  "AssignmentId":  7,
                  "ElapsedMs":  154,
                  "{OriginalFormat}":  "Successfully created assignment {AssignmentTitle} with generated ID {AssignmentId} in {ElapsedMs}ms"
              }
}
```

Slow-query Warning (same request crossed the 150ms threshold):

```json
{
    "Timestamp":  "2026-10-30T03:41:14.117Z ",
    "EventId":  0,
    "LogLevel":  "Warning",
    "Category":  "AssignmentTracker.Services.AssignmentRepository",
    "Message":  "Slow query detected on POST /api/assignments: execution took 153ms (threshold 150ms)",
    "State":  {
                  "Endpoint":  "POST /api/assignments",
                  "ElapsedMs":  153,
                  "ThresholdMs":  150,
                  "{OriginalFormat}":  "Slow query detected on {Endpoint}: execution took {ElapsedMs}ms (threshold {ThresholdMs}ms)"
              }
}
```

---

## Part 3: Connection failure telemetry

**Scenario:** A - Authentication failure (`Database__Password=wrongpassword_999`)
**PostgreSQL error code:** 28P01

Captured Error event:

```json
{
    "Timestamp":  "2026-10-30T03:35:29.881Z ",
    "EventId":  0,
    "LogLevel":  "Error",
    "Category":  "AssignmentTracker.Services.AssignmentRepository",
    "Message":  "Database operation failed against db on POST /api/assignments for assignment Outage test: 28P01: password authentication failed for user \"postgres\" (SqlState 28P01, after 61ms)",
    "Exception":  "Npgsql.PostgresException (0x80004005): 28P01: password authentication failed for user \"postgres\"\n   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)\n   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource\u003cTResult\u003e.GetResult(Int16 token)\n   at Npgsql.Internal.NpgsqlConnector.AuthenticateSASL(List`1 mechanisms, String username, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.Internal.NpgsqlConnector.Authenticate(String username, NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.Internal.NpgsqlConnector.\u003cOpen\u003eg__OpenCore|214_1(NpgsqlConnector conn, SslMode sslMode, NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.Internal.NpgsqlConnector.Open(NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.PoolingDataSource.OpenNewConnector(NpgsqlConnection conn, NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.PoolingDataSource.\u003cGet\u003eg__RentAsync|33_0(NpgsqlConnection conn, NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)\n   at Npgsql.NpgsqlConnection.\u003cOpen\u003eg__OpenAsync|42_0(Boolean async, CancellationToken cancellationToken)\n   at Npgsql.NpgsqlDataSourceCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)\n   at AssignmentTracker.Services.AssignmentRepository.ReadSingleAsync(NpgsqlCommand cmd, CancellationToken ct) in /src/AssignmentTracker/Services/AssignmentRepository.cs:line 180\n   at AssignmentTracker.Services.AssignmentRepository.\u003c\u003ec__DisplayClass9_0.\u003c\u003cAddAsync\u003eb__0\u003ed.MoveNext() in /src/AssignmentTracker/Services/AssignmentRepository.cs:line 81\n--- End of stack trace from previous location ---\n   at AssignmentTracker.Services.AssignmentRepository.\u003c\u003ec__DisplayClass9_0.\u003c\u003cAddAsync\u003eb__0\u003ed.MoveNext() in /src/AssignmentTracker/Services/AssignmentRepository.cs:line 81\n--- End of stack trace from previous location ---\n   at AssignmentTracker.Services.AssignmentRepository.RunAsync[T](String endpoint, String assignmentTitle, Func`1 operation) in /src/AssignmentTracker/Services/AssignmentRepository.cs:line 154\n  Exception data:\n    Severity: FATAL\n    SqlState: 28P01\n    MessageText: password authentication failed for user \"postgres\"\n    File: auth.c\n    Line: 329\n    Routine: auth_failed",
    "State":  {
                  "DbHost":  "db",
                  "Endpoint":  "POST /api/assignments",
                  "AssignmentTitle":  "Outage test",
                  "ErrorMessage":  "28P01: password authentication failed for user \"postgres\"",
                  "SqlState":  "28P01",
                  "ElapsedMs":  61,
                  "{OriginalFormat}":  "Database operation failed against {DbHost} on {Endpoint} for assignment {AssignmentTitle}: {ErrorMessage} (SqlState {SqlState}, after {ElapsedMs}ms)"
              }
}
```

API response to the client during the outage:

```text
HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json; charset=utf-8
Date: Fri, 30 Oct 2026 03:38:51 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"title":"Database Service Unavailable","status":503,"detail":"A persistence error occurred while processing the request."}
```

Recovery after reverting the password:

```text
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Date: Fri, 30 Oct 2026 03:41:14 GMT
Server: Kestrel
Location: http://localhost:8080/api/assignments/7
Transfer-Encoding: chunked

{"id":7,"title":"Outage test","course":"Web Programming 1","dueDate":"2026-11-12","status":"todo"}
```

---

## Part 4: Query performance baseline

```sql
EXPLAIN ANALYZE SELECT * FROM assignments WHERE status = 'todo';
```

```text
PASTE: EXPLAIN ANALYZE output
```

| Measure | Value |
|---|---|
| Scan type | |
| Planning time | |
| Execution time | |

---

## Part 5: Azure deployment and backup/restore verification

**Container App URL:**

### pg_dump output
```text
PASTE
```

### pg_restore output
```text
PASTE
```

### Checksum verification
```text
PASTE
```
