const languages = [
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000001", name: "English", code: "en", createdAt: "2026-05-02T00:30:00Z", updatedAt: "2026-07-24T01:15:00Z" },
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000002", name: "French", code: "fr", createdAt: "2026-05-02T00:31:00Z", updatedAt: "2026-07-22T05:40:00Z" },
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000003", name: "German", code: "de", createdAt: "2026-05-04T02:12:00Z", updatedAt: "2026-07-20T23:25:00Z" },
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000004", name: "Japanese", code: "ja", createdAt: "2026-05-08T06:45:00Z", updatedAt: "2026-07-18T04:05:00Z" },
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000005", name: "Spanish", code: "es", createdAt: "2026-05-07T07:20:00Z", updatedAt: "2026-07-17T00:50:00Z" },
  { id: "e4f36cb0-12f2-4ef4-96f7-000000000006", name: "Italian", code: "it", createdAt: "2026-06-11T09:00:00Z", updatedAt: "2026-07-15T03:30:00Z" }
];

const greetings = [
  { id: "3c244cad-1140-4a21-895e-000000000001", languageId: languages[0].id, greetingText: "Hello", formal: false, createdAt: "2026-05-02T01:00:00Z", updatedAt: "2026-07-24T00:55:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000002", languageId: languages[0].id, greetingText: "Good morning", formal: true, createdAt: "2026-05-02T01:05:00Z", updatedAt: "2026-07-23T23:10:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000003", languageId: languages[1].id, greetingText: "Bonjour", formal: true, createdAt: "2026-05-03T02:00:00Z", updatedAt: "2026-07-22T05:44:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000004", languageId: languages[2].id, greetingText: "Guten Tag", formal: true, createdAt: "2026-05-04T03:00:00Z", updatedAt: "2026-07-20T23:27:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000005", languageId: languages[3].id, greetingText: "こんにちは", formal: false, createdAt: "2026-05-08T07:00:00Z", updatedAt: "2026-07-18T04:08:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000006", languageId: languages[4].id, greetingText: "Hola", formal: false, createdAt: "2026-05-07T08:00:00Z", updatedAt: "2026-07-17T00:55:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000007", languageId: languages[4].id, greetingText: "Buenos días", formal: true, createdAt: "2026-05-07T08:05:00Z", updatedAt: "2026-07-16T22:05:00Z" },
  { id: "3c244cad-1140-4a21-895e-000000000008", languageId: languages[5].id, greetingText: "Ciao", formal: false, createdAt: "2026-06-11T09:15:00Z", updatedAt: "2026-07-15T03:35:00Z" }
];

const pageMeta = {
  dashboard: { title: "Dashboard", description: "An overview of the language and greeting catalog." },
  languages: { title: "Languages", description: "Manage the languages available to the application." },
  greetings: { title: "Greetings", description: "Manage greeting text, language associations and formality." }
};

const state = {
  route: "dashboard",
  languageSort: { field: "name", direction: "asc" },
  greetingSort: { field: "greetingText", direction: "asc" },
  pendingDelete: null,
  toastTimer: null
};

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
const icon = (name) => `<svg aria-hidden="true"><use href="#icon-${name}"></use></svg>`;
const escapeHtml = (value) => String(value)
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;")
  .replaceAll("'", "&#039;");

function languageFor(id) {
  return languages.find(language => language.id === id);
}

function formatDate(value, includeTime = false) {
  const options = includeTime
    ? { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }
    : { day: "2-digit", month: "short", year: "numeric" };
  return new Intl.DateTimeFormat("en-AU", options).format(new Date(value));
}

function relativeDate(value) {
  const diffHours = Math.max(0, Math.round((new Date("2026-07-24T08:00:00Z") - new Date(value)) / 3_600_000));
  if (diffHours < 1) return "Just now";
  if (diffHours < 24) return `${diffHours}h ago`;
  const days = Math.round(diffHours / 24);
  return `${days}d ago`;
}

function guid() {
  return crypto.randomUUID?.() ?? `${Date.now()}-0000-4000-8000-${Math.random().toString(16).slice(2, 14)}`;
}

function goTo(route) {
  window.location.hash = route;
}

function currentRoute() {
  const route = window.location.hash.replace("#", "").toLowerCase();
  return pageMeta[route] ? route : "dashboard";
}

function renderRoute() {
  state.route = currentRoute();
  const meta = pageMeta[state.route];
  $("#pageTitle").textContent = meta.title;
  $("#pageDescription").textContent = meta.description;
  $("#breadcrumbCurrent").textContent = meta.title;
  $$("[data-page]").forEach(page => { page.hidden = page.dataset.page !== state.route; });
  $$("[data-route]").forEach(link => link.classList.toggle("active", link.dataset.route === state.route));

  $("#pageActions").innerHTML = state.route === "languages"
    ? `<button class="button" data-create="language">${icon("add")}Add language</button>`
    : state.route === "greetings"
      ? `<button class="button" data-create="greeting">${icon("add")}Add greeting</button>`
      : "";

  closeNavigation();
  renderAll();
}

function renderAll() {
  renderNavigationCounts();
  renderDashboard();
  renderLanguageFilters();
  renderLanguages();
  renderGreetings();
}

function renderNavigationCounts() {
  $("#languageNavCount").textContent = languages.length;
  $("#greetingNavCount").textContent = greetings.length;
}

function renderDashboard() {
  const formalCount = greetings.filter(greeting => greeting.formal).length;
  const allRecords = [
    ...languages.map(item => ({ ...item, type: "Language", label: item.name, icon: "language" })),
    ...greetings.map(item => ({
      ...item,
      type: "Greeting",
      label: item.greetingText,
      icon: "greeting",
      context: languageFor(item.languageId)?.name ?? "Unknown language"
    }))
  ].sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt));

  $("#languageMetric").textContent = languages.length;
  $("#greetingMetric").textContent = greetings.length;
  $("#formalMetric").textContent = formalCount;
  $("#formalCaption").textContent = `${greetings.length ? Math.round(formalCount / greetings.length * 100) : 0}% of greetings`;
  $("#lastUpdatedMetric").textContent = allRecords.length ? formatDate(allRecords[0].updatedAt) : "—";

  $("#activityList").innerHTML = allRecords.slice(0, 5).map(record => `
    <div class="activity-item">
      <span class="activity-item-icon">${icon(record.icon)}</span>
      <span class="activity-item-copy">
        <strong>${escapeHtml(record.label)}</strong>
        <span>${record.type}${record.context ? ` · ${escapeHtml(record.context)}` : ""}</span>
      </span>
      <time datetime="${record.updatedAt}">${relativeDate(record.updatedAt)}</time>
    </div>
  `).join("") || `<div class="activity-item"><span>No catalog activity yet.</span></div>`;
}

function renderLanguageFilters() {
  const currentValue = $("#greetingLanguageFilter").value;
  $("#greetingLanguageFilter").innerHTML = `<option value="">All languages</option>${languages
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name))
    .map(language => `<option value="${language.id}">${escapeHtml(language.name)}</option>`)
    .join("")}`;
  if (languages.some(language => language.id === currentValue)) $("#greetingLanguageFilter").value = currentValue;
}

function compareValues(a, b, field, direction) {
  const first = a[field] ?? "";
  const second = b[field] ?? "";
  const result = typeof first === "boolean"
    ? Number(first) - Number(second)
    : String(first).localeCompare(String(second), undefined, { numeric: true, sensitivity: "base" });
  return direction === "asc" ? result : -result;
}

function filteredLanguages() {
  const search = $("#languageSearch").value.trim().toLocaleLowerCase();
  return languages
    .filter(language => !search || language.name.toLocaleLowerCase().includes(search) || language.code.toLocaleLowerCase().includes(search))
    .sort((a, b) => compareValues(a, b, state.languageSort.field, state.languageSort.direction));
}

function renderLanguages() {
  const rows = filteredLanguages();
  $("#languageRows").innerHTML = rows.length ? rows.map(language => {
    const greetingCount = greetings.filter(greeting => greeting.languageId === language.id).length;
    return `
      <tr>
        <td><span class="primary-cell"><strong>${escapeHtml(language.name)}</strong><small>${escapeHtml(language.id)}</small></span></td>
        <td><span class="code-chip">${escapeHtml(language.code)}</span></td>
        <td><span class="muted-cell">${formatDate(language.updatedAt)}</span></td>
        <td><span class="muted-cell">${greetingCount} ${greetingCount === 1 ? "greeting" : "greetings"}</span></td>
        <td>
          <div class="row-actions">
            <button class="row-action" data-action="view" data-type="language" data-id="${language.id}" aria-label="View ${escapeHtml(language.name)}" title="View">${icon("eye")}</button>
            <button class="row-action" data-action="edit" data-type="language" data-id="${language.id}" aria-label="Edit ${escapeHtml(language.name)}" title="Edit">${icon("edit")}</button>
            <button class="row-action delete" data-action="delete" data-type="language" data-id="${language.id}" aria-label="Delete ${escapeHtml(language.name)}" title="Delete">${icon("delete")}</button>
          </div>
        </td>
      </tr>`;
  }).join("") : emptyRow("language", 5);

  setResultCounts("language", rows.length, languages.length);
  setSortIndicators("language", state.languageSort);
}

function filteredGreetings() {
  const search = $("#greetingSearch").value.trim().toLocaleLowerCase();
  const languageId = $("#greetingLanguageFilter").value;
  const formality = $("#greetingFormalFilter").value;

  return greetings
    .map(greeting => ({ ...greeting, languageName: languageFor(greeting.languageId)?.name ?? "Unknown language" }))
    .filter(greeting => !search || greeting.greetingText.toLocaleLowerCase().includes(search))
    .filter(greeting => !languageId || greeting.languageId === languageId)
    .filter(greeting => !formality || (formality === "formal" ? greeting.formal : !greeting.formal))
    .sort((a, b) => compareValues(a, b, state.greetingSort.field, state.greetingSort.direction));
}

function renderGreetings() {
  const rows = filteredGreetings();
  $("#greetingRows").innerHTML = rows.length ? rows.map(greeting => `
    <tr>
      <td><span class="primary-cell"><strong>${escapeHtml(greeting.greetingText)}</strong><small>${escapeHtml(greeting.id)}</small></span></td>
      <td><span class="muted-cell">${escapeHtml(greeting.languageName)}</span></td>
      <td><span class="type-chip ${greeting.formal ? "formal" : "informal"}">${greeting.formal ? "Formal" : "Informal"}</span></td>
      <td><span class="muted-cell">${formatDate(greeting.updatedAt)}</span></td>
      <td>
        <div class="row-actions">
          <button class="row-action" data-action="view" data-type="greeting" data-id="${greeting.id}" aria-label="View ${escapeHtml(greeting.greetingText)}" title="View">${icon("eye")}</button>
          <button class="row-action" data-action="edit" data-type="greeting" data-id="${greeting.id}" aria-label="Edit ${escapeHtml(greeting.greetingText)}" title="Edit">${icon("edit")}</button>
          <button class="row-action delete" data-action="delete" data-type="greeting" data-id="${greeting.id}" aria-label="Delete ${escapeHtml(greeting.greetingText)}" title="Delete">${icon("delete")}</button>
        </div>
      </td>
    </tr>
  `).join("") : emptyRow("greeting", 5);

  setResultCounts("greeting", rows.length, greetings.length);
  setSortIndicators("greeting", state.greetingSort);
}

function emptyRow(type, columns) {
  return `<tr><td colspan="${columns}" class="empty-state"><div>${icon("search")}<strong>No ${type}s found</strong><span>Try adjusting the current filters.</span></div></td></tr>`;
}

function setResultCounts(type, filtered, total) {
  $(`#${type}ResultCount`).textContent = filtered === total ? `${total} records` : `${filtered} of ${total} records`;
  $(`#${type}FooterCount`).textContent = `${total} total`;
  $(`#${type}PageEnd`).textContent = Math.min(filtered, 10);
  $(`#${type}PageTotal`).textContent = filtered;
}

function setSortIndicators(type, sort) {
  $$(`[data-${type}-sort]`).forEach(button => {
    const active = button.dataset[`${type}Sort`] === sort.field;
    button.classList.toggle("active", active);
    button.classList.toggle("desc", active && sort.direction === "desc");
  });
}

function openDialog(type, mode, id = null) {
  const collection = type === "language" ? languages : greetings;
  const record = id ? collection.find(item => item.id === id) : null;
  if (id && !record) return;

  const entity = type === "language" ? "Language" : "Greeting";
  $("#dialogEyebrow").textContent = `${entity} record`;
  $("#dialogTitle").textContent = mode === "create" ? `Add ${entity.toLowerCase()}` : mode === "edit" ? `Edit ${entity.toLowerCase()}` : `${entity} details`;
  $("#dialogBody").innerHTML = mode === "view" ? detailMarkup(type, record) : formMarkup(type, record);
  $("#dialogFooter").innerHTML = mode === "view"
    ? `<button class="button secondary" data-dialog-cancel>Close</button><button class="button" data-dialog-edit="${type}" data-id="${record.id}">${icon("edit")}Edit</button>`
    : `<button class="button secondary" data-dialog-cancel>Cancel</button><button class="button" data-dialog-save="${type}" data-mode="${mode}" data-id="${record?.id ?? ""}">${mode === "create" ? icon("add") : icon("check")}${mode === "create" ? "Create" : "Save changes"}</button>`;

  $("#dialogBackdrop").hidden = false;
  document.body.style.overflow = "hidden";
  setTimeout(() => $("#dialogBody input, #dialogBody select, [data-dialog-cancel]")?.focus(), 0);
}

function detailMarkup(type, record) {
  if (type === "language") {
    const greetingCount = greetings.filter(greeting => greeting.languageId === record.id).length;
    return `<dl class="detail-list">
      <div><dt>Name</dt><dd>${escapeHtml(record.name)}</dd></div>
      <div><dt>Code</dt><dd><span class="code-chip">${escapeHtml(record.code)}</span></dd></div>
      <div><dt>Greetings</dt><dd>${greetingCount}</dd></div>
      <div><dt>Identifier</dt><dd>${escapeHtml(record.id)}</dd></div>
      <div><dt>Created</dt><dd>${formatDate(record.createdAt, true)}</dd></div>
      <div><dt>Last updated</dt><dd>${formatDate(record.updatedAt, true)}</dd></div>
    </dl>`;
  }

  const language = languageFor(record.languageId);
  return `<dl class="detail-list">
    <div><dt>Greeting</dt><dd>${escapeHtml(record.greetingText)}</dd></div>
    <div><dt>Language</dt><dd>${escapeHtml(language?.name ?? "Unknown")} <span class="code-chip">${escapeHtml(language?.code ?? "—")}</span></dd></div>
    <div><dt>Type</dt><dd><span class="type-chip ${record.formal ? "formal" : "informal"}">${record.formal ? "Formal" : "Informal"}</span></dd></div>
    <div><dt>Identifier</dt><dd>${escapeHtml(record.id)}</dd></div>
    <div><dt>Created</dt><dd>${formatDate(record.createdAt, true)}</dd></div>
    <div><dt>Last updated</dt><dd>${formatDate(record.updatedAt, true)}</dd></div>
  </dl>`;
}

function formMarkup(type, record) {
  if (type === "language") {
    return `<form id="entityForm" class="form-grid" novalidate>
      <div class="field">
        <label for="languageName">Name <span>*</span></label>
        <input id="languageName" name="name" maxlength="100" value="${escapeHtml(record?.name ?? "")}" placeholder="e.g. Portuguese" autocomplete="off">
        <span class="field-help">Human-readable language name, up to 100 characters.</span>
        <span class="field-error" data-error-for="name"></span>
      </div>
      <div class="field">
        <label for="languageCode">Code <span>*</span></label>
        <input id="languageCode" name="code" maxlength="10" value="${escapeHtml(record?.code ?? "")}" placeholder="e.g. pt-BR" autocomplete="off">
        <span class="field-help">Unique short code, up to 10 characters.</span>
        <span class="field-error" data-error-for="code"></span>
      </div>
      <span class="field-error" data-error-for="form"></span>
    </form>`;
  }

  return `<form id="entityForm" class="form-grid" novalidate>
    <div class="field">
      <label for="greetingText">Greeting text <span>*</span></label>
      <input id="greetingText" name="greetingText" maxlength="255" value="${escapeHtml(record?.greetingText ?? "")}" placeholder="e.g. Welcome" autocomplete="off">
      <span class="field-help">Greeting text shown to users, up to 255 characters.</span>
      <span class="field-error" data-error-for="greetingText"></span>
    </div>
    <div class="field">
      <label for="greetingLanguage">Language <span>*</span></label>
      <select id="greetingLanguage" name="languageId">
        <option value="">Select a language</option>
        ${languages.slice().sort((a, b) => a.name.localeCompare(b.name)).map(language =>
          `<option value="${language.id}" ${record?.languageId === language.id ? "selected" : ""}>${escapeHtml(language.name)} (${escapeHtml(language.code)})</option>`
        ).join("")}
      </select>
      <span class="field-error" data-error-for="languageId"></span>
    </div>
    <div class="checkbox-field">
      <input id="greetingFormal" name="formal" type="checkbox" ${record?.formal ? "checked" : ""}>
      <label for="greetingFormal">Formal greeting<small>Use for professional or respectful contexts.</small></label>
    </div>
  </form>`;
}

function closeDialog() {
  $("#dialogBackdrop").hidden = true;
  document.body.style.overflow = "";
}

function saveDialog(type, mode, id) {
  const form = $("#entityForm");
  if (!form) return;
  clearErrors(form);
  const data = new FormData(form);
  const now = new Date().toISOString();

  if (type === "language") {
    const name = String(data.get("name") ?? "").trim();
    const code = String(data.get("code") ?? "").trim();
    let valid = true;
    if (!name) { setError(form, "name", "Name is required."); valid = false; }
    if (!code) { setError(form, "code", "Code is required."); valid = false; }
    const duplicate = languages.find(language =>
      language.id !== id &&
      (language.name.toLocaleLowerCase() === name.toLocaleLowerCase() || language.code.toLocaleLowerCase() === code.toLocaleLowerCase()));
    if (valid && duplicate) { setError(form, "form", "A language with the same name or code already exists."); valid = false; }
    if (!valid) return;

    if (mode === "create") {
      languages.push({ id: guid(), name, code, createdAt: now, updatedAt: now });
      showToast("Language created", `${name} was added to the catalog.`);
    } else {
      const record = languages.find(language => language.id === id);
      Object.assign(record, { name, code, updatedAt: now });
      showToast("Language updated", `${name} was saved successfully.`);
    }
  } else {
    const greetingText = String(data.get("greetingText") ?? "").trim();
    const languageId = String(data.get("languageId") ?? "");
    const formal = data.get("formal") === "on";
    let valid = true;
    if (!greetingText) { setError(form, "greetingText", "Greeting text is required."); valid = false; }
    if (!languageId) { setError(form, "languageId", "Language is required."); valid = false; }
    if (!valid) return;

    if (mode === "create") {
      greetings.push({ id: guid(), languageId, greetingText, formal, createdAt: now, updatedAt: now });
      showToast("Greeting created", `${greetingText} was added to the catalog.`);
    } else {
      const record = greetings.find(greeting => greeting.id === id);
      Object.assign(record, { languageId, greetingText, formal, updatedAt: now });
      showToast("Greeting updated", `${greetingText} was saved successfully.`);
    }
  }

  closeDialog();
  renderAll();
}

function clearErrors(form) {
  $$(".field-error", form).forEach(error => { error.textContent = ""; });
  $$("input, select", form).forEach(field => field.classList.remove("invalid"));
}

function setError(form, field, message) {
  const error = $(`[data-error-for="${field}"]`, form);
  if (error) error.textContent = message;
  $(`[name="${field}"]`, form)?.classList.add("invalid");
}

function requestDelete(type, id) {
  const record = (type === "language" ? languages : greetings).find(item => item.id === id);
  if (!record) return;
  state.pendingDelete = { type, id };
  const label = type === "language" ? record.name : record.greetingText;
  const related = type === "language" ? greetings.filter(greeting => greeting.languageId === id).length : 0;
  $("#confirmTitle").textContent = `Delete ${type}?`;
  $("#confirmMessage").innerHTML = type === "language" && related
    ? `<strong>${escapeHtml(label)}</strong> and its ${related} associated ${related === 1 ? "greeting" : "greetings"} will be permanently deleted.`
    : `<strong>${escapeHtml(label)}</strong> will be permanently deleted. This action cannot be undone.`;
  $("#confirmBackdrop").hidden = false;
  document.body.style.overflow = "hidden";
  setTimeout(() => $("#confirmCancel").focus(), 0);
}

function closeConfirm() {
  $("#confirmBackdrop").hidden = true;
  state.pendingDelete = null;
  document.body.style.overflow = "";
}

function confirmDelete() {
  if (!state.pendingDelete) return;
  const { type, id } = state.pendingDelete;
  if (type === "language") {
    const index = languages.findIndex(language => language.id === id);
    const [record] = languages.splice(index, 1);
    for (let greetingIndex = greetings.length - 1; greetingIndex >= 0; greetingIndex--) {
      if (greetings[greetingIndex].languageId === id) greetings.splice(greetingIndex, 1);
    }
    showToast("Language deleted", `${record.name} was removed from the catalog.`);
  } else {
    const index = greetings.findIndex(greeting => greeting.id === id);
    const [record] = greetings.splice(index, 1);
    showToast("Greeting deleted", `${record.greetingText} was removed from the catalog.`);
  }
  closeConfirm();
  renderAll();
}

function showToast(title, message) {
  clearTimeout(state.toastTimer);
  $("#toastTitle").textContent = title;
  $("#toastMessage").textContent = message;
  $("#toast").classList.add("visible");
  state.toastTimer = setTimeout(hideToast, 4200);
}

function hideToast() {
  $("#toast").classList.remove("visible");
}

function toggleSort(type, field) {
  const sort = type === "language" ? state.languageSort : state.greetingSort;
  if (sort.field === field) sort.direction = sort.direction === "asc" ? "desc" : "asc";
  else Object.assign(sort, { field, direction: "asc" });
  type === "language" ? renderLanguages() : renderGreetings();
}

function toggleNavigation() {
  $("#sidebar").classList.toggle("open");
  $("#navScrim").classList.toggle("open");
}

function closeNavigation() {
  $("#sidebar").classList.remove("open");
  $("#navScrim").classList.remove("open");
}

document.addEventListener("click", event => {
  const target = event.target.closest("button, a");
  if (!target) return;

  if (target.matches("[data-create]")) openDialog(target.dataset.create, "create");
  if (target.matches("[data-quick-create]")) openDialog(target.dataset.quickCreate, "create");
  if (target.matches("[data-quick-route]")) goTo(target.dataset.quickRoute);
  if (target.matches("[data-action]")) {
    const { action, type, id } = target.dataset;
    action === "delete" ? requestDelete(type, id) : openDialog(type, action, id);
  }
  if (target.matches("[data-dialog-cancel]")) closeDialog();
  if (target.matches("[data-dialog-edit]")) openDialog(target.dataset.dialogEdit, "edit", target.dataset.id);
  if (target.matches("[data-dialog-save]")) saveDialog(target.dataset.dialogSave, target.dataset.mode, target.dataset.id || null);
  if (target.matches("[data-language-sort]")) toggleSort("language", target.dataset.languageSort);
  if (target.matches("[data-greeting-sort]")) toggleSort("greeting", target.dataset.greetingSort);
});

$("#languageSearch").addEventListener("input", renderLanguages);
$("#greetingSearch").addEventListener("input", renderGreetings);
$("#greetingLanguageFilter").addEventListener("change", renderGreetings);
$("#greetingFormalFilter").addEventListener("change", renderGreetings);
$("#navToggle").addEventListener("click", toggleNavigation);
$("#navScrim").addEventListener("click", closeNavigation);
$("#dialogClose").addEventListener("click", closeDialog);
$("#confirmCancel").addEventListener("click", closeConfirm);
$("#confirmDelete").addEventListener("click", confirmDelete);
$("#toastClose").addEventListener("click", hideToast);

$("#profileButton").addEventListener("click", event => {
  event.stopPropagation();
  const menu = $("#profileMenu");
  menu.hidden = !menu.hidden;
  $("#profileButton").setAttribute("aria-expanded", String(!menu.hidden));
});

document.addEventListener("click", event => {
  if (!event.target.closest(".profile")) {
    $("#profileMenu").hidden = true;
    $("#profileButton").setAttribute("aria-expanded", "false");
  }
});

document.addEventListener("keydown", event => {
  if (event.key !== "Escape") return;
  if (!$("#confirmBackdrop").hidden) closeConfirm();
  else if (!$("#dialogBackdrop").hidden) closeDialog();
  else closeNavigation();
});

window.addEventListener("hashchange", renderRoute);
renderRoute();
