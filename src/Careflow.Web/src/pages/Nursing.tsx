import { useState } from "react";
import { AlertTriangle, Download, Pill, Plus } from "lucide-react";
import { useApp } from "../store";
import { api, downloadCsv, localInputDate, shortDate, time } from "../api";
import { Avatar, Badge, Empty, Modal, PageHeader } from "../components";
import { ErrorNote, Field, Submit } from "../components";
import type { Admission, MedicationOrder } from "../types";
export function Nursing() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const active = care.admissions.filter((a) => a.status === "Active");
  const [selectedId, setSelectedId] = useState(active[0]?.id || "");
  const selected = active.find((a) => a.id === selectedId) || active[0];
  const [note, setNote] = useState(false);
  const [order, setOrder] = useState(false);
  const [administer, setAdminister] = useState<MedicationOrder | null>(null);
  const [handover, setHandover] = useState(false);
  const [ackAlert, setAckAlert] = useState<string | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const notes = care.nursingNotes.filter((n) => n.admissionId === selected?.id);
  const orders = care.medicationOrders.filter(
    (o) => o.admissionId === selected?.id,
  );
  const alerts = care.criticalAlerts.filter(
    (a) => a.kind === "Nursing" && a.status === "Open",
  );
  return (
    <>
      <PageHeader
        eyebrow="NURSING CARE"
        title="Nursing station"
        description="Assessments, vitals, intake/output, eMAR and shift handover for admitted patients."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-nursing-notes.csv", [
              ["Admission", "Patient", "Kind", "Note", "Recorded"],
              ...care.nursingNotes.map((n) => [
                care.admissions.find((a) => a.id === n.admissionId)?.number ||
                  "",
                patient(n.patientId)?.fullName || "",
                n.kind,
                n.text,
                n.createdAt,
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {can("nursing.handover") && (
          <button className="btn" onClick={() => setHandover(true)}>
            Shift handover
          </button>
        )}
        {selected && can("nursing.write") && (
          <button className="btn primary" onClick={() => setNote(true)}>
            <Plus size={17} />
            Nursing note
          </button>
        )}
      </PageHeader>
      {alerts.length > 0 && (
        <div className="alert-banner">
          <AlertTriangle size={18} />
          <div>
            <strong>
              {alerts.length} open nursing escalation
              {alerts.length > 1 ? "s" : ""}
            </strong>
            <p>{alerts[0].message}</p>
          </div>
          {can("nursing.write") && (
            <button
              className="btn tiny"
              onClick={() => setAckAlert(alerts[0].id)}
            >
              Acknowledge
            </button>
          )}
        </div>
      )}
      <div className="care-layout">
        <section className="card care-patient-list">
          <h2>Admitted patients</h2>
          {active.map((a) => {
            const p = patient(a.patientId);
            const b = care.beds.find((x) => x.id === a.bedId);
            return (
              <button
                key={a.id}
                className={`care-patient ${selected?.id === a.id ? "selected" : ""}`}
                onClick={() => setSelectedId(a.id)}
              >
                <Avatar name={p?.fullName || "Patient"} />
                <span>
                  <strong>{p?.fullName}</strong>
                  <small>
                    {b?.ward} · {b?.number} · {a.number}
                  </small>
                </span>
              </button>
            );
          })}
          {!active.length && (
            <Empty
              title="No active admissions"
              description="Admitted patients appear here."
            />
          )}
        </section>
        <div className="care-main">
          {selected ? (
            <>
              <section className="card">
                <div className="section-heading">
                  <div>
                    <h2>{patient(selected.patientId)?.fullName}</h2>
                    <p className="muted small">
                      {selected.number} ·{" "}
                      {care.beds.find((b) => b.id === selected.bedId)?.number} ·
                      Consultant{" "}
                      {
                        data.doctors.find((d) => d.id === selected.doctorId)
                          ?.name
                      }
                    </p>
                  </div>
                  <Badge status={selected.status} />
                </div>
                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Recorded</th>
                        <th>Type</th>
                        <th>Note</th>
                        <th>Vitals</th>
                        <th>I/O</th>
                        <th>Pain / fall</th>
                      </tr>
                    </thead>
                    <tbody>
                      {notes.map((n) => (
                        <tr key={n.id}>
                          <td>
                            {shortDate(n.createdAt)}
                            <small className="cell-small">
                              {time(n.createdAt)}
                            </small>
                          </td>
                          <td>
                            <Badge status={n.kind} />
                          </td>
                          <td className="note-cell">{n.text}</td>
                          <td className="small muted">
                            {n.vitals.temperatureC ?? "—"}°C ·{" "}
                            {n.vitals.pulse ?? "—"} bpm ·{" "}
                            {n.vitals.systolic ?? "—"}/
                            {n.vitals.diastolic ?? "—"} · SpO₂{" "}
                            {n.vitals.spo2 ?? "—"}%
                          </td>
                          <td>
                            {n.intakeMl ?? "—"} / {n.outputMl ?? "—"} mL
                          </td>
                          <td>
                            {n.painScore ?? "—"} / {n.fallRisk}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {!notes.length && (
                  <Empty
                    title="No nursing notes yet"
                    description="Record the first assessment for this admission."
                  />
                )}
              </section>
              <section className="card">
                <div className="section-heading">
                  <div>
                    <h2>Medication administration record</h2>
                    <p className="muted small">
                      Administer only against an active order and unexpired
                      stock batch.
                    </p>
                  </div>
                  {can("nursing.order") && (
                    <button
                      className="btn tiny primary"
                      onClick={() => setOrder(true)}
                    >
                      <Pill size={15} />
                      Order medication
                    </button>
                  )}
                </div>
                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Medicine</th>
                        <th>Dose / route</th>
                        <th>Frequency</th>
                        <th>Window</th>
                        <th>Status</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {orders.map((o) => (
                        <tr key={o.id}>
                          <td>
                            <strong>{o.name}</strong>
                          </td>
                          <td>
                            {o.dose} · {o.route}
                          </td>
                          <td>{o.frequency}</td>
                          <td className="small muted">
                            {shortDate(o.startsAt)} {time(o.startsAt)}
                            {o.endsAt ? ` → ${shortDate(o.endsAt)}` : ""}
                          </td>
                          <td>
                            <Badge status={o.status} />
                          </td>
                          <td className="row-actions">
                            {o.status === "Active" &&
                              can("nursing.administer") && (
                                <button
                                  className="btn tiny"
                                  onClick={() => setAdminister(o)}
                                >
                                  Administer
                                </button>
                              )}
                            {o.status === "Active" && can("nursing.order") && (
                              <button
                                className="btn tiny"
                                onClick={async () => {
                                  try {
                                    await api(
                                      `/nursing/medication-orders/${o.id}/stop`,
                                      "POST",
                                      {
                                        version: o.version,
                                        reason: "Stopped from nursing station",
                                      },
                                    );
                                    await refresh();
                                    notify("Medication order stopped");
                                  } catch (e) {
                                    notify((e as Error).message, "error");
                                  }
                                }}
                              >
                                Stop
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <h3 className="subsection-title">Recent administrations</h3>
                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Given</th>
                        <th>Order</th>
                        <th>Quantity</th>
                        <th>Batch</th>
                        <th>Notes</th>
                      </tr>
                    </thead>
                    <tbody>
                      {care.administrations
                        .filter((a) => a.admissionId === selected.id)
                        .map((a) => (
                          <tr key={a.id}>
                            <td>
                              {shortDate(a.givenAt)} {time(a.givenAt)}
                            </td>
                            <td>
                              {orders.find((o) => o.id === a.orderId)?.name ||
                                a.orderId}
                            </td>
                            <td>{a.quantity}</td>
                            <td>
                              {care.stockBatches.find((b) => b.id === a.batchId)
                                ?.batchNumber || "—"}
                            </td>
                            <td className="small muted">{a.notes || "—"}</td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </>
          ) : (
            <Empty
              title="Select an admitted patient"
              description="Nursing workflows require an active admission."
            />
          )}
          <section className="card">
            <div className="section-heading">
              <h2>Shift handovers</h2>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Created</th>
                    <th>Incoming</th>
                    <th>Summary</th>
                    <th>Patients</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {care.handovers.map((h) => (
                    <tr key={h.id}>
                      <td>
                        {shortDate(h.createdAt)} {time(h.createdAt)}
                      </td>
                      <td>{h.toNurseName}</td>
                      <td className="note-cell">{h.summary}</td>
                      <td>{h.patientIds.length}</td>
                      <td>
                        <Badge status={h.status} />
                      </td>
                      <td>
                        {h.status === "Pending" &&
                          can("nursing.handover") &&
                          h.toNurseId === user.id && (
                            <button
                              className="btn tiny"
                              onClick={async () => {
                                try {
                                  await api(
                                    `/nursing/handovers/${h.id}/acknowledge`,
                                    "POST",
                                    {
                                      version: h.version,
                                      reason: "Handover received",
                                    },
                                  );
                                  await refresh();
                                  notify("Handover acknowledged");
                                } catch (e) {
                                  notify((e as Error).message, "error");
                                }
                              }}
                            >
                              Acknowledge
                            </button>
                          )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </div>
      {note && selected && (
        <NursingNoteModal admission={selected} onClose={() => setNote(false)} />
      )}
      {order && selected && (
        <MedicationOrderModal
          admission={selected}
          onClose={() => setOrder(false)}
        />
      )}
      {administer && (
        <AdministerModal
          order={administer}
          onClose={() => setAdminister(null)}
        />
      )}
      {handover && <HandoverModal onClose={() => setHandover(false)} />}
      {ackAlert && (
        <AlertAckModal id={ackAlert} onClose={() => setAckAlert(null)} />
      )}
    </>
  );
}
function NursingNoteModal({
  admission,
  onClose,
}: {
  admission: Admission;
  onClose: () => void;
}) {
  const { refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    kind: "Assessment",
    text: "",
    temperatureC: "",
    pulse: "",
    systolic: "",
    diastolic: "",
    spo2: "",
    intakeMl: "",
    outputMl: "",
    painScore: "",
    fallRisk: "Low",
  });
  const num = (v: string) => (v === "" ? null : Number(v));
  return (
    <Modal
      title="Record nursing note"
      subtitle="Notes are append-only. Abnormal observations create escalation alerts."
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/nursing/notes", "POST", {
              admissionId: admission.id,
              kind: form.kind,
              text: form.text,
              vitals: {
                temperatureC: num(form.temperatureC),
                pulse: num(form.pulse),
                systolic: num(form.systolic),
                diastolic: num(form.diastolic),
                weightKg: null,
                spo2: num(form.spo2),
              },
              intakeMl: num(form.intakeMl),
              outputMl: num(form.outputMl),
              painScore: num(form.painScore),
              fallRisk: form.fallRisk,
            });
            await refresh();
            notify("Nursing note recorded");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <div className="form-grid">
            <Field label="Note type" required>
              <select
                value={form.kind}
                onChange={(e) => setForm({ ...form, kind: e.target.value })}
              >
                {["Assessment", "Progress", "IntakeOutput"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <Field label="Fall risk" required>
              <select
                value={form.fallRisk}
                onChange={(e) => setForm({ ...form, fallRisk: e.target.value })}
              >
                {["Low", "Moderate", "High"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <div className="span-2">
              <Field label="Nursing note" required>
                <textarea
                  required
                  minLength={5}
                  rows={3}
                  value={form.text}
                  onChange={(e) => setForm({ ...form, text: e.target.value })}
                />
              </Field>
            </div>
            {[
              ["temperatureC", "Temperature °C"],
              ["pulse", "Pulse"],
              ["systolic", "Systolic"],
              ["diastolic", "Diastolic"],
              ["spo2", "SpO₂ %"],
              ["intakeMl", "Intake mL"],
              ["outputMl", "Output mL"],
              ["painScore", "Pain score 0–10"],
            ].map(([key, label]) => (
              <Field key={key} label={label}>
                <input
                  type="number"
                  step="any"
                  value={form[key as keyof typeof form]}
                  onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                />
              </Field>
            ))}
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Record note</Submit>
        </div>
      </form>
    </Modal>
  );
}
function MedicationOrderModal({
  admission,
  onClose,
}: {
  admission: Admission;
  onClose: () => void;
}) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    medicineId: data.medicines[0]?.id || "",
    dose: "",
    route: "Oral",
    frequency: "",
    startsAt: localInputDate(new Date()),
    endsAt: "",
  });
  return (
    <Modal
      title="Order inpatient medication"
      subtitle="Orders are checked against the formulary and recorded allergies."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/nursing/medication-orders", "POST", {
              admissionId: admission.id,
              ...form,
              startsAt: new Date(form.startsAt).toISOString(),
              endsAt: form.endsAt ? new Date(form.endsAt).toISOString() : null,
            });
            await refresh();
            notify("Medication order created");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Medicine" required>
            <select
              required
              value={form.medicineId}
              onChange={(e) => setForm({ ...form, medicineId: e.target.value })}
            >
              {data.medicines.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.genericName} {m.strength} ({m.brandName})
                </option>
              ))}
            </select>
          </Field>
          <div className="form-grid">
            <Field label="Dose" required>
              <input
                required
                value={form.dose}
                onChange={(e) => setForm({ ...form, dose: e.target.value })}
                placeholder="1 tablet"
              />
            </Field>
            <Field label="Route" required>
              <input
                required
                value={form.route}
                onChange={(e) => setForm({ ...form, route: e.target.value })}
              />
            </Field>
            <Field label="Frequency" required>
              <input
                required
                value={form.frequency}
                onChange={(e) =>
                  setForm({ ...form, frequency: e.target.value })
                }
                placeholder="Twice daily"
              />
            </Field>
            <Field label="Starts at" required>
              <input
                type="datetime-local"
                required
                value={form.startsAt}
                onChange={(e) => setForm({ ...form, startsAt: e.target.value })}
              />
            </Field>
            <Field label="Ends at">
              <input
                type="datetime-local"
                value={form.endsAt}
                onChange={(e) => setForm({ ...form, endsAt: e.target.value })}
              />
            </Field>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Create order</Submit>
        </div>
      </form>
    </Modal>
  );
}
function AdministerModal({
  order,
  onClose,
}: {
  order: MedicationOrder;
  onClose: () => void;
}) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const batches = data.care.stockBatches.filter(
    (b) =>
      b.medicineId === order.medicineId &&
      b.status === "Active" &&
      b.quantity > 0,
  );
  const [form, setForm] = useState({
    batchId: batches[0]?.id || "",
    quantity: "1",
    givenAt: localInputDate(new Date()),
    witness: "",
    notes: "",
  });
  return (
    <Modal
      title="Record administration"
      subtitle={`${order.name} · ${order.dose} · ${order.route}`}
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/nursing/administrations", "POST", {
              orderId: order.id,
              batchId: form.batchId,
              quantity: Number(form.quantity),
              givenAt: new Date(form.givenAt).toISOString(),
              witness: form.witness,
              notes: form.notes,
            });
            await refresh();
            notify("Administration recorded and stock updated");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Stock batch" required>
            <select
              required
              value={form.batchId}
              onChange={(e) => setForm({ ...form, batchId: e.target.value })}
            >
              <option value="">Select batch</option>
              {batches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.batchNumber} · exp {b.expiryDate} · qty {b.quantity}
                </option>
              ))}
            </select>
          </Field>
          <div className="form-grid">
            <Field label="Quantity" required>
              <input
                required
                type="number"
                min="0.001"
                step="0.001"
                value={form.quantity}
                onChange={(e) => setForm({ ...form, quantity: e.target.value })}
              />
            </Field>
            <Field label="Given at" required>
              <input
                required
                type="datetime-local"
                value={form.givenAt}
                onChange={(e) => setForm({ ...form, givenAt: e.target.value })}
              />
            </Field>
            <Field label="Witness">
              <input
                value={form.witness}
                onChange={(e) => setForm({ ...form, witness: e.target.value })}
              />
            </Field>
            <Field label="Notes">
              <input
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </Field>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Record administration</Submit>
        </div>
      </form>
    </Modal>
  );
}
function HandoverModal({ onClose }: { onClose: () => void }) {
  const { data, user, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const active = data.care.admissions.filter((a) => a.status === "Active");
  const nurses = data.care.nurses.filter((n) => n.id !== user.id);
  const [toNurseId, setTo] = useState(nurses[0]?.id || "");
  const [summary, setSummary] = useState("");
  const [ids, setIds] = useState<string[]>(active.map((a) => a.patientId));
  return (
    <Modal
      title="Create shift handover"
      subtitle="The incoming nurse must acknowledge the handover."
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/nursing/handovers", "POST", {
              toNurseId,
              summary,
              patientIds: ids,
            });
            await refresh();
            notify("Handover created");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Incoming nurse" required>
            <select
              required
              value={toNurseId}
              onChange={(e) => setTo(e.target.value)}
            >
              <option value="">Select nurse</option>
              {nurses.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.name}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Handover summary" required>
            <textarea
              required
              minLength={20}
              rows={4}
              value={summary}
              onChange={(e) => setSummary(e.target.value)}
            />
          </Field>
          <div className="checkbox-list">
            {active.map((a) => {
              const p = data.patients.find((x) => x.id === a.patientId);
              return (
                <label key={a.id}>
                  <input
                    type="checkbox"
                    checked={ids.includes(a.patientId)}
                    onChange={(e) =>
                      setIds(
                        e.target.checked
                          ? [...ids, a.patientId]
                          : ids.filter((x) => x !== a.patientId),
                      )
                    }
                  />
                  {p?.fullName} · {a.number}
                </label>
              );
            })}
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Create handover</Submit>
        </div>
      </form>
    </Modal>
  );
}
export function AlertAckModal({
  id,
  onClose,
}: {
  id: string;
  onClose: () => void;
}) {
  const { refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [note, setNote] = useState("");
  return (
    <Modal
      title="Acknowledge critical alert"
      subtitle="Record the action taken or escalation made."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/alerts/${id}/acknowledge`, "POST", { note });
            await refresh();
            notify("Alert acknowledged");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Acknowledgement note" required>
            <textarea
              required
              minLength={5}
              rows={3}
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
          </Field>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Acknowledge</Submit>
        </div>
      </form>
    </Modal>
  );
}
