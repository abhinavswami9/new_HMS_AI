import { useMemo, useState } from "react";
import {
  BedDouble,
  Download,
  HeartPulse,
  Plus,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { useApp } from "../store";
import { api, downloadCsv, shortDate, time } from "../api";
import {
  Avatar,
  Badge,
  Empty,
  Modal,
  PageHeader,
  SearchBox,
} from "../components";
import { ErrorNote, Field, Submit } from "../components";
import type { Admission, Bed } from "../types";
export function Inpatient() {
  const { data, user } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [admit, setAdmit] = useState(false);
  const [transfer, setTransfer] = useState<Admission | null>(null);
  const [discharge, setDischarge] = useState<Admission | null>(null);
  const [bedAction, setBedAction] = useState<{
    bed: Bed;
    status: string;
  } | null>(null);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const bed = (id: string) => care.beds.find((b) => b.id === id);
  const active = care.admissions.filter((a) => a.status === "Active");
  const available = care.beds.filter((b) => b.status === "Available").length;
  const wards = useMemo(
    () => Array.from(new Set(care.beds.map((b) => b.ward))),
    [care.beds],
  );
  const filtered = care.admissions.filter((a) =>
    `${a.number} ${patient(a.patientId)?.fullName} ${bed(a.bedId)?.number}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const can = (p: string) => user.permissions.includes(p);
  return (
    <>
      <PageHeader
        eyebrow="INPATIENT CARE"
        title="Inpatient & bed board"
        description="Admission, transfer and discharge with a live, auditable view of bed readiness."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-admissions.csv", [
              ["Admission", "Patient", "Bed", "Status", "Admitted"],
              ...filtered.map((a) => [
                a.number,
                patient(a.patientId)?.fullName || "",
                bed(a.bedId)?.number || "",
                a.status,
                a.admittedAt,
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {can("ipd.write") && (
          <button className="btn primary" onClick={() => setAdmit(true)}>
            <Plus size={17} />
            Admit patient
          </button>
        )}
      </PageHeader>
      <div className="finance-grid">
        {[
          ["Active admissions", active.length, HeartPulse, "teal"],
          ["Available beds", available, BedDouble, "green"],
          [
            "Cleaning / maintenance",
            care.beds.filter((b) =>
              ["Cleaning", "Maintenance"].includes(b.status),
            ).length,
            Sparkles,
            "amber",
          ],
          [
            "Occupancy",
            `${care.beds.length ? Math.round((care.beds.filter((b) => b.status === "Occupied").length / care.beds.length) * 100) : 0}%`,
            BedDouble,
            "blue",
          ],
        ].map(([label, value, Icon, color]) => {
          const I = Icon as typeof HeartPulse;
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
      {care.truncated && (
        <div className="info-note standalone">
          <ShieldCheck size={18} />
          <span>
            This care view is bounded. Use patient-level archives for complete
            history.
          </span>
        </div>
      )}
      <section className="card bed-board-card">
        <div className="section-heading">
          <div>
            <h2>Bed board</h2>
            <p className="muted small">
              Occupied beds change only through admission workflows.
            </p>
          </div>
        </div>
        {wards.map((ward) => (
          <div key={ward} className="ward-block">
            <h3>{ward}</h3>
            <div className="bed-grid">
              {care.beds
                .filter((b) => b.ward === ward)
                .map((b) => {
                  const a = active.find((x) => x.bedId === b.id);
                  const p = a ? patient(a.patientId) : null;
                  return (
                    <article
                      key={b.id}
                      className={`bed-card bed-${b.status.toLowerCase()}`}
                    >
                      <div className="bed-card-top">
                        <strong>{b.number}</strong>
                        <Badge status={b.status} />
                      </div>
                      <small className="muted">{b.kind}</small>
                      {p ? (
                        <div className="person-cell bed-person">
                          <Avatar name={p.fullName} />
                          <div>
                            <strong>{p.fullName}</strong>
                            <small>{p.uhid}</small>
                          </div>
                        </div>
                      ) : (
                        <p className="muted small bed-empty">
                          {b.notes || "No patient assigned"}
                        </p>
                      )}
                      <div className="bed-actions">
                        {a && can("ipd.write") && (
                          <button
                            className="btn tiny"
                            onClick={() => setTransfer(a)}
                          >
                            Transfer
                          </button>
                        )}
                        {a && can("ipd.discharge") && (
                          <button
                            className="btn tiny primary"
                            onClick={() => setDischarge(a)}
                          >
                            Discharge
                          </button>
                        )}
                        {b.status === "Cleaning" && can("ipd.bed") && (
                          <button
                            className="btn tiny"
                            onClick={() =>
                              setBedAction({ bed: b, status: "Available" })
                            }
                          >
                            Mark available
                          </button>
                        )}
                        {b.status === "Maintenance" && can("ipd.bed") && (
                          <button
                            className="btn tiny"
                            onClick={() =>
                              setBedAction({ bed: b, status: "Available" })
                            }
                          >
                            Return to service
                          </button>
                        )}
                        {b.status === "Available" && can("ipd.bed") && (
                          <button
                            className="btn tiny"
                            onClick={() =>
                              setBedAction({ bed: b, status: "Maintenance" })
                            }
                          >
                            Maintenance
                          </button>
                        )}
                      </div>
                    </article>
                  );
                })}
            </div>
          </div>
        ))}
      </section>
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={setSearch}
            placeholder="Search admission, patient or bed…"
          />
          <span className="small muted">{filtered.length} admissions</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Admission</th>
                <th>Patient</th>
                <th>Bed</th>
                <th>Consultant</th>
                <th>Admitted</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td>
                    <span className="invoice-link">{a.number}</span>
                    <small className="cell-small">{a.reason}</small>
                  </td>
                  <td>
                    <div className="person-cell">
                      <Avatar
                        name={patient(a.patientId)?.fullName || "Patient"}
                      />
                      <div>
                        <strong>{patient(a.patientId)?.fullName}</strong>
                        <small>{patient(a.patientId)?.uhid}</small>
                      </div>
                    </div>
                  </td>
                  <td>
                    {bed(a.bedId)?.ward} · {bed(a.bedId)?.number}
                  </td>
                  <td>{data.doctors.find((d) => d.id === a.doctorId)?.name}</td>
                  <td>
                    {shortDate(a.admittedAt)}
                    <small className="cell-small">{time(a.admittedAt)}</small>
                  </td>
                  <td>
                    <Badge status={a.status} />
                  </td>
                  <td className="row-actions">
                    {a.status === "Active" && can("ipd.write") && (
                      <button
                        className="btn tiny"
                        onClick={() => setTransfer(a)}
                      >
                        Transfer
                      </button>
                    )}
                    {a.status === "Active" && can("ipd.discharge") && (
                      <button
                        className="btn tiny primary"
                        onClick={() => setDischarge(a)}
                      >
                        Discharge
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <Empty
            title="No admissions in this view"
            description="Admit a patient to an available bed to begin inpatient care."
          />
        )}
      </section>
      {admit && <AdmitModal onClose={() => setAdmit(false)} />}
      {transfer && (
        <TransferModal admission={transfer} onClose={() => setTransfer(null)} />
      )}
      {discharge && (
        <DischargeModal
          admission={discharge}
          onClose={() => setDischarge(null)}
        />
      )}
      {bedAction && (
        <BedStatusModal
          bed={bedAction.bed}
          status={bedAction.status}
          onClose={() => setBedAction(null)}
        />
      )}
    </>
  );
}
function AdmitModal({ onClose }: { onClose: () => void }) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    patientId: "",
    bedId: "",
    doctorId: data.doctors[0]?.id || "",
    reason: "",
  });
  return (
    <Modal
      title="Admit a patient"
      subtitle="A unique admission number is allocated and the bed is occupied atomically."
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/ipd/admissions", "POST", form);
            await refresh();
            notify("Patient admitted successfully");
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
            <Field label="Patient" required>
              <select
                required
                value={form.patientId}
                onChange={(e) =>
                  setForm({ ...form, patientId: e.target.value })
                }
              >
                <option value="">Select patient</option>
                {data.patients.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.fullName} · {p.uhid}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Available bed" required>
              <select
                required
                value={form.bedId}
                onChange={(e) => setForm({ ...form, bedId: e.target.value })}
              >
                <option value="">Select bed</option>
                {data.care.beds
                  .filter((b) => b.status === "Available")
                  .map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.ward} · {b.number} ({b.kind})
                    </option>
                  ))}
              </select>
            </Field>
            <Field label="Consultant" required>
              <select
                required
                value={form.doctorId}
                onChange={(e) => setForm({ ...form, doctorId: e.target.value })}
              >
                {data.doctors.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </Field>
            <div className="span-2">
              <Field label="Admission reason" required>
                <textarea
                  required
                  minLength={5}
                  maxLength={1000}
                  rows={3}
                  value={form.reason}
                  onChange={(e) => setForm({ ...form, reason: e.target.value })}
                />
              </Field>
            </div>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Admit patient</Submit>
        </div>
      </form>
    </Modal>
  );
}
function TransferModal({
  admission,
  onClose,
}: {
  admission: Admission;
  onClose: () => void;
}) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [bedId, setBedId] = useState("");
  const [reason, setReason] = useState("");
  return (
    <Modal
      title="Transfer patient"
      subtitle="The previous bed is released for cleaning; the target bed is occupied."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/ipd/admissions/${admission.id}/transfer`, "POST", {
              bedId,
              reason,
              version: admission.version,
            });
            await refresh();
            notify("Patient transferred");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Target bed" required>
            <select
              required
              value={bedId}
              onChange={(e) => setBedId(e.target.value)}
            >
              <option value="">Select available bed</option>
              {data.care.beds
                .filter(
                  (b) => b.status === "Available" && b.id !== admission.bedId,
                )
                .map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.ward} · {b.number}
                  </option>
                ))}
            </select>
          </Field>
          <Field label="Transfer reason" required>
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
          <Submit busy={busy}>Transfer patient</Submit>
        </div>
      </form>
    </Modal>
  );
}
function DischargeModal({
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
    summary: "",
    diagnosis: "",
    instructions: "",
  });
  const p = data.patients.find((x) => x.id === admission.patientId);
  return (
    <Modal
      title="Clinical discharge"
      subtitle={`${p?.fullName || "Patient"} · ${admission.number}. Discharge records are immutable after sign-off.`}
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/ipd/admissions/${admission.id}/discharge`, "POST", {
              ...form,
              version: admission.version,
            });
            await refresh();
            notify("Patient discharged and summary locked");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Discharge summary" required>
            <textarea
              required
              minLength={20}
              rows={4}
              value={form.summary}
              onChange={(e) => setForm({ ...form, summary: e.target.value })}
            />
          </Field>
          <Field label="Discharge diagnosis" required>
            <input
              required
              minLength={3}
              value={form.diagnosis}
              onChange={(e) => setForm({ ...form, diagnosis: e.target.value })}
            />
          </Field>
          <Field label="Discharge instructions" required>
            <textarea
              required
              minLength={10}
              rows={3}
              value={form.instructions}
              onChange={(e) =>
                setForm({ ...form, instructions: e.target.value })
              }
            />
          </Field>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Sign discharge</Submit>
        </div>
      </form>
    </Modal>
  );
}
function BedStatusModal({
  bed,
  status,
  onClose,
}: {
  bed: Bed;
  status: string;
  onClose: () => void;
}) {
  const { refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [reason, setReason] = useState("");
  return (
    <Modal
      title={`Set bed ${bed.number} to ${status}`}
      subtitle="Bed readiness changes are audited."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(`/ipd/beds/${bed.id}/status`, "PUT", {
              status,
              reason,
              version: bed.version,
            });
            await refresh();
            notify("Bed status updated");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Reason" required>
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
          <Submit busy={busy}>Update bed</Submit>
        </div>
      </form>
    </Modal>
  );
}
