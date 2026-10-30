# Assignment Tracker

A small C# / ASP.NET Core project with two parts:

- **AssignmentTracker** — a REST API (ASP.NET Core controllers, .NET 10) for tracking school assignments. Supports GET, POST, PUT and DELETE on the `assignments` resource.
- **AssignmentTracker.Client** — a local web app that consumes the API. It lists assignments, lets you create, edit and delete them, and shows every request and response in a request log so you can see each operation working.

Data is kept in memory, so it resets to two sample assignments each time the API restarts. No database, credentials or secrets are needed.

## Running it locally

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Start the API and the client in **two terminals** from the repository root:

```bash
# Terminal 1 — the API on http://localhost:5204
dotnet run --project AssignmentTracker --launch-profile http

# Terminal 2 — the consumer app on http://localhost:5300
dotnet run --project AssignmentTracker.Client --launch-profile http
```

Then open **http://localhost:5300** in a browser.

**Visual Studio:** right-click the solution → *Configure Startup Projects…* → *Multiple startup projects* → set both projects to *Start* (with the `http` profile), then press F5.

**Configuration:** the client reads the API address from `ApiBaseUrl` in `AssignmentTracker.Client/appsettings.json`. The API only accepts browser requests from the origins listed in `Cors:AllowedOrigins` in `AssignmentTracker/appsettings.json` (default `http://localhost:5300`). Change both if you use different ports.

You can also call the API directly with `AssignmentTracker/AssignmentTracker.http` (Visual Studio / VS Code REST Client) or curl.

## Resource

An **assignment**:

| Field     | Type                 | Notes                                    |
|-----------|----------------------|------------------------------------------|
| `id`      | int                  | Assigned by the server                   |
| `title`   | string               | Required, max 100 characters             |
| `course`  | string               | Required, max 60 characters              |
| `dueDate` | date (`YYYY-MM-DD`)  | Required                                 |
| `status`  | string               | `todo` (default), `in-progress`, `done`  |

## Routes

Base URL: `http://localhost:5204`

| Method   | Route                     | Operates on            | Success                                   | Errors       |
|----------|---------------------------|------------------------|-------------------------------------------|--------------|
| `GET`    | `/api/assignments`        | All assignments        | `200 OK` + JSON array (sorted by due date)| —            |
| `GET`    | `/api/assignments/{id}`   | One assignment         | `200 OK` + the assignment                 | `404`        |
| `POST`   | `/api/assignments`        | Creates an assignment  | `201 Created` + the new assignment + `Location` header | `400` |
| `PUT`    | `/api/assignments/{id}`   | Updates an assignment  | `200 OK` + the updated assignment         | `400`, `404` |
| `DELETE` | `/api/assignments/{id}`   | Removes an assignment  | `204 No Content`                          | `404`        |

Example POST / PUT body:

```json
{ "title": "Lab 5", "course": "Web Programming 1", "dueDate": "2026-10-30", "status": "todo" }
```

Errors are returned as standard [problem details](https://www.rfc-editor.org/rfc/rfc9457) JSON. Validation failures (`400`) include an `errors` object with a message per field; malformed JSON also returns `400`.

## How the consumer app handles request states

- **Loading:** while the list is being fetched, the table is replaced with a spinner and "Loading assignments…". Buttons that would send a duplicate request are disabled while it is in flight, and the form shows "Creating…" / "Saving changes…".
- **Returned data:** the list is always built from the live `GET /api/assignments` response. After every POST, PUT or DELETE the app requests the list again, so the table shows what the API actually stored; the changed row is briefly highlighted and a success message names the assignment.
- **Empty result:** when the API returns an empty array, a distinct "No assignments yet" message appears instead of an empty table.
- **Failures:**
  - *Unsuccessful HTTP responses* (400, 404, 500…) show the status code and the API's `detail`/`title` message. For `400` validation errors, each field's message appears under that field.
  - *Network failures* (API not running, wrong port, CORS blocked) show "Could not reach the API at …" with a **Try again** button.
  - *Timeouts:* requests are cancelled after 8 seconds with a clear message.
- **Safe rendering:** everything that comes from the API is inserted with `textContent`, never `innerHTML`, so a title like `<b>Lab 5</b>` is displayed as literal text, not HTML.
- **Request log:** every call shows its method, route, status code, time, request body and response body.

## Running with Docker (API + PostgreSQL)

Data is stored in PostgreSQL 17. `compose.yaml` starts the API and the database together on a private bridge network; the API waits until the database passes its healthcheck.

```bash
docker compose up -d --build        # API on http://localhost:8080, Postgres on localhost:5432
curl http://localhost:8080/api/assignments
docker compose down                 # removes the containers; data stays in the pgdata volume
```

Database rows live in the `pgdata` named volume, so they survive `docker compose down` / `up`. Only `docker compose down -v` deletes them. `init-schema.sql` runs only when the volume is empty (the first start).

To run the API from Visual Studio instead, start just the database (`docker compose up -d db`); `appsettings.Development.json` points the API at `localhost:5432`. When the API runs in Docker, set `ApiBaseUrl` in `AssignmentTracker.Client/appsettings.json` to `http://localhost:8080`.

## Project layout

```
AssignmentTracker/                 API
  Models/Assignment.cs             Assignment model + AssignmentInput (request body + validation)
  Controllers/AssignmentsController.cs  GET/POST/PUT/DELETE endpoints
  Services/AssignmentRepository.cs PostgreSQL data access (Npgsql, parameterized SQL)
  Program.cs                       Services (DI), CORS, and the request pipeline
  AssignmentTracker.http           Sample requests
Dockerfile                         Multi-stage build for the API image
compose.yaml                       API + PostgreSQL 17 with a persistent named volume
init-schema.sql                    Table, index and seed rows (runs on first database start)
AssignmentTracker.Client/          Consumer web app
  Program.cs                       Serves wwwroot and /config.js (the API address)
  wwwroot/index.html, app.js, styles.css
```

The consumer app is styled with [Bootstrap 5.3](https://getbootstrap.com/), loaded from the jsDelivr CDN (so the browser needs an internet connection the first time it loads the page). `styles.css` only holds a few extras Bootstrap doesn't provide, such as the HTTP method badge colors and the request-log layout.
