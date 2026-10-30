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
PASTE: the "Successfully created assignment ..." log line
```

---

## Part 3: Connection failure telemetry

**Scenario:** A - Authentication failure (`Database__Password=wrongpassword_999`)
**PostgreSQL error code:** 28P01

Captured Error event:

```json
PASTE: the "LogLevel":"Error" log line
```

API response to the client during the outage:

```text
PASTE: HTTP 503 "Database Service Unavailable" response
```

Recovery after reverting the password:

```text
PASTE: 201 Created response after the fix
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
