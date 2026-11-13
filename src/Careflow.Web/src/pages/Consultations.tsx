import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Check,
  ClipboardList,
  FileCheck2,
  FileText,
  FlaskConical,
  Heart,
  Pill,
  Plus,
  Printer,
  Save,
  ShieldCheck,
  Stethoscope,
  Trash2,
} from "lucide-react";
import { useApp } from "../store";
import {
  Avatar,
  Badge,
  Empty,
  ErrorNote,
  Field,
  Modal,
  PageHeader,
  SearchBox,
} from "../components";
import { age, api, shortDate, time } from "../api";
import type { Encounter, PrescriptionItem } from "../types";
export function Consultations() {
  const { data, demo } = useApp();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All records");
  const navigate = useNavigate();
  const records = data.encounters.filter(
    (e) =>
      (status === "All records" || e.status === status) &&
      `${data.patients.find((p) => p.id === e.patientId)?.fullName} ${e.diagnosis}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  return (
    <>
      <PageHeader
        eyebrow="CLINICAL WORKSPACE"
        title="Consultations & medical records"
        description="The context you need. The tools to deliver better care."
      >
        <Link to="/appointments" className="btn primary">
          <Stethoscope size={17} />
          Open patient queue
        </Link>
      </PageHeader>
      {demo && (
        <div className="info-note standalone">
          <ShieldCheck size={18} />
          <span>
            Clinical editing is restricted to the assigned doctor. Use the demo
            role selector to switch from Hospital Admin to Doctor.
          </span>
        </div>
      )}
      <div className="summary-strip">
        <div>
          <span className="quick-icon amber">
            <ClipboardList size={21} />
          </span>
          <span>
            Awaiting sign-off
            <strong>
              {data.encounters.filter((e) => e.status === "Draft").length}
            </strong>
          </span>
        </div>
        <div>
          <span className="quick-icon teal">
            <FileCheck2 size={21} />
          </span>
          <span>
            Signed records
            <strong>
              {data.encounters.filter((e) => e.status === "Signed").length}
            </strong>
          </span>
        </div>
        <p>
          <ShieldCheck size={18} />
          Signed clinical content is immutable
        </p>
      </div>
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={setSearch}
            placeholder="Search patient or diagnosis…"
          />
          <select
            aria-label="Clinical record status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {["All records", "Draft", "Signed"].map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
          <span className="small muted">{records.length} records</span>
        </div>
        {records.length ? (
          <div className="record-list">
            {records.map((e) => {
              const p = data.patients.find((p) => p.id === e.patientId),
                d = data.doctors.find((d) => d.id === e.doctorId);
              return (
                <button
                  key={e.id}
                  onClick={() => navigate(`/consultations/${e.id}`)}
                >
                  <Avatar name={p?.fullName || "Patient"} />
                  <div>
                    <strong>
                      {p?.fullName}
                      <span className="muted small"> · {p?.uhid}</span>
                    </strong>
                    <p>
                      {e.diagnosis || e.chiefComplaint || "New consultation"}
                    </p>
                    <small>
                      {d?.name} · {shortDate(e.updatedAt)}
                    </small>
                  </div>
                  <Badge status={e.status} />
                  <ArrowRight size={17} />
                </button>
              );
            })}
          </div>
        ) : (
          <Empty
            title="No clinical records in this view"
            description="Start a consultation from a checked-in appointment, or adjust your search."
          />
        )}
      </section>
    </>
  );
}
export function ConsultationDetail() {
  const { id } = useParams();
  const { data, user, refresh, notify, demo, switchRole } = useApp();
  const record = data.encounters.find((e) => e.id === id);
  const [form, setForm] = useState<Encounter | null>(record || null);
  const [tab, setTab] = useState("Consultation");
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [medicineId, setMedicineId] = useState("");
  const [allergy, setAllergy] = useState("");
  useEffect(() => {
    if (record) {
      setForm(record);
      setDirty(false);
    }
  }, [record?.id, record?.version]);
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (dirty) e.preventDefault();
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
  if (!record || !form)
    return (
      <Empty
        title="Clinical record not available"
        description="The record may not exist or your current role may not have access."
      >
        <Link className="btn" to="/consultations">
          Back to clinical workspace
        </Link>
      </Empty>
    );
  const patient = data.patients.find((p) => p.id === form.patientId);
  const doctor = data.doctors.find((d) => d.id === form.doctorId);
  const editable =
    form.status === "Draft" &&
    user.permissions.includes("clinical.sign") &&
    user.id === form.doctorId;
  const change = <K extends keyof Encounter>(key: K, value: Encounter[K]) => {
    setForm((f) => (f ? { ...f, [key]: value } : f));
    setDirty(true);
  };
  const save = async () => {
    if (!editable) return;
    setBusy(true);
    setError("");
    try {
      const result = await api<Encounter>(`/encounters/${id}/draft`, "PUT", {
        ...form,
      });
      setForm(result);
      setDirty(false);
      await refresh();
      notify("Consultation draft saved");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  const addMedicine = () => {
    const m = data.medicines.find((m) => m.id === medicineId);
    if (!m || form.prescription.some((p) => p.medicineId === m.id)) return;
    change("prescription", [
      ...form.prescription,
      {
        medicineId: m.id,
        name: `${m.genericName} ${m.strength}`,
        dose: "",
        frequency: "",
        durationDays: 1,
        instructions: "",
      },
    ]);
    setMedicineId("");
  };
  const updateMed = (
    index: number,
    key: keyof PrescriptionItem,
    value: string | number,
  ) =>
    change(
      "prescription",
      form.prescription.map((p, i) =>
        i === index ? { ...p, [key]: value } : p,
      ),
    );
  return (
    <>
      <div className="clinical-topline">
        <Link to="/consultations" className="back-link">
          <ArrowLeft size={16} />
          Clinical workspace
        </Link>
        <div>
          <span className="muted small">
            {dirty ? "Unsaved changes" : "All changes saved"}
          </span>
          <Badge status={form.status} />
        </div>
      </div>
      <div className="patient-banner compact">
        <Avatar name={patient?.fullName || "Patient"} size="large" />
        <div className="patient-identity">
          <div>
            <h1>{patient?.fullName}</h1>
            <span className="uhid">{patient?.uhid}</span>
          </div>
          <p>
            {patient && `${age(patient.dateOfBirth)} years · ${patient.sex}`}
            <span className="dot-separator">·</span>
            {doctor?.name}
            <span className="dot-separator">·</span>
            {shortDate(form.updatedAt)}
          </p>
        </div>
        <Link className="btn" to={`/patients/${patient?.id}`}>
          Patient chart <ArrowRight size={15} />
        </Link>
        {form.status === "Signed" && form.prescription.length > 0 && (
          <button className="btn" onClick={() => window.print()}>
            <Printer size={16} />
            Print prescription
          </button>
        )}
      </div>
      {!editable && form.status === "Draft" && (
        <div className="info-note standalone">
          <ShieldCheck size={18} />
          <span>
            Read-only: only {doctor?.name} can edit and sign this consultation.
            {demo && user.role === "Hospital Admin" && (
              <button
                className="text-link ml-8"
                onClick={() => void switchRole("Doctor")}
              >
                Switch to Doctor preview <ArrowRight size={13} />
              </button>
            )}
          </span>
        </div>
      )}
      <div className="clinical-grid">
        <section className="card clinical-editor">
          <div className="tabs inside">
            {[
              ["Consultation", ClipboardList],
              ["Prescription", Pill],
              ["Orders", FlaskConical],
            ].map(([name, Icon]) => {
              const I = Icon as typeof Pill;
              return (
                <button
                  key={String(name)}
                  className={tab === name ? "active" : ""}
                  onClick={() => setTab(String(name))}
                >
                  <I size={16} />
                  {String(name)}
                  {name === "Prescription" && form.prescription.length > 0 && (
                    <span>{form.prescription.length}</span>
                  )}
                </button>
              );
            })}
          </div>
          <fieldset disabled={!editable || busy} className="editor-fieldset">
            {tab === "Consultation" && (
              <div className="card-body">
                <div className="form-section-label">
                  <Heart size={17} />
                  Patient vitals
                </div>
                <div className="vitals-inputs">
                  {[
                    ["temperatureC", "Temperature", "°C", "0.1"],
                    ["pulse", "Pulse", "bpm", "1"],
                    ["systolic", "Systolic", "mmHg", "1"],
                    ["diastolic", "Diastolic", "mmHg", "1"],
                    ["weightKg", "Weight", "kg", "0.1"],
                    ["spo2", "SpO₂", "%", "1"],
                  ].map(([key, label, unit, step]) => (
                    <Field key={key} label={`${label} (${unit})`}>
                      <input
                        type="number"
                        step={step}
                        value={
                          form.vitals[key as keyof typeof form.vitals] ?? ""
                        }
                        onChange={(e) =>
                          change("vitals", {
                            ...form.vitals,
                            [key]:
                              e.target.value === "" ? null : +e.target.value,
                          })
                        }
                        placeholder="—"
                      />
                    </Field>
                  ))}
                </div>
                <div className="section-divider" />
                <Field label="Chief complaint" required>
                  <textarea
                    rows={2}
                    maxLength={10000}
                    value={form.chiefComplaint}
                    onChange={(e) => change("chiefComplaint", e.target.value)}
                    placeholder="Patient’s presenting symptoms and duration"
                  />
                </Field>
                <Field label="Clinical examination">
                  <textarea
                    rows={3}
                    maxLength={10000}
                    value={form.examination}
                    onChange={(e) => change("examination", e.target.value)}
                    placeholder="Examination findings and clinical observations"
                  />
                </Field>
                <Field
                  label="Diagnosis"
                  required
                  hint="Free-text diagnosis in this slice. ICD coding integration is planned."
                >
                  <input
                    maxLength={10000}
                    value={form.diagnosis}
                    onChange={(e) => change("diagnosis", e.target.value)}
                    placeholder="Working or confirmed diagnosis"
                  />
                </Field>
                <Field label="Treatment plan">
                  <textarea
                    rows={3}
                    maxLength={10000}
                    value={form.plan}
                    onChange={(e) => change("plan", e.target.value)}
                    placeholder="Treatment, patient education and care plan"
                  />
                </Field>
                <Field label="Follow-up instructions">
                  <input
                    maxLength={10000}
                    value={form.followUp}
                    onChange={(e) => change("followUp", e.target.value)}
                    placeholder="Review interval, precautions and referral instructions"
                  />
                </Field>
              </div>
            )}
            {tab === "Prescription" && (
              <div className="card-body">
                <div className="section-heading">
                  <div>
                    <h3>Digital prescription</h3>
                    <p className="muted small">
                      Medicines are resolved from the hospital formulary.
                    </p>
                  </div>
                  <Pill size={20} className="teal-text" />
                </div>
                {editable && (
                  <div className="add-row">
                    <select
                      aria-label="Select medicine to prescribe"
                      value={medicineId}
                      onChange={(e) => setMedicineId(e.target.value)}
                    >
                      <option value="">Search and select a medicine…</option>
                      {data.medicines.map((m) => (
                        <option key={m.id} value={m.id}>
                          {m.genericName} {m.strength} · {m.brandName} ({m.form}
                          )
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="btn"
                      disabled={!medicineId}
                      onClick={addMedicine}
                    >
                      <Plus size={16} />
                      Add
                    </button>
                  </div>
                )}
                {form.prescription.length ? (
                  <div className="prescription-items">
                    {form.prescription.map((p, i) => (
                      <div className="prescription-item" key={p.medicineId}>
                        <div className="section-heading">
                          <strong>
                            <Pill size={16} />
                            {p.name}
                          </strong>
                          {editable && (
                            <button
                              type="button"
                              className="icon-btn"
                              aria-label={`Remove ${p.name}`}
                              onClick={() =>
                                change(
                                  "prescription",
                                  form.prescription.filter((_, n) => n !== i),
                                )
                              }
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
                        </div>
                        <div className="form-grid three">
                          <Field label="Dose" required>
                            <input
                              required
                              value={p.dose}
                              maxLength={80}
                              placeholder="e.g. 1 tablet"
                              onChange={(e) =>
                                updateMed(i, "dose", e.target.value)
                              }
                            />
                          </Field>
                          <Field label="Frequency" required>
                            <input
                              required
                              value={p.frequency}
                              maxLength={80}
                              placeholder="e.g. Twice daily"
                              onChange={(e) =>
                                updateMed(i, "frequency", e.target.value)
                              }
                            />
                          </Field>
                          <Field label="Duration (days)" required>
                            <input
                              required
                              type="number"
                              min={1}
                              max={365}
                              value={p.durationDays}
                              onChange={(e) =>
                                updateMed(i, "durationDays", +e.target.value)
                              }
                            />
                          </Field>
                        </div>
                        <Field label="Instructions">
                          <input
                            maxLength={500}
                            value={p.instructions}
                            placeholder="Route, timing and patient instructions"
                            onChange={(e) =>
                              updateMed(i, "instructions", e.target.value)
                            }
                          />
                        </Field>
                      </div>
                    ))}
                  </div>
                ) : (
                  <Empty
                    title="No medicines added"
                    description="Select a medicine above, then enter its dose, frequency and duration."
                  />
                )}
                <label className="checkbox-note">
                  <input
                    type="checkbox"
                    checked={form.allergyReviewed}
                    onChange={(e) =>
                      change("allergyReviewed", e.target.checked)
                    }
                  />
                  <span>
                    <strong>I have reconciled the patient’s allergies.</strong>
                    <small>
                      Required before signing. Basic name matching is not a
                      substitute for clinical review; a certified interaction
                      database is not connected.
                    </small>
                  </span>
                </label>
              </div>
            )}
            {tab === "Orders" && (
              <div className="card-body">
                <div className="section-heading">
                  <div>
                    <h3>Investigation & procedure orders</h3>
                    <p className="muted small">
                      Orders are recorded here; diagnostic fulfillment is not
                      connected.
                    </p>
                  </div>
                  <FlaskConical size={20} className="teal-text" />
                </div>
                {editable && (
                  <button
                    type="button"
                    className="btn"
                    onClick={() =>
                      change("orders", [
                        ...form.orders,
                        { kind: "Laboratory", name: "", instructions: "" },
                      ])
                    }
                  >
                    <Plus size={16} />
                    Add order
                  </button>
                )}
                {form.orders.length ? (
                  <div className="order-items">
                    {form.orders.map((o, i) => (
                      <div className="order-item" key={i}>
                        <div className="form-grid">
                          <Field label="Order type">
                            <select
                              value={o.kind}
                              onChange={(e) =>
                                change(
                                  "orders",
                                  form.orders.map((x, n) =>
                                    n === i
                                      ? { ...x, kind: e.target.value }
                                      : x,
                                  ),
                                )
                              }
                            >
                              {["Laboratory", "Radiology", "Procedure"].map(
                                (x) => (
                                  <option key={x}>{x}</option>
                                ),
                              )}
                            </select>
                          </Field>
                          <Field label="Investigation / procedure" required>
                            <input
                              required
                              maxLength={200}
                              value={o.name}
                              placeholder="e.g. Complete blood count"
                              onChange={(e) =>
                                change(
                                  "orders",
                                  form.orders.map((x, n) =>
                                    n === i
                                      ? { ...x, name: e.target.value }
                                      : x,
                                  ),
                                )
                              }
                            />
                          </Field>
                        </div>
                        <div className="add-row">
                          <input
                            aria-label="Order instructions"
                            maxLength={1000}
                            value={o.instructions}
                            placeholder="Clinical indication and instructions"
                            onChange={(e) =>
                              change(
                                "orders",
                                form.orders.map((x, n) =>
                                  n === i
                                    ? { ...x, instructions: e.target.value }
                                    : x,
                                ),
                              )
                            }
                          />
                          {editable && (
                            <button
                              type="button"
                              className="icon-btn"
                              aria-label={`Remove order ${i + 1}`}
                              onClick={() =>
                                change(
                                  "orders",
                                  form.orders.filter((_, n) => n !== i),
                                )
                              }
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <Empty
                    title="No investigation orders"
                    description="Add laboratory, radiology or procedure orders to the care plan."
                  />
                )}
              </div>
            )}
          </fieldset>
          {error && (
            <div className="card-body">
              <ErrorNote>{error}</ErrorNote>
            </div>
          )}
          {editable && (
            <div className="editor-footer">
              <span>
                <ShieldCheck size={15} />
                {dirty
                  ? "Save your changes before signing"
                  : "Draft · version " + form.version}
              </span>
              <div>
                <button
                  className="btn"
                  disabled={busy || !dirty}
                  onClick={() => void save()}
                >
                  <Save size={16} />
                  Save draft
                </button>
                <button
                  className="btn primary"
                  disabled={busy || dirty}
                  onClick={() => setConfirm(true)}
                >
                  <FileCheck2 size={16} />
                  Review & sign
                </button>
              </div>
            </div>
          )}
          {form.status === "Signed" && (
            <div className="signed-note">
              <ShieldCheck size={18} />
              <div>
                <strong>Electronically signed by {form.signedBy}</strong>
                <p>
                  {form.signedAt &&
                    `${shortDate(form.signedAt)} at ${time(form.signedAt)}`}{" "}
                  · This record is locked. Corrections require a new addendum
                  workflow.
                </p>
              </div>
            </div>
          )}
        </section>
        <aside className="clinical-aside">
          <section className="card">
            <div className="card-header">
              <h2>
                <Heart size={17} />
                Allergies & alerts
              </h2>
            </div>
            <div className="card-body">
              {form.allergies.map((a, i) => (
                <div className="allergy-item" key={i}>
                  <div>
                    <strong>{a.substance}</strong>
                    <span>{a.reaction}</span>
                  </div>
                  {editable && (
                    <button
                      className="icon-btn"
                      aria-label={`Remove allergy ${a.substance}`}
                      onClick={() =>
                        change(
                          "allergies",
                          form.allergies.filter((_, n) => n !== i),
                        )
                      }
                    >
                      <XIcon />
                    </button>
                  )}
                  <span className="badge amber">{a.severity}</span>
                </div>
              ))}
              {!form.allergies.length && (
                <p className="muted small">
                  No allergies recorded. Confirm with the patient before
                  prescribing.
                </p>
              )}
              {editable && (
                <div className="add-row">
                  <input
                    aria-label="Allergy substance and reaction"
                    value={allergy}
                    maxLength={200}
                    placeholder="Substance — reaction"
                    onChange={(e) => setAllergy(e.target.value)}
                  />
                  <button
                    className="icon-btn"
                    aria-label="Add allergy"
                    disabled={!allergy.trim()}
                    onClick={() => {
                      change("allergies", [
                        ...form.allergies,
                        {
                          substance: allergy.trim(),
                          reaction: "Reported by patient; review required",
                          severity: "Review",
                        },
                      ]);
                      setAllergy("");
                    }}
                  >
                    <Plus size={17} />
                  </button>
                </div>
              )}
            </div>
          </section>
          <section className="card mt-20">
            <div className="card-header">
              <h2>Previous visits</h2>
              <Link className="text-link" to={`/patients/${patient?.id}`}>
                View chart <ArrowRight size={13} />
              </Link>
            </div>
            <div className="previous-visits">
              {data.encounters
                .filter(
                  (e) => e.patientId === form.patientId && e.id !== form.id,
                )
                .slice(0, 4)
                .map((e) => (
                  <Link key={e.id} to={`/consultations/${e.id}`}>
                    <span className="timeline-icon">
                      <FileText size={16} />
                    </span>
                    <div>
                      <strong>{e.diagnosis || "Consultation"}</strong>
                      <small>{shortDate(e.updatedAt)}</small>
                    </div>
                    <Badge status={e.status} />
                  </Link>
                ))}
              {!data.encounters.some(
                (e) => e.patientId === form.patientId && e.id !== form.id,
              ) && (
                <p className="muted small">
                  No other recent visits for this patient.
                </p>
              )}
            </div>
          </section>
          <div className="clinical-safety">
            <ShieldCheck size={22} />
            <strong>Care with confidence</strong>
            <p>
              Every draft revision and clinical sign-off is recorded in the
              audit trail.
            </p>
            <span>Human review. Always.</span>
          </div>
        </aside>
      </div>
      {confirm && (
        <Modal
          title="Review and sign this record"
          subtitle="This action finalizes the current clinical content."
          onClose={() => setConfirm(false)}
        >
          <div className="modal-body">
            <div className="sign-summary">
              <FileCheck2 size={30} />
              <h3>{patient?.fullName}</h3>
              <p>{form.diagnosis}</p>
              <span>
                {form.prescription.length} medicines · {form.orders.length}{" "}
                orders
              </span>
            </div>
            <div className="info-note">
              <ShieldCheck size={19} />
              <span>
                By signing, you confirm that you have reviewed this record and
                reconciled allergies. Signed content cannot be overwritten. This
                preview records an electronic sign-off, not a certificate-based
                digital signature.
              </span>
            </div>
          </div>
          <div className="modal-footer">
            <button className="btn" onClick={() => setConfirm(false)}>
              Keep reviewing
            </button>
            <button
              className="btn primary"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                try {
                  await api(`/encounters/${id}/sign`, "POST", {
                    version: form.version,
                  });
                  await refresh();
                  setConfirm(false);
                  notify("Consultation signed and locked");
                } catch (e) {
                  notify((e as Error).message, "error");
                } finally {
                  setBusy(false);
                }
              }}
            >
              <Check size={16} />
              Confirm & sign
            </button>
          </div>
        </Modal>
      )}
      {form.status === "Signed" && (
        <div className="print-area">
          <div className="print-brand">
            careflow<span>Nova Care Hospital · Main branch</span>
          </div>
          <h2>Medical prescription</h2>
          <p>
            <strong>{patient?.fullName}</strong> ·{" "}
            {patient && age(patient.dateOfBirth)} years · {patient?.sex}
            <br />
            {patient?.uhid} · {shortDate(form.updatedAt)}
          </p>
          <p>
            <strong>Diagnosis:</strong> {form.diagnosis}
          </p>
          <p>
            <strong>Chief complaint:</strong> {form.chiefComplaint}
          </p>
          <table>
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Dose</th>
                <th>Frequency</th>
                <th>Duration</th>
                <th>Instructions</th>
              </tr>
            </thead>
            <tbody>
              {form.prescription.map((p) => (
                <tr key={p.medicineId}>
                  <td>{p.name}</td>
                  <td>{p.dose}</td>
                  <td>{p.frequency}</td>
                  <td>{p.durationDays} days</td>
                  <td>{p.instructions}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            <strong>Plan:</strong> {form.plan}
          </p>
          <p>
            <strong>Follow-up:</strong> {form.followUp}
          </p>
          <div className="print-signature">
            Electronically recorded by {form.signedBy}
            <br />
            {form.signedAt && shortDate(form.signedAt)}
            <br />
            <small>Content hash: {form.contentHash}</small>
          </div>
          <p className="small">
            Synthetic development record — not valid for medical use. No
            certificate-based signature.
          </p>
        </div>
      )}
    </>
  );
}
function XIcon() {
  return <Trash2 size={14} />;
}
