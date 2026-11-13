import { useState } from "react";
import {
  AlertTriangle,
  Download,
  FlaskConical,
  Plus,
  Printer,
  ShieldCheck,
} from "lucide-react";
import { useApp } from "../store";
import { api, downloadCsv, shortDate, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { ErrorNote, Field, Submit } from "../components";
import { AlertAckModal } from "./Nursing";
import type { LabOrder, LabResult } from "../types";
export function Laboratory() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [order, setOrder] = useState(false);
  const [collect, setCollect] = useState<LabOrder | null>(null);
  const [result, setResult] = useState<LabOrder | null>(null);
  const [report, setReport] = useState<LabOrder | null>(null);
  const [reject, setReject] = useState<LabOrder | null>(null);
  const [ack, setAck] = useState<string | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const test = (id: string) => care.labTests.find((t) => t.id === id);
  const latest = (orderId: string) =>
    care.labResults
      .filter((r) => r.orderId === orderId)
      .sort((a, b) => b.version - a.version)[0];
  const filtered = care.labOrders.filter(
    (o) =>
      `${o.number} ${patient(o.patientId)?.fullName} ${test(o.testId)?.name} ${o.barcode}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All" || o.status === status),
  );
  const critical = care.criticalAlerts.filter(
    (a) => a.kind === "Laboratory" && a.status === "Open",
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
        eyebrow="LABORATORY INFORMATION SYSTEM"
        title="Laboratory worklist"
        description="Order-to-report tracking with specimen identity, validation, authorization and critical alerts."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-lab-orders.csv", [
              [
                "Order",
                "Patient",
                "Test",
                "Priority",
                "Status",
                "Barcode",
                "Accession",
              ],
              ...filtered.map((o) => [
                o.number,
                patient(o.patientId)?.fullName || "",
                test(o.testId)?.name || "",
                o.priority,
                o.status,
                o.barcode || "",
                o.accession || "",
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {can("lab.order") && (
          <button className="btn primary" onClick={() => setOrder(true)}>
            <Plus size={17} />
            Order test
          </button>
        )}
      </PageHeader>
      <div className="finance-grid">
        {[
          [
            "Ordered",
            care.labOrders.filter((o) => o.status === "Ordered").length,
            FlaskConical,
            "blue",
          ],
          [
            "In process",
            care.labOrders.filter((o) =>
              ["Collected", "Processing", "Resulted", "Validated"].includes(
                o.status,
              ),
            ).length,
            FlaskConical,
            "amber",
          ],
          [
            "Authorized",
            care.labOrders.filter((o) => o.status === "Authorized").length,
            ShieldCheck,
            "green",
          ],
          ["Critical open", critical.length, AlertTriangle, "teal"],
        ].map(([label, value, Icon, color]) => {
          const I = Icon as typeof FlaskConical;
          return (
            <section className="card finance-card" key={String(label)}>
              <span className={`quick-icon ${color}`}>
                <I size={21} />
              </span>
              <p>{String(label)}</p>
              <strong>{String(value)}</strong>
              <small>Current branch view</small>
            </section>
          );
        })}
      </div>
      {critical.length > 0 && (
        <div className="alert-banner">
          <AlertTriangle size={18} />
          <div>
            <strong>
              {critical.length} critical laboratory alert
              {critical.length > 1 ? "s" : ""}
            </strong>
            <p>{critical[0].message}</p>
          </div>
          {can("lab.acknowledge") && (
            <button className="btn tiny" onClick={() => setAck(critical[0].id)}>
              Acknowledge
            </button>
          )}
        </div>
      )}
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={setSearch}
            placeholder="Search order, patient, test or barcode…"
          />
          <select
            aria-label="Lab status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {[
              "All",
              "Ordered",
              "Collected",
              "Processing",
              "Resulted",
              "Validated",
              "Authorized",
              "Rejected",
            ].map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
          <span className="small muted">{filtered.length} orders</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Order / test</th>
                <th>Patient</th>
                <th>Priority</th>
                <th>Specimen</th>
                <th>Result</th>
                <th>Status</th>
                <th>TAT</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.map((o) => {
                const r = latest(o.id);
                const tat =
                  o.collectedAt && o.authorizedAt
                    ? `${Math.max(0, Math.round((new Date(o.authorizedAt).getTime() - new Date(o.collectedAt).getTime()) / 60000))} min`
                    : "—";
                return (
                  <tr key={o.id}>
                    <td>
                      <span className="invoice-link">{o.number}</span>
                      <small className="cell-small">
                        {test(o.testId)?.name}
                      </small>
                    </td>
                    <td>
                      <strong>{patient(o.patientId)?.fullName}</strong>
                      <small className="cell-small">
                        {patient(o.patientId)?.uhid}
                      </small>
                    </td>
                    <td>
                      <Badge status={o.priority} />
                    </td>
                    <td className="small muted">
                      {o.barcode || "Not collected"}
                      <br />
                      {o.accession || ""}
                    </td>
                    <td>
                      {r ? (
                        <>
                          <strong
                            className={
                              r.flag.startsWith("Critical")
                                ? "warning-text"
                                : ""
                            }
                          >
                            {r.value} {r.unit}
                          </strong>
                          <small className="cell-small">{r.flag}</small>
                        </>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>
                      <Badge status={o.status} />
                    </td>
                    <td>{tat}</td>
                    <td className="row-actions">
                      {o.status === "Ordered" && can("lab.collect") && (
                        <>
                          <button
                            className="btn tiny"
                            onClick={() => setCollect(o)}
                          >
                            Collect
                          </button>
                          <button
                            className="btn tiny"
                            onClick={() => setReject(o)}
                          >
                            Reject
                          </button>
                        </>
                      )}
                      {o.status === "Collected" && can("lab.process") && (
                        <button
                          className="btn tiny"
                          onClick={() =>
                            act(
                              `/lab/orders/${o.id}/process`,
                              { version: o.version },
                              "Specimen entered processing",
                            )
                          }
                        >
                          Process
                        </button>
                      )}
                      {["Processing", "Resulted"].includes(o.status) &&
                        can("lab.result") && (
                          <button
                            className="btn tiny"
                            onClick={() => setResult(o)}
                          >
                            {r ? "New result version" : "Enter result"}
                          </button>
                        )}
                      {o.status === "Resulted" && can("lab.validate") && (
                        <button
                          className="btn tiny"
                          onClick={() =>
                            act(
                              `/lab/orders/${o.id}/validate`,
                              { version: o.version },
                              "Result technically validated",
                            )
                          }
                        >
                          Validate
                        </button>
                      )}
                      {o.status === "Validated" && can("lab.authorize") && (
                        <button
                          className="btn tiny primary"
                          onClick={() =>
                            act(
                              `/lab/orders/${o.id}/authorize`,
                              { version: o.version },
                              "Report authorized",
                            )
                          }
                        >
                          Authorize
                        </button>
                      )}
                      {o.status === "Authorized" && (
                        <button
                          className="btn tiny"
                          onClick={() => setReport(o)}
                        >
                          <Printer size={14} />
                          Report
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <Empty
            title="No laboratory orders in this view"
            description="Order a test or adjust filters."
          />
        )}
      </section>
      <div className="info-note standalone">
        <ShieldCheck size={18} />
        <span>
          Analyzer interfaces, QC rules and NABL accreditation are
          integration/release gates. Authorized reports are immutable;
          corrections require an addendum workflow.
        </span>
      </div>
      {order && <OrderModal onClose={() => setOrder(false)} />}
      {collect && (
        <CollectModal order={collect} onClose={() => setCollect(null)} />
      )}
      {result && <ResultModal order={result} onClose={() => setResult(null)} />}
      {reject && <RejectModal order={reject} onClose={() => setReject(null)} />}
      {report && (
        <ReportModal
          order={report}
          result={latest(report.id)}
          onClose={() => setReport(null)}
        />
      )}
      {ack && <AlertAckModal id={ack} onClose={() => setAck(null)} />}
    </>
  );
}
function OrderModal({ onClose }: { onClose: () => void }) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    patientId: "",
    testId: data.care.labTests[0]?.id || "",
    priority: "Routine",
    clinicalNote: "",
  });
  return (
    <Modal
      title="Order laboratory test"
      subtitle="Orders are tracked from specimen collection through authorized report."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/lab/orders", "POST", {
              ...form,
              encounterId: null,
              admissionId: null,
            });
            await refresh();
            notify("Laboratory test ordered");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Patient" required>
            <select
              required
              value={form.patientId}
              onChange={(e) => setForm({ ...form, patientId: e.target.value })}
            >
              <option value="">Select patient</option>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Test" required>
            <select
              required
              value={form.testId}
              onChange={(e) => setForm({ ...form, testId: e.target.value })}
            >
              {data.care.labTests.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} ({t.specimenType})
                </option>
              ))}
            </select>
          </Field>
          <Field label="Priority" required>
            <select
              value={form.priority}
              onChange={(e) => setForm({ ...form, priority: e.target.value })}
            >
              {["Routine", "Urgent", "STAT"].map((p) => (
                <option key={p}>{p}</option>
              ))}
            </select>
          </Field>
          <Field label="Clinical note">
            <textarea
              rows={3}
              value={form.clinicalNote}
              onChange={(e) =>
                setForm({ ...form, clinicalNote: e.target.value })
              }
            />
          </Field>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Order test</Submit>
        </div>
      </form>
    </Modal>
  );
}
function CollectModal({
  order,
  onClose,
}: {
  order: LabOrder;
  onClose: () => void;
}) {
  const { data, refresh, notify } = useApp();
  const test = data.care.labTests.find((t) => t.id === order.testId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [volume, setVolume] = useState("2");
  return (
    <Modal
      title="Collect specimen"
      subtitle={`${test?.name} · expected specimen: ${test?.specimenType}`}
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/lab/orders/${order.id}/collect`, "POST", {
              specimenType: test?.specimenType || "",
              volumeMl: Number(volume),
              version: order.version,
            });
            await refresh();
            notify("Specimen collected and accessioned");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Specimen type" required>
            <input required readOnly value={test?.specimenType || ""} />
          </Field>
          <Field label="Volume mL" required>
            <input
              required
              type="number"
              min="0.1"
              step="0.1"
              value={volume}
              onChange={(e) => setVolume(e.target.value)}
            />
          </Field>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Collect specimen</Submit>
        </div>
      </form>
    </Modal>
  );
}
function ResultModal({
  order,
  onClose,
}: {
  order: LabOrder;
  onClose: () => void;
}) {
  const { data, refresh, notify } = useApp();
  const test = data.care.labTests.find((t) => t.id === order.testId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    value: "",
    unit: test?.unit || "",
    referenceLow: test?.referenceLow?.toString() || "",
    referenceHigh: test?.referenceHigh?.toString() || "",
  });
  return (
    <Modal
      title="Enter laboratory result"
      subtitle="Critical values create open alerts. Validation requires a second qualified staff member."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/lab/orders/${order.id}/results`, "POST", {
              value: form.value,
              unit: form.unit,
              referenceLow:
                form.referenceLow === "" ? null : Number(form.referenceLow),
              referenceHigh:
                form.referenceHigh === "" ? null : Number(form.referenceHigh),
              version: order.version,
            });
            await refresh();
            notify("Result entered");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Result value" required>
            <input
              required
              value={form.value}
              onChange={(e) => setForm({ ...form, value: e.target.value })}
            />
          </Field>
          <div className="form-grid">
            <Field label="Unit" required>
              <input
                required
                value={form.unit}
                onChange={(e) => setForm({ ...form, unit: e.target.value })}
              />
            </Field>
            <Field label="Reference low">
              <input
                type="number"
                step="any"
                value={form.referenceLow}
                onChange={(e) =>
                  setForm({ ...form, referenceLow: e.target.value })
                }
              />
            </Field>
            <Field label="Reference high">
              <input
                type="number"
                step="any"
                value={form.referenceHigh}
                onChange={(e) =>
                  setForm({ ...form, referenceHigh: e.target.value })
                }
              />
            </Field>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Save result</Submit>
        </div>
      </form>
    </Modal>
  );
}
function RejectModal({
  order,
  onClose,
}: {
  order: LabOrder;
  onClose: () => void;
}) {
  const { refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [reason, setReason] = useState("");
  return (
    <Modal
      title="Reject specimen"
      subtitle="Rejection is terminal for this order and preserves the reason."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/lab/orders/${order.id}/reject`, "POST", {
              version: order.version,
              reason,
            });
            await refresh();
            notify("Specimen rejected");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Rejection reason" required>
            <textarea
              required
              minLength={5}
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </Field>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Reject specimen</Submit>
        </div>
      </form>
    </Modal>
  );
}
function ReportModal({
  order,
  result,
  onClose,
}: {
  order: LabOrder;
  result?: LabResult;
  onClose: () => void;
}) {
  const { data } = useApp();
  const p = data.patients.find((x) => x.id === order.patientId);
  const t = data.care.labTests.find((x) => x.id === order.testId);
  return (
    <Modal
      title="Authorized laboratory report"
      subtitle={`${order.number} · ${order.accession}`}
      onClose={onClose}
      wide
    >
      <div className="modal-body print-area">
        <div className="print-brand">
          careflow<span>{data.hospitalName}</span>
        </div>
        <h3>
          {p?.fullName} · {p?.uhid}
        </h3>
        <p className="muted small">
          {t?.name} · {order.priority} · Authorized{" "}
          {order.authorizedAt
            ? `${shortDate(order.authorizedAt)} ${time(order.authorizedAt)}`
            : ""}
        </p>
        <div className="report-result">
          <strong>
            {result?.value} {result?.unit}
          </strong>
          <span
            className={
              result?.flag.startsWith("Critical") ? "warning-text" : ""
            }
          >
            {result?.flag}
          </span>
          <small>
            Reference: {result?.referenceLow ?? "—"} –{" "}
            {result?.referenceHigh ?? "—"} {result?.unit}
          </small>
        </div>
        <p className="small muted">
          Content hash: {result?.contentHash || "—"}
        </p>
        <p className="small muted">
          Synthetic preview report. Not for clinical use.
        </p>
      </div>
      <div className="modal-footer">
        <button className="btn" onClick={() => window.print()}>
          <Printer size={16} />
          Print
        </button>
        <button className="btn primary" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  );
}
