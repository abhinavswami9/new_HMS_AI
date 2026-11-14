import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Activity,
  ArrowRight,
  Building2,
  Check,
  ClipboardCheck,
  Download,
  FlaskConical,
  HeartPulse,
  Layers3,
  LockKeyhole,
  Pill,
  ScanLine,
  Settings2,
  ShieldCheck,
  Stethoscope,
  Users,
  Video,
  Workflow,
} from "lucide-react";
import { useApp } from "../store";
import {
  Badge,
  Empty,
  ErrorNote,
  Field,
  Modal,
  PageHeader,
  SearchBox,
  Submit,
} from "../components";
import { api, downloadCsv, shortDate, time } from "../api";
import type { Module } from "../types";
const moduleIcons: Record<string, typeof Activity> = {
  patients: Users,
  appointments: Activity,
  consultations: Stethoscope,
  billing: ClipboardCheck,
  ipd: HeartPulse,
  nursing: HeartPulse,
  pharmacy: Pill,
  laboratory: FlaskConical,
  radiology: ScanLine,
  pacs: ScanLine,
  emergency: HeartPulse,
  ot: Workflow,
  blood: HeartPulse,
  insurance: ShieldCheck,
  incentives: ClipboardCheck,
  telemedicine: Video,
  inventory: Layers3,
  cssd: Workflow,
  quality: ShieldCheck,
  portal: Users,
  reports: Activity,
};
export function Modules() {
  const { data } = useApp();
  const [phase, setPhase] = useState("All phases");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Module | null>(null);
  const modules = data.modules.filter(
    (m) =>
      (phase === "All phases" || m.phase === +phase.replace("Phase ", "")) &&
      m.name.toLowerCase().includes(search.toLowerCase()),
  );
  return (
    <>
      <PageHeader
        eyebrow="BUILT AROUND YOUR HOSPITAL"
        title="One platform. Every possibility."
        description="A modular foundation for connected care, from a single branch to a hospital network."
      >
        <span className="badge teal">
          <Layers3 size={14} />
          {data.modules.length} planned modules
        </span>
      </PageHeader>
      <div className="module-hero">
        <div>
          <span className="eyebrow">THE CAREFLOW ECOSYSTEM</span>
          <h2>
            Start with what you need.
            <br />
            Grow without starting over.
          </h2>
          <p>
            Shared patient identity, configurable workflows and a complete audit
            trail across every module.
          </p>
        </div>
        <div className="module-orbit">
          <div className="orbit-center">
            <span>+</span>
            <strong>careflow</strong>
          </div>
          <span className="orbit-node one">
            <Stethoscope size={23} />
          </span>
          <span className="orbit-node two">
            <FlaskConical size={23} />
          </span>
          <span className="orbit-node three">
            <Pill size={23} />
          </span>
          <span className="orbit-node four">
            <HeartPulse size={23} />
          </span>
        </div>
      </div>
      <div className="list-toolbar no-card">
        <SearchBox
          value={search}
          onChange={setSearch}
          placeholder="Find a module…"
        />
        <select
          aria-label="Filter module phase"
          value={phase}
          onChange={(e) => setPhase(e.target.value)}
        >
          {[
            "All phases",
            "Phase 1",
            "Phase 2",
            "Phase 3",
            "Phase 4",
            "Phase 5",
          ].map((x) => (
            <option key={x}>{x}</option>
          ))}
        </select>
        <span className="small muted">{modules.length} modules</span>
      </div>
      <div className="module-grid">
        {modules.map((m) => {
          const Icon = moduleIcons[m.key] || Layers3;
          return (
            <button
              className="card module-card"
              key={m.key}
              onClick={() => setSelected(m)}
            >
              <div>
                <span className={`quick-icon ${m.enabled ? "teal" : "gray"}`}>
                  <Icon size={22} />
                </span>
                <span className={`badge ${m.enabled ? "green" : "gray"}`}>
                  {m.enabled ? "Preview enabled" : "Planned"}
                </span>
              </div>
              <h3>{m.name}</h3>
              <p>{m.description}</p>
              <span className="text-link">
                Explore module <ArrowRight size={15} />
              </span>
            </button>
          );
        })}
      </div>
      {!modules.length && <Empty title="No matching modules" />}
      {selected && (
        <Modal
          title={selected.name}
          subtitle={`${selected.enabled ? "Initial workflow available in preview" : "Architecture specified — implementation planned"}`}
          onClose={() => setSelected(null)}
        >
          <div className="modal-body">
            <p>{selected.description}</p>
            <div className="module-contract">
              <h3>Module contract</h3>
              {[
                "Bounded architecture and ownership",
                "Least-privilege user roles",
                "Normalized SQL Server tables",
                "Tenant-scoped REST endpoints",
                "Responsive workflow screens",
                "Configurable business rules",
                "End-to-end clinical workflow",
                "Input and state validation",
                "Append-only audit trail",
              ].map((x, i) => (
                <div key={x}>
                  <span>{String(i + 1).padStart(2, "0")}</span>
                  {x}
                  <Check size={15} />
                </div>
              ))}
            </div>
            <div className="info-note">
              <ShieldCheck size={18} />
              <span>
                The complete contract is documented in
                docs/MODULE_SPECIFICATIONS.md. Planned modules do not process
                live clinical or financial work.
              </span>
            </div>
          </div>
          <div className="modal-footer">
            <button className="btn primary" onClick={() => setSelected(null)}>
              Got it
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
export function Administration() {
  const { data, user, demo, refresh, notify } = useApp();
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") || "organization";
  const [search, setSearch] = useState("");
  const [changing, setChanging] = useState<Module | null>(null);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const roles = [
    "Hospital Admin",
    "Doctor",
    "Receptionist",
    "Billing Staff",
    "Nurse",
  ];
  const permissions = [
    "patient.read",
    "patient.write",
    "schedule.read",
    "schedule.write",
    "clinical.read",
    "clinical.write",
    "clinical.sign",
    "billing.read",
    "billing.write",
    "billing.collect",
    "security.admin",
    "audit.read",
  ];
  const grants: Record<string, string[]> = {
    "Hospital Admin": permissions.filter(
      (p) => !["clinical.write", "clinical.sign"].includes(p),
    ),
    Doctor: [
      "patient.read",
      "schedule.read",
      "schedule.write",
      "clinical.read",
      "clinical.write",
      "clinical.sign",
    ],
    Receptionist: [
      "patient.read",
      "patient.write",
      "schedule.read",
      "schedule.write",
    ],
    "Billing Staff": [
      "patient.read",
      "schedule.read",
      "billing.read",
      "billing.write",
      "billing.collect",
    ],
    Nurse: ["patient.read", "schedule.read", "clinical.read"],
  };
  if (!user.permissions.includes("security.admin"))
    return (
      <Empty
        title="Administration is restricted"
        description="Your current role does not have security administration permission."
      />
    );
  return (
    <>
      <PageHeader
        eyebrow="HOSPITAL ADMINISTRATION"
        title="Your workspace, configured"
        description="Organization settings, access control and a traceable record of every change."
      >
        <span className="badge teal">
          <ShieldCheck size={14} />
          Scoped access
        </span>
      </PageHeader>
      <div className="tabs">
        {[
          ["organization", "Organization"],
          ["modules", "Module settings"],
          ["roles", "Roles & access"],
          ["audit", "Audit trail"],
        ].map(([key, label]) => (
          <button
            key={key}
            className={tab === key ? "active" : ""}
            onClick={() => setParams({ tab: key })}
          >
            {label}
          </button>
        ))}
      </div>
      {tab === "organization" && (
        <div className="detail-grid">
          <section className="card">
            <div className="card-header">
              <h2>
                <Building2 size={19} />
                Hospital profile
              </h2>
              <Badge status="Available" />
            </div>
            <div className="info-grid padded">
              <div>
                <span>Hospital name</span>
                <strong>{data.hospitalName}</strong>
              </div>
              <div>
                <span>Active branch</span>
                <strong>{data.branchName}</strong>
              </div>
              <div>
                <span>Deployment scope</span>
                <strong>Single hospital · multi-branch schema</strong>
              </div>
              <div>
                <span>Currency</span>
                <strong>Indian Rupee (INR)</strong>
              </div>
              <div>
                <span>Identity namespace</span>
                <strong>Hospital-scoped UHID</strong>
              </div>
              <div>
                <span>Environment</span>
                <strong>
                  {demo
                    ? "Synthetic development preview"
                    : "SQL Server workspace"}
                </strong>
              </div>
            </div>
            <div className="card-bottom">
              <span>
                <Settings2 size={15} />
                Organization editing and branch provisioning are planned setup
                workflows.
              </span>
            </div>
          </section>
          <section className="card">
            <div className="card-header">
              <h2>Care team</h2>
              <span className="count-badge">{data.doctors.length}</span>
            </div>
            <div className="team-list">
              {data.doctors.map((d) => (
                <div key={d.id}>
                  <span
                    className="avatar"
                    style={{ color: d.color, background: `${d.color}18` }}
                  >
                    {d.initials}
                  </span>
                  <div>
                    <strong>{d.name}</strong>
                    <small>{d.department}</small>
                  </div>
                  <span className="badge green">Active</span>
                </div>
              ))}
            </div>
          </section>
        </div>
      )}
      {tab === "modules" && (
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Branch module settings</h2>
              <p>
                Disabling a module stops new work without deleting its existing
                records.
              </p>
            </div>
          </div>
          <div className="settings-list">
            {data.modules
              .filter((m) => m.phase === 1)
              .map((m) => (
                <div key={m.key}>
                  <span className="quick-icon teal">
                    <Layers3 size={20} />
                  </span>
                  <div>
                    <strong>{m.name}</strong>
                    <p>{m.description}</p>
                  </div>
                  <button
                    className={`toggle ${m.enabled ? "on" : ""}`}
                    role="switch"
                    aria-checked={m.enabled}
                    aria-label={`${m.name} module`}
                    onClick={() => {
                      setChanging(m);
                      setReason("");
                      setError("");
                    }}
                  >
                    <span />
                  </button>
                </div>
              ))}
          </div>
          <div className="card-bottom">
            <span>
              <LockKeyhole size={15} />
              Later-phase modules remain gated until their implementation and
              release checks pass.
            </span>
          </div>
        </section>
      )}
      {tab === "roles" && (
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Default role permission matrix</h2>
              <p>
                Server-enforced permissions. Custom role editing and branch
                assignments are planned.
              </p>
            </div>
            <Badge status="Draft" />
          </div>
          <div className="table-scroll">
            <table className="roles-table">
              <thead>
                <tr>
                  <th>Permission</th>
                  {roles.map((r) => (
                    <th key={r}>{r}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {permissions.map((p) => (
                  <tr key={p}>
                    <td>
                      <code>{p}</code>
                    </td>
                    {roles.map((r) => (
                      <td key={r}>
                        {grants[r].includes(p) ? (
                          <Check size={17} className="teal-text" />
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="card-bottom">
            <span>
              <ShieldCheck size={15} />
              Administrative access does not grant clinical signing authority.
            </span>
          </div>
        </section>
      )}
      {tab === "audit" && (
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Workspace audit trail</h2>
              <p>
                Most recent 200 events · server-recorded actor and timestamp
              </p>
            </div>
            <button
              className="btn"
              onClick={() =>
                downloadCsv("careflow-audit.csv", [
                  ["Time", "Actor", "Action", "Entity", "Entity ID", "Reason"],
                  ...data.audit.map((a) => [
                    a.occurredAt,
                    a.actorName,
                    a.action,
                    a.entity,
                    a.entityId,
                    a.reason,
                  ]),
                ])
              }
            >
              <Download size={16} />
              Export
            </button>
          </div>
          <div className="list-toolbar">
            <SearchBox
              value={search}
              onChange={setSearch}
              placeholder="Search action, actor or entity…"
            />
          </div>
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Actor</th>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>Reason</th>
                </tr>
              </thead>
              <tbody>
                {data.audit
                  .filter((a) =>
                    `${a.actorName} ${a.action} ${a.entity}`
                      .toLowerCase()
                      .includes(search.toLowerCase()),
                  )
                  .map((a) => (
                    <tr key={a.id}>
                      <td>
                        {shortDate(a.occurredAt)}
                        <small className="cell-small">
                          {time(a.occurredAt)}
                        </small>
                      </td>
                      <td>{a.actorName}</td>
                      <td>
                        <code className="audit-action">{a.action}</code>
                      </td>
                      <td>
                        {a.entity}
                        <small className="cell-small mono">
                          {a.entityId.slice(0, 8)}…
                        </small>
                      </td>
                      <td className="truncate-cell">{a.reason || "—"}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {!data.audit.length && <Empty title="No audit events in this view" />}
        </section>
      )}
      {changing && (
        <Modal
          title={`${changing.enabled ? "Disable" : "Enable"} ${changing.name}`}
          subtitle="This configuration change is recorded in the audit trail."
          onClose={() => setChanging(null)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              setBusy(true);
              try {
                await api(`/modules/${changing.key}`, "PUT", {
                  enabled: !changing.enabled,
                  reason,
                });
                await refresh();
                notify("Module configuration updated");
                setChanging(null);
              } catch (err) {
                setError((err as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            <div className="modal-body">
              <div className="info-note">
                <ShieldCheck size={18} />
                <span>
                  Existing records remain retained and accessible according to
                  role permissions. This setting controls the creation of new
                  work.
                </span>
              </div>
              <Field label="Reason for change" required>
                <textarea
                  minLength={10}
                  required
                  rows={3}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Describe the operational reason for this change"
                />
              </Field>
              {error && <ErrorNote>{error}</ErrorNote>}
            </div>
            <div className="modal-footer">
              <button
                className="btn"
                type="button"
                onClick={() => setChanging(null)}
              >
                Cancel
              </button>
              <Submit busy={busy}>Confirm change</Submit>
            </div>
          </form>
        </Modal>
      )}
    </>
  );
}
