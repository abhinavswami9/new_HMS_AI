import { useState } from "react";
import { AlertTriangle, Plus, ShieldCheck } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import { AlertAckModal } from "./Nursing";
import type { RadiologyOrder } from "../types";
export function Radiology() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [order, setOrder] = useState(false);
  const [schedule, setSchedule] = useState<RadiologyOrder | null>(null);
  const [perform, setPerform] = useState<RadiologyOrder | null>(null);
  const [report, setReport] = useState<RadiologyOrder | null>(null);
  const [cancel, setCancel] = useState<RadiologyOrder | null>(null);
  const [ack, setAck] = useState<string | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const latestReport = (orderId: string) =>
    care.radiologyReports.find((r) => r.orderId === orderId);
  const filtered = care.radiologyOrders.filter(
    (o) =>
      `${o.number} ${o.procedure} ${o.accession ?? ""} ${patient(o.patientId)?.fullName ?? ""}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All" || o.status === status),
  );
  const critical = care.criticalAlerts.filter(
    (a) => a.kind === "Radiology" && a.status === "Open",
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
        eyebrow="RADIOLOGY INFORMATION SYSTEM"
        title="Imaging worklist"
        description="Order-to-authorized reporting with accession numbers, study UIDs and separation of duties. No live PACS/DICOM archive is connected."
      >
        {can("radiology.order") && (
          <button className="btn primary" onClick={() => setOrder(true)}>
            <Plus /> New study
          </button>
        )}
      </PageHeader>
      {critical.length > 0 && (
        <div className="banner error">
          <AlertTriangle size={18} />
          <div>
            <strong>
              {critical.length} critical radiology result
              {critical.length > 1 ? "s" : ""} await acknowledgement
            </strong>
            <div className="muted">
              Communicate the critical finding to the care team and acknowledge
              with a note.
            </div>
          </div>
          <button className="btn" onClick={() => setAck(critical[0].id)}>
            Acknowledge
          </button>
        </div>
      )}
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          {[
            "All",
            "Ordered",
            "Scheduled",
            "Performed",
            "Reported",
            "Authorized",
            "Cancelled",
          ].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </div>
      {filtered.length === 0 ? (
        <Empty
          title="No studies match"
          description="Order an imaging study to start the RIS workflow."
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Accession / number</th>
                <th>Patient</th>
                <th>Study</th>
                <th>Priority</th>
                <th>Status</th>
                <th>Ordered</th>
                <th>Report</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((o) => {
                const report = latestReport(o.id);
                return (
                  <tr key={o.id}>
                    <td>
                      <strong>{o.accession ?? o.number}</strong>
                      <div className="muted">{o.number}</div>
                    </td>
                    <td>{patient(o.patientId)?.fullName ?? "—"}</td>
                    <td>
                      {o.procedure}
                      <div className="muted">
                        {o.modality} · {o.bodyPart} · {o.laterality}
                      </div>
                    </td>
                    <td>
                      <Badge
                        tone={
                          o.priority === "STAT"
                            ? "red"
                            : o.priority === "Urgent"
                              ? "amber"
                              : "gray"
                        }
                      >
                        {o.priority}
                      </Badge>
                    </td>
                    <td>
                      <Badge
                        tone={
                          o.status === "Authorized"
                            ? "green"
                            : o.status === "Cancelled"
                              ? "gray"
                              : "blue"
                        }
                      >
                        {o.status}
                      </Badge>
                    </td>
                    <td>
                      {shortDate(o.orderedAt)}
                      <div className="muted">{time(o.orderedAt)}</div>
                    </td>
                    <td>
                      {report ? (
                        <>
                          <Badge
                            tone={
                              report.critical
                                ? "red"
                                : report.status === "Authorized"
                                  ? "green"
                                  : "amber"
                            }
                          >
                            {report.critical ? "Critical" : report.status}
                          </Badge>
                          {report.contentHash && (
                            <div className="muted">
                              hash {report.contentHash.slice(0, 12)}…
                            </div>
                          )}
                        </>
                      ) : (
                        <span className="muted">—</span>
                      )}
                    </td>
                    <td className="row-actions">
                      {o.status === "Ordered" && can("radiology.schedule") && (
                        <button
                          className="btn small"
                          onClick={() => setSchedule(o)}
                        >
                          Schedule
                        </button>
                      )}
                      {o.status === "Scheduled" && can("radiology.perform") && (
                        <button
                          className="btn small"
                          onClick={() => setPerform(o)}
                        >
                          Perform
                        </button>
                      )}
                      {o.status === "Performed" && can("radiology.report") && (
                        <button
                          className="btn small"
                          onClick={() => setReport(o)}
                        >
                          Report
                        </button>
                      )}
                      {report?.status === "Reported" &&
                        can("radiology.authorize") && (
                          <button
                            className="btn small"
                            onClick={() =>
                              act(
                                `/radiology/orders/${o.id}/authorize`,
                                { reason: "", version: o.version },
                                "Report authorized",
                              )
                            }
                          >
                            <ShieldCheck size={14} /> Authorize
                          </button>
                        )}
                      {(o.status === "Ordered" || o.status === "Scheduled") &&
                        can("radiology.order") && (
                          <button
                            className="btn small ghost"
                            onClick={() => setCancel(o)}
                          >
                            Cancel
                          </button>
                        )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <Modal
        open={order}
        onClose={() => setOrder(false)}
        title="Order imaging study"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/radiology/orders",
              {
                patientId: f.get("patientId"),
                encounterId: null,
                admissionId: null,
                procedure: f.get("procedure"),
                modality: f.get("modality"),
                bodyPart: f.get("bodyPart"),
                laterality: f.get("laterality"),
                priority: f.get("priority"),
                clinicalNote: f.get("clinicalNote"),
              },
              "Study ordered",
            );
            setOrder(false);
          }}
        >
          <Field label="Patient">
            <select name="patientId" required>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <div className="grid-2">
            <Field label="Procedure">
              <input
                name="procedure"
                required
                minLength={3}
                placeholder="CT Brain plain"
              />
            </Field>
            <Field label="Modality">
              <select name="modality">
                {["X-ray", "Ultrasound", "CT", "MRI"].map((m) => (
                  <option key={m}>{m}</option>
                ))}
              </select>
            </Field>
            <Field label="Body part">
              <input
                name="bodyPart"
                required
                minLength={2}
                placeholder="Brain"
              />
            </Field>
            <Field label="Laterality">
              <select name="laterality">
                {["Not applicable", "Left", "Right", "Bilateral"].map((l) => (
                  <option key={l}>{l}</option>
                ))}
              </select>
            </Field>
            <Field label="Priority">
              <select name="priority">
                {["Routine", "Urgent", "STAT"].map((p) => (
                  <option key={p}>{p}</option>
                ))}
              </select>
            </Field>
          </div>
          <Field label="Clinical indication">
            <textarea name="clinicalNote" required minLength={5} rows={3} />
          </Field>
          <Submit label="Order study" />
        </form>
      </Modal>
      <Modal
        open={!!schedule}
        onClose={() => setSchedule(null)}
        title="Schedule study"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/radiology/orders/${schedule!.id}/schedule`,
              {
                at: new Date(String(f.get("at"))).toISOString(),
                version: schedule!.version,
              },
              "Study scheduled",
            );
            setSchedule(null);
          }}
        >
          <Field label="Scheduled time">
            <input type="datetime-local" name="at" required />
          </Field>
          <Submit label="Schedule" />
        </form>
      </Modal>
      <Modal
        open={!!perform}
        onClose={() => setPerform(null)}
        title="Record study acquisition"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/radiology/orders/${perform!.id}/perform`,
              { studyUid: f.get("studyUid"), version: perform!.version },
              "Study performed",
            );
            setPerform(null);
          }}
        >
          <Field label="DICOM Study Instance UID">
            <input
              name="studyUid"
              required
              placeholder="1.2.840.113619.2.55.3.604688119.868.1762000002.789"
            />
          </Field>
          <p className="muted">
            The UID links this worklist entry to images held in the modality or
            archive. This slice stores the identifier only — no archive is
            connected.
          </p>
          <Submit label="Record acquisition" />
        </form>
      </Modal>
      <Modal
        open={!!report}
        onClose={() => setReport(null)}
        title="Record radiology report"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/radiology/orders/${report!.id}/report`,
              {
                findings: f.get("findings"),
                impression: f.get("impression"),
                critical: f.get("critical") === "on",
                version: report!.version,
              },
              "Report recorded",
            );
            setReport(null);
          }}
        >
          <Field label="Findings">
            <textarea name="findings" required minLength={5} rows={4} />
          </Field>
          <Field label="Impression">
            <textarea name="impression" required minLength={5} rows={2} />
          </Field>
          <label className="check-row">
            <input type="checkbox" name="critical" /> Critical result — raise
            alert and communicate
          </label>
          <Submit label="Save report" />
        </form>
      </Modal>
      <Modal
        open={!!cancel}
        onClose={() => setCancel(null)}
        title="Cancel study"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/radiology/orders/${cancel!.id}/cancel`,
              { reason: f.get("reason"), version: cancel!.version },
              "Study cancelled",
            );
            setCancel(null);
          }}
        >
          <Field label="Reason">
            <input name="reason" required minLength={5} />
          </Field>
          <Submit label="Cancel study" tone="danger" />
        </form>
      </Modal>
      {ack && <AlertAckModal id={ack} onClose={() => setAck(null)} />}
    </>
  );
}
