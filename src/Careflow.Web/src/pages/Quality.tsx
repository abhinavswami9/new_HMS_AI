import { useState } from "react";
import { ShieldAlert, FileCheck2 } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { Capa, QualityIncident } from "../types";
export function Quality() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [incident, setIncident] = useState(false);
  const [close, setClose] = useState<QualityIncident | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string | null) =>
    id ? data.patients.find((p) => p.id === id)?.fullName : "—";
  const filtered = care.incidents.filter((i) =>
    `${i.number} ${i.category} ${i.location}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const act = async (path: string, body: unknown, message: string) => {
    try {
      await api(path, "POST", body);
      await refresh();
      notify(message);
    } catch (e) {
      notify((e as Error).message, "error");
    }
  };
  return (
    <>
      <PageHeader
        eyebrow="QUALITY & INFECTION CONTROL"
        title="Incidents, CAPA & evidence"
        description="Just-culture incident reporting, infection surveillance, risk register and CAPA with effectiveness verification before closure. NABH/NABL-oriented evidence records — not an accreditation certification."
      >
        {can("quality.incident") && (
          <button className="btn primary" onClick={() => setIncident(true)}>
            <ShieldAlert size={16} /> Report incident
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
      </div>
      <div className="split-2">
        <div className="panel">
          <h3>Incidents</h3>
          <table>
            <thead>
              <tr>
                <th>Number</th>
                <th>Category</th>
                <th>Location</th>
                <th>Patient</th>
                <th>Severity</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((i) => (
                <tr key={i.id}>
                  <td>
                    <strong>{i.number}</strong>
                  </td>
                  <td>{i.category}</td>
                  <td>{i.location}</td>
                  <td>{patient(i.patientId)}</td>
                  <td>{i.severity}</td>
                  <td>
                    <Badge>{i.status}</Badge>
                  </td>
                  <td>
                    {can("quality.capa") && i.status === "Reported" && (
                      <button
                        className="btn ghost"
                        onClick={() =>
                          void act(
                            `/quality/incidents/${i.id}/triage`,
                            {
                              triageNote: "Under investigation by the quality desk.",
                              assignedTo: user.name,
                              version: i.version,
                            },
                            "Incident triaged",
                          )
                        }
                      >
                        Triage
                      </button>
                    )}
                    {can("quality.capa") && i.status === "UnderInvestigation" && (
                      <button className="btn ghost" onClick={() => setClose(i)}>
                        Close
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && <Empty title="No incidents reported" />}
        </div>
        <div className="panel">
          <h3>CAPA actions</h3>
          {care.capas.length === 0 && <Empty title="No CAPA actions open" />}
          {care.capas.map((c: Capa) => (
            <div key={c.id} className="list-row">
              <div>
                <strong>{c.number}</strong>
                <small>
                  {c.owner} · due {shortDate(c.dueDate)} ·{" "}
                  {c.effectivenessVerified ? "verified" : "effectiveness pending"}
                </small>
              </div>
              <div>
                <Badge>{c.status}</Badge>
                {can("quality.capa") && c.status !== "Closed" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/quality/capa/${c.id}/verify`,
                        {
                          effectivenessEvidence:
                            "Spot checks and audit sampling confirm the change is holding.",
                          version: c.version,
                        },
                        "Effectiveness verified",
                      )
                    }
                  >
                    Verify
                  </button>
                )}
                {can("quality.capa") && c.status === "Verified" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/quality/capa/${c.id}/close`,
                        {
                          reason: "Effectiveness verified across three audits.",
                          version: c.version,
                        },
                        "CAPA closed",
                      )
                    }
                  >
                    Close
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="split-2">
        <div className="panel">
          <h3>Infection surveillance</h3>
          {care.infections.length === 0 && (
            <Empty title="No infection surveillance reports" />
          )}
          {care.infections.map((x) => (
            <div key={x.id} className="list-row">
              <div>
                <strong>{x.number}</strong>
                <small>
                  {x.category} · {x.site} · {patient(x.patientId)} ·{" "}
                  {shortDate(x.detectedOn)}
                </small>
              </div>
              <Badge>{x.status}</Badge>
            </div>
          ))}
          <h3>Quality indicators</h3>
          {care.qualityIndicators.length === 0 && (
            <Empty title="No indicator definitions" />
          )}
          {care.qualityIndicators.map((x) => (
            <div key={x.id} className="list-row">
              <div>
                <strong>
                  {x.code} · {x.name}
                </strong>
                <small>
                  {x.numerator} ÷ {x.denominator} · {x.period}
                </small>
              </div>
              <Badge>{x.status}</Badge>
            </div>
          ))}
        </div>
        <div className="panel">
          <h3>Controlled documents</h3>
          {care.qualityDocuments.length === 0 && (
            <Empty title="No controlled documents" />
          )}
          {care.qualityDocuments.map((d) => (
            <div key={d.id} className="list-row">
              <div>
                <strong>{d.title}</strong>
                <small>
                  {d.number} · {d.category} · v{d.currentVersion} · {d.owner}
                </small>
              </div>
              <div>
                <Badge>{d.status}</Badge>
                {can("quality.read") && d.status === "Published" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/quality/documents/${d.id}/acknowledge`,
                        { attestation: "Read and understood; will apply on shift." },
                        "Document acknowledged",
                      )
                    }
                  >
                    <FileCheck2 size={14} /> Acknowledge
                  </button>
                )}
              </div>
            </div>
          ))}
          <h3>Risk register</h3>
          {care.risks.length === 0 && <Empty title="No risks assessed" />}
          {care.risks.map((r) => (
            <div key={r.id} className="list-row">
              <div>
                <strong>{r.domain}</strong>
                <small>
                  score {r.score} · {r.owner}
                </small>
              </div>
              <Badge>{r.status}</Badge>
            </div>
          ))}
        </div>
      </div>
      {incident && (
        <Modal title="Report a safety incident" onClose={() => setIncident(false)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/quality/incidents", "POST", {
                  category: f.get("category"),
                  severity: f.get("severity"),
                  description: f.get("description"),
                  location: f.get("location"),
                  patientId: null,
                });
                await refresh();
                notify("Incident reported");
                setIncident(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <div className="form-grid">
              <Field label="Category">
                <select name="category">
                  <option>Medication</option>
                  <option>Fall</option>
                  <option>Documentation</option>
                  <option>Equipment</option>
                  <option>Infection control</option>
                  <option>Near miss</option>
                  <option>Security</option>
                  <option>Other</option>
                </select>
              </Field>
              <Field label="Severity">
                <select name="severity">
                  <option>Mild</option>
                  <option>Moderate</option>
                  <option>Severe</option>
                  <option>Life-threatening</option>
                </select>
              </Field>
            </div>
            <Field label="Location">
              <input name="location" required />
            </Field>
            <Field label="Description">
              <textarea name="description" rows={4} required />
            </Field>
            <p className="muted">
              Reports are confidential to the quality desk — just culture, not blame.
            </p>
            <Submit label="Submit report" />
          </form>
        </Modal>
      )}
      {close && (
        <Modal title={`Close ${close.number}`} onClose={() => setClose(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(`/quality/incidents/${close.id}/close`, "POST", {
                  investigation: f.get("investigation"),
                  rootCause: f.get("rootCause"),
                  version: close.version,
                });
                await refresh();
                notify("Incident closed");
                setClose(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <p className="muted">
              Linked CAPA actions must be closed before an incident can close.
            </p>
            <Field label="Investigation">
              <textarea name="investigation" rows={3} required />
            </Field>
            <Field label="Root cause">
              <textarea name="rootCause" rows={2} required />
            </Field>
            <Submit label="Close incident" />
          </form>
        </Modal>
      )}
    </>
  );
}
