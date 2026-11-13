import { useRef, useState } from "react";
import {
  CalendarDays,
  Check,
  CreditCard,
  ShieldCheck,
  UserRoundPlus,
} from "lucide-react";
import { useApp } from "./store";
import { api, localInputDate, money } from "./api";
import { ErrorNote, Field, Modal, Submit } from "./components";
import type { Invoice, Patient } from "./types";
export function RegistrationModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated?: (p: Patient) => void;
}) {
  const { refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [reason, setReason] = useState("");
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    dateOfBirth: "",
    sex: "Female",
    phone: "",
    email: "",
    bloodGroup: "Unknown",
    address: "",
    emergencyContact: "",
  });
  const set = (key: keyof typeof form, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));
  return (
    <Modal
      title="Register a new patient"
      subtitle="A unique UHID is assigned after registration."
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            const p = await api<Patient>("/patients", "POST", {
              ...form,
              duplicateReviewReason: reason,
            });
            await refresh();
            notify(`${p.fullName} registered · ${p.uhid}`);
            onCreated?.(p);
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <div className="form-section-label">
            <UserRoundPlus size={17} />
            Patient demographics
          </div>
          <div className="form-grid">
            <Field label="First name" required>
              <input
                required
                maxLength={80}
                value={form.firstName}
                onChange={(e) => set("firstName", e.target.value)}
                placeholder="e.g. Ananya"
              />
            </Field>
            <Field label="Last name" required>
              <input
                required
                maxLength={80}
                value={form.lastName}
                onChange={(e) => set("lastName", e.target.value)}
                placeholder="e.g. Sharma"
              />
            </Field>
            <Field label="Date of birth" required>
              <input
                type="date"
                required
                max={new Date().toISOString().slice(0, 10)}
                value={form.dateOfBirth}
                onChange={(e) => set("dateOfBirth", e.target.value)}
              />
            </Field>
            <Field label="Sex / gender" required>
              <select
                value={form.sex}
                onChange={(e) => set("sex", e.target.value)}
              >
                {["Female", "Male", "Other", "Undisclosed"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <Field label="Mobile number">
              <input
                type="tel"
                maxLength={20}
                value={form.phone}
                onChange={(e) => set("phone", e.target.value)}
                placeholder="+91 98765 43210"
              />
            </Field>
            <Field label="Email address">
              <input
                type="email"
                value={form.email}
                onChange={(e) => set("email", e.target.value)}
                placeholder="patient@example.com"
              />
            </Field>
            <Field label="Blood group">
              <select
                value={form.bloodGroup}
                onChange={(e) => set("bloodGroup", e.target.value)}
              >
                {[
                  "Unknown",
                  "A+",
                  "A-",
                  "B+",
                  "B-",
                  "AB+",
                  "AB-",
                  "O+",
                  "O-",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <Field label="Emergency contact">
              <input
                maxLength={200}
                value={form.emergencyContact}
                onChange={(e) => set("emergencyContact", e.target.value)}
                placeholder="Name and phone number"
              />
            </Field>
            <div className="span-2">
              <Field label="Residential address">
                <textarea
                  rows={2}
                  maxLength={500}
                  value={form.address}
                  onChange={(e) => set("address", e.target.value)}
                  placeholder="House, street, city, state and postal code"
                />
              </Field>
            </div>
          </div>
          <div className="info-note">
            <ShieldCheck size={18} />
            <span>
              Possible duplicates are checked before a new UHID is created.
              Fields marked * are required.
            </span>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
          {error.toLowerCase().includes("duplicate") || reason ? (
            <Field
              label="Duplicate-review reason"
              hint="Explain why this is a distinct patient after reviewing the registry."
            >
              <textarea
                required
                minLength={10}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </Field>
          ) : null}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Register patient</Submit>
        </div>
      </form>
    </Modal>
  );
}
export function BookingModal({
  onClose,
  patientId,
}: {
  onClose: () => void;
  patientId?: string;
}) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    patientId: patientId || "",
    doctorId: data.doctors[0]?.id || "",
    startsAt: localInputDate(new Date(Date.now() + 60 * 60000)),
    durationMinutes: 20,
    mode: "In person",
    reason: "",
  });
  return (
    <Modal
      title="Book an appointment"
      subtitle="Choose a patient, practitioner and available time."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/appointments", "POST", {
              ...form,
              startsAt: new Date(form.startsAt).toISOString(),
            });
            await refresh();
            notify("Appointment booked successfully");
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
              <option value="">Select a registered patient</option>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Practitioner" required>
            <select
              value={form.doctorId}
              onChange={(e) => setForm({ ...form, doctorId: e.target.value })}
            >
              {data.doctors.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} · {d.department}
                </option>
              ))}
            </select>
          </Field>
          <div className="form-grid">
            <Field label="Date and time" required>
              <input
                type="datetime-local"
                required
                value={form.startsAt}
                onChange={(e) => setForm({ ...form, startsAt: e.target.value })}
              />
            </Field>
            <Field label="Duration">
              <select
                value={form.durationMinutes}
                onChange={(e) =>
                  setForm({ ...form, durationMinutes: +e.target.value })
                }
              >
                {[15, 20, 30, 45, 60].map((x) => (
                  <option key={x} value={x}>
                    {x} minutes
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <Field label="Appointment type">
            <select
              value={form.mode}
              onChange={(e) => setForm({ ...form, mode: e.target.value })}
            >
              <option>In person</option>
              <option>Follow-up</option>
              {data.modules.find((m) => m.key === "telemedicine")?.enabled && (
                <option>Telemedicine</option>
              )}
            </select>
          </Field>
          <Field label="Reason for visit">
            <textarea
              rows={3}
              maxLength={500}
              value={form.reason}
              onChange={(e) => setForm({ ...form, reason: e.target.value })}
              placeholder="Brief reason or referral notes"
            />
          </Field>
          <div className="info-note">
            <CalendarDays size={18} />
            <span>
              Availability is validated against the practitioner’s schedule.
              Tokens are assigned at check-in.
            </span>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button className="btn" type="button" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Book appointment</Submit>
        </div>
      </form>
    </Modal>
  );
}
export function PaymentModal({
  invoice,
  onClose,
}: {
  invoice: Invoice;
  onClose: () => void;
}) {
  const { refresh, notify } = useApp();
  const [amount, setAmount] = useState(invoice.balance);
  const [method, setMethod] = useState("UPI");
  const [reference, setReference] = useState(
    `DEMO-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <Modal
      title="Record a payment"
      subtitle={`${invoice.number} · ${money(invoice.balance)} outstanding`}
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            await api(`/invoices/${invoice.id}/payments`, "POST", {
              amount,
              method,
              reference,
              version: invoice.version,
            });
            await refresh();
            notify("Payment recorded and receipt updated");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <div className="payment-total">
            <CreditCard size={24} />
            <span>
              Amount to collect<strong>{money(amount || 0)}</strong>
            </span>
          </div>
          <Field label="Amount (INR)" required>
            <input
              type="number"
              min="0.01"
              max={invoice.balance}
              step="0.01"
              required
              value={amount}
              onChange={(e) => setAmount(+e.target.value)}
            />
          </Field>
          <Field label="Payment method">
            <select value={method} onChange={(e) => setMethod(e.target.value)}>
              {["UPI", "Cash", "Card", "Bank transfer"].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </Field>
          <Field label="Payment reference" required>
            <input
              required
              maxLength={100}
              value={reference}
              onChange={(e) => setReference(e.target.value)}
            />
          </Field>
          <div className="info-note">
            <ShieldCheck size={18} />
            <span>
              This records a payment received outside Careflow. No payment
              gateway is connected in this preview.
            </span>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button className="btn" type="button" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>
            <Check size={16} />
            Confirm payment
          </Submit>
        </div>
      </form>
    </Modal>
  );
}
export function InvoiceModal({
  onClose,
  patientId,
}: {
  onClose: () => void;
  patientId?: string;
}) {
  const { data, refresh, notify } = useApp();
  const [patient, setPatient] = useState(patientId || "");
  const [selected, setSelected] = useState<Record<string, number>>({});
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const [error, setError] = useState("");
  const total = data.services.reduce(
    (s, x) => s + (selected[x.id] || 0) * x.price,
    0,
  );
  return (
    <Modal
      title="Create an invoice"
      subtitle="Prices are resolved from the hospital service catalog."
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          if (submitting.current) return;
          submitting.current = true;
          setBusy(true);
          try {
            await api("/invoices", "POST", {
              patientId: patient,
              lines: Object.entries(selected)
                .filter(([, q]) => q > 0)
                .map(([serviceId, quantity]) => ({ serviceId, quantity })),
            });
            await refresh();
            notify(
              "Draft invoice created. Review and issue it to collect payment.",
            );
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            submitting.current = false;
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Patient" required>
            <select
              required
              value={patient}
              onChange={(e) => setPatient(e.target.value)}
            >
              <option value="">Select patient</option>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <div className="form-section-label">Services and charges</div>
          <div className="service-picker">
            {data.services.map((s) => (
              <div key={s.id}>
                <label>
                  <input
                    type="checkbox"
                    checked={!!selected[s.id]}
                    onChange={(e) =>
                      setSelected({
                        ...selected,
                        [s.id]: e.target.checked ? 1 : 0,
                      })
                    }
                  />
                  <span>
                    {s.name}
                    <small>{s.department}</small>
                  </span>
                </label>
                {selected[s.id] ? (
                  <input
                    aria-label={`${s.name} quantity`}
                    type="number"
                    min={1}
                    max={100}
                    value={selected[s.id]}
                    onChange={(e) =>
                      setSelected({
                        ...selected,
                        [s.id]: Math.min(100, Math.max(1, +e.target.value)),
                      })
                    }
                  />
                ) : (
                  <strong>{money(s.price)}</strong>
                )}
              </div>
            ))}
          </div>
          <div className="invoice-total">
            <span>Invoice total</span>
            <strong>{money(total)}</strong>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button
            className="btn"
            type="button"
            onClick={onClose}
            disabled={busy}
          >
            Cancel
          </button>
          <Submit busy={busy}>Create draft invoice</Submit>
        </div>
      </form>
    </Modal>
  );
}
