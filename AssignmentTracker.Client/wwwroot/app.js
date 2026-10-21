// Assignment Tracker consumer app.
// Talks to the API with fetch(). Every value that comes back from the API is put on the
// page with textContent (never innerHTML), so API text is always shown as text, not HTML.

const API_BASE = (window.APP_CONFIG && window.APP_CONFIG.apiBaseUrl) || "http://localhost:5204";
const RESOURCE = `${API_BASE}/api/assignments`;
const TIMEOUT_MS = 8000;

const STATUS_LABELS = { "todo": "To do", "in-progress": "In progress", "done": "Done" };
// Bootstrap badge colors for each status
const STATUS_CLASSES = { "todo": "text-bg-secondary", "in-progress": "text-bg-warning", "done": "text-bg-success" };

// Shortcut: find an element on the page by its id
const $ = (id) => document.getElementById(id);
const els = {
  apiUrl: $("api-url"), reload: $("reload"), retry: $("retry"),
  loading: $("state-loading"), error: $("state-error"), empty: $("state-empty"),
  errorTitle: $("error-title"), errorDetail: $("error-detail"),
  table: $("table"), rows: $("rows"), count: $("count"), rowTemplate: $("row-template"),
  form: $("assignment-form"), formHeading: $("form-heading"), editId: $("edit-id"),
  title: $("title"), course: $("course"), dueDate: $("dueDate"), status: $("status"),
  submit: $("submit"), cancelEdit: $("cancel-edit"), formMessage: $("form-message"),
  log: $("log"), clearLog: $("clear-log"),
};

els.apiUrl.textContent = RESOURCE;

// ---------------------------------------------------------------------------
// HTTP helper: one place that sends requests, handles timeouts/network failures,
// parses JSON or problem-details bodies, and writes to the request log.
// ---------------------------------------------------------------------------

// An error that also carries the HTTP status code and any field validation messages
class ApiError extends Error {
  constructor(message, { status = 0, title = "", errors = null } = {}) {
    super(message);
    this.status = status;   // 0 means the request never got a response (network failure/timeout)
    this.title = title;
    this.errors = errors;   // field errors from a 400 validation response
  }
}

// Sends one request to the API and returns the data; throws an ApiError on failure
async function api(method, url, body) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  const started = performance.now();
  const entry = addLogEntry(method, url, body);

  let response;

  try {
    response = await fetch(url, {
      method,
      headers: body !== undefined ? { "Content-Type": "application/json", "Accept": "application/json" }
                                  : { "Accept": "application/json" },
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal: controller.signal,
    });
  }

  catch (err) {
    const timedOut = err.name === "AbortError";
    const message = timedOut
      ? `The API did not respond within ${TIMEOUT_MS / 1000} seconds.`
      : `Could not reach the API at ${API_BASE}. Make sure it is running.`;
    finishLogEntry(entry, { status: "Network error", ok: false, ms: performance.now() - started, text: message });
    throw new ApiError(message, { title: timedOut ? "Request timed out" : "Network error" });
  }

  finally {
    clearTimeout(timer);
  }

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    }

    catch {
      data = text;
    }
  }

  finishLogEntry(entry, {
    status: `${response.status} ${response.statusText}`.trim(),
    ok: response.ok,
    ms: performance.now() - started,
    text: typeof data === "string" ? data : (data === null ? "(no body)" : JSON.stringify(data, null, 2)),
  });

  if (!response.ok) {
    // ASP.NET Core returns RFC 7807 "problem details": { title, detail, status, errors? }
    const problem = data && typeof data === "object" ? data : {};
    const message = problem.detail || problem.title || `The API returned ${response.status}.`;
    throw new ApiError(message, {
      status: response.status,
      title: `Request failed (${response.status})`,
      errors: problem.errors || null,
    });
  }
  return data;
}

// ---------------------------------------------------------------------------
// GET: load and render the list
// ---------------------------------------------------------------------------

// Shows one list state (loading, error, empty, or list) and hides the rest
function showState(which) {
  els.loading.hidden = which !== "loading";
  els.error.hidden = which !== "error";
  els.empty.hidden = which !== "empty";
  els.table.hidden = which !== "list";
}

// GETs all assignments and shows the table, or the error box if it fails
async function loadAssignments() {
  showState("loading");
  els.count.textContent = "";
  els.reload.disabled = true;
  try {
    const assignments = await api("GET", RESOURCE);
    if (!Array.isArray(assignments)) throw new ApiError("The API returned data in an unexpected format.");
    renderList(assignments);
  }

  catch (err) {
    els.errorTitle.textContent = err.title || "Something went wrong";
    els.errorDetail.textContent = err.message;
    showState("error");
  }

  finally {
    els.reload.disabled = false;
  }
}

// Builds one table row per assignment (or shows the empty message)
function renderList(assignments) {
  els.rows.replaceChildren();
  els.count.textContent = `(${assignments.length})`;
  if (assignments.length === 0) {
    showState("empty");
    return;
  }
  for (const a of assignments) {
    const row = els.rowTemplate.content.firstElementChild.cloneNode(true);
    row.dataset.id = a.id;
    row.querySelector(".id").textContent = a.id;
    row.querySelector(".title").textContent = a.title;
    row.querySelector(".course").textContent = a.course;
    row.querySelector(".due").textContent = formatDate(a.dueDate);
    const status = row.querySelector(".status");
    status.textContent = STATUS_LABELS[a.status] || a.status;
    status.classList.add(STATUS_CLASSES[a.status] || "text-bg-light");
    row.querySelector(".edit").addEventListener("click", () => startEdit(a));
    row.querySelector(".delete").addEventListener("click", (e) => deleteAssignment(a, e.currentTarget));
    if (a.id === lastChangedId) row.classList.add("flash");   // brief highlight (styles.css)
    els.rows.append(row);
  }
  lastChangedId = null;
  showState("list");
}

// Turns "2026-10-25" into "Oct 25, 2026"
function formatDate(iso) {
  // "2026-10-25" -> "Oct 25, 2026" (parsed as a local date, not UTC, so it doesn't shift a day)
  const [y, m, d] = String(iso).split("-").map(Number);
  if (!y || !m || !d) return String(iso);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

let lastChangedId = null;

// ---------------------------------------------------------------------------
// POST (create) and PUT (update) share the form
// ---------------------------------------------------------------------------

// Collects the form's values into an object to send to the API
function readForm() {
  return {
    title: els.title.value,
    course: els.course.value,
    dueDate: els.dueDate.value || null,
    status: els.status.value,
  };
}

// Removes the red borders and error messages from every field
function clearFieldErrors() {
  document.querySelectorAll(".invalid-feedback").forEach((el) => (el.textContent = ""));
  document.querySelectorAll(".is-invalid").forEach((el) => {
    el.classList.remove("is-invalid");
    el.removeAttribute("aria-invalid");
  });
}

// Shows each 400 validation message under its field
function showFieldErrors(errors) {
  for (const [field, messages] of Object.entries(errors || {})) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);
    const slot = document.querySelector(`.invalid-feedback[data-for="${CSS.escape(key)}"]`);
    if (slot) slot.textContent = [].concat(messages).join(" ");
    const input = document.getElementById(key);
    if (input) {
      input.classList.add("is-invalid");   // Bootstrap shows the .invalid-feedback next to it
      input.setAttribute("aria-invalid", "true");
    }
  }
}

const MESSAGE_CLASSES = { success: "alert-success", error: "alert-danger", pending: "alert-secondary" };

// Shows the message under the form (success, error, or pending); empty text hides it
function setFormMessage(text, kind) {
  els.formMessage.textContent = text;
  els.formMessage.classList.remove(...Object.values(MESSAGE_CLASSES));
  if (kind) els.formMessage.classList.add(MESSAGE_CLASSES[kind]);
  els.formMessage.hidden = !text;
}

// Switches the form to edit mode, filled in with this assignment
function startEdit(a) {
  clearFieldErrors();
  setFormMessage("", "");
  els.editId.value = a.id;
  els.title.value = a.title;
  els.course.value = a.course;
  els.dueDate.value = a.dueDate;
  els.status.value = a.status;
  els.formHeading.textContent = `Edit assignment #${a.id}`;
  els.submit.replaceChildren("Save changes ", tag("PUT", "put"));
  els.cancelEdit.hidden = false;
  els.title.focus();
}

// Switches the form back to create mode and empties it
function resetForm() {
  els.form.reset();
  els.editId.value = "";
  clearFieldErrors();
  els.formHeading.textContent = "New assignment";
  els.submit.replaceChildren("Create ", tag("POST", "post"));
  els.cancelEdit.hidden = true;
}

// Makes a colored HTTP method badge, e.g. tag("POST", "post")
function tag(text, cls) {
  const span = document.createElement("span");
  span.className = `badge method-${cls}`;
  span.textContent = text;
  return span;
}

// Create or Save changes: sends a POST (new) or PUT (editing), then reloads the list
els.form.addEventListener("submit", async (e) => {
  e.preventDefault();
  clearFieldErrors();

  const id = els.editId.value;
  const body = readForm();

  els.submit.disabled = true;
  setFormMessage(id ? "Saving changes…" : "Creating…", "pending");
  try {
    const saved = id
      ? await api("PUT", `${RESOURCE}/${encodeURIComponent(id)}`, body)
      : await api("POST", RESOURCE, body);

    lastChangedId = saved.id;
    resetForm();
    setFormMessage(id ? `Updated assignment #${saved.id} (“${saved.title}”).`
                      : `Created assignment #${saved.id} (“${saved.title}”).`, "success");

    await loadAssignments();   // re-GET so the list reflects what the API actually stored
  }

  catch (err) {
    if (err.errors) showFieldErrors(err.errors);
    setFormMessage(`${err.title ? err.title + ": " : ""}${err.message}`, "error");
  }

  finally {
    els.submit.disabled = false;
  }
});

// Cancel edit: back to create mode
els.cancelEdit.addEventListener("click", () => { resetForm(); setFormMessage("", ""); });

// ---------------------------------------------------------------------------
// DELETE: two-step confirm on the button itself (no browser pop-up)
// ---------------------------------------------------------------------------

// First click arms the button; second click sends the DELETE and reloads the list
async function deleteAssignment(a, button) {
  if (button.dataset.confirm !== "yes") {
    button.dataset.confirm = "yes";
    button.textContent = "Confirm?";
    button.classList.replace("btn-outline-danger", "btn-danger");
    setTimeout(() => {
      if (button.isConnected) {
        button.dataset.confirm = "";
        button.textContent = "Delete";
        button.classList.replace("btn-danger", "btn-outline-danger");
      }
    }, 4000);
    return;
  }
  button.disabled = true;
  try {
    await api("DELETE", `${RESOURCE}/${encodeURIComponent(a.id)}`);
    if (els.editId.value === String(a.id)) resetForm();
    setFormMessage(`Deleted assignment #${a.id} (“${a.title}”).`, "success");
    await loadAssignments();
  }

  catch (err) {
    setFormMessage(`${err.title ? err.title + ": " : ""}${err.message}`, "error");
    button.disabled = false;
    // A 404 means it was already gone; refresh so the list matches the API.
    if (err.status === 404) await loadAssignments();
  }
}

// ---------------------------------------------------------------------------
// Request log
// ---------------------------------------------------------------------------

// Adds a request to the log before it is sent (method, route, request body)
function addLogEntry(method, url, body) {
  const li = document.createElement("li");
  li.className = "list-group-item";

  const line = document.createElement("div");
  line.className = "d-flex flex-wrap align-items-center gap-2";
  const m = tag(method, method.toLowerCase());
  const u = document.createElement("code");
  u.textContent = url.replace(API_BASE, "");
  const s = document.createElement("span");
  s.className = "log-status ms-auto small text-body-secondary";
  s.textContent = "…";
  line.append(m, u, s);
  li.append(line);

  if (body !== undefined) li.append(logBlock("Request body", JSON.stringify(body, null, 2)));

  els.log.prepend(li);
  while (els.log.children.length > 30) els.log.lastElementChild.remove();
  return li;
}

// Fills in a log entry once the response arrives (status, time, response body)
function finishLogEntry(li, { status, ok, ms, text }) {
  const statusEl = li.querySelector(".log-status");
  statusEl.textContent = `${status} · ${Math.round(ms)} ms`;
  if (!ok) {
    li.classList.add("list-group-item-danger");
    statusEl.classList.replace("text-body-secondary", "fw-semibold");
  }
  li.append(logBlock("Response", text));
}

// Makes a collapsible "Request body" / "Response" section for a log entry
function logBlock(label, text) {
  const details = document.createElement("details");
  details.className = "mt-2";
  const summary = document.createElement("summary");
  summary.className = "small text-body-secondary";
  summary.textContent = label;
  const pre = document.createElement("pre");
  pre.className = "log-body bg-body-tertiary border rounded p-2 small mt-1 mb-0";
  pre.textContent = text;
  details.append(summary, pre);
  if (label === "Response") details.open = true;
  return details;
}

// Wire up the Clear, Reload, and Try again buttons
els.clearLog.addEventListener("click", () => els.log.replaceChildren());
els.reload.addEventListener("click", loadAssignments);
els.retry.addEventListener("click", loadAssignments);

// Load the list when the page first opens
loadAssignments();
