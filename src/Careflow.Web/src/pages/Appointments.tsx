import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  CalendarDays,
  Check,
  Clock3,
  Plus,
  Stethoscope,
  Users,
  X,
  RefreshCw,
  MoreHorizontal,
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
  Submit,
} from "../components";
import { BookingModal } from "../forms";
import { api, localInputDate, time } from "../api";
import type { Appointment } from "../types";
export function Appointments() {
  const { data, user, refresh, notify } = useApp();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All statuses");
  const [doctor, setDoctor] = useState("All practitioners");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [book, setBook] = useState(false);
  const [selected, setSelected] = useState<Appointment | null>(null);
  const [action, setAction] = useState<"cancel" | "reschedule" | null>(null);
  const [reason, setReason] = useState("");
  const [newDate, setNewDate] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const canWrite = user.permissions.includes("schedule.write");
  const daily = data.appointments.filter(
    (a) => !date || new Date(a.startsAt).toISOString().slice(0, 10) === date,
  );
  const filtered = daily
    .filter((a) => {
      const p = data.patients.find((p) => p.id === a.patientId);
      return (
        `${p?.fullName} ${p?.uhid} ${a.token || ""}`
          .toLowerCase()
          .includes(search.toLowerCase()) &&
        (status === "All statuses" || a.status === status) &&
        (doctor === "All practitioners" || a.doctorId === doctor)
      );
    })
    .sort(
      (a, b) =>
        (a.token || 999) - (b.token || 999) ||
        a.startsAt.localeCompare(b.startsAt),
    );
  const transition = async (a: Appointment, next: string) => {
    setBusy(true);
    try {
      if (next === "start") {
        const e = await api<{ id: string }>(
          `/appointments/${a.id}/encounter`,
          "POST",
          {},
        );
        await refresh();
        navigate(`/consultations/${e.id}`);
      } else {
        await api(`/appointments/${a.id}/${next}`, "POST", {
          version: a.version,
          reason: "",
        });
        await refresh();
        notify(
          next === "check-in"
            ? "Patient checked in and token assigned"
            : "Visit marked complete",
        );
      }
      setSelected(null);
    } catch (e) {
      notify((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <PageHeader
        eyebrow="SCHEDULING & PATIENT FLOW"
        title="Appointments & live queue"
        description="From the first booking to the next step in care."
      >
        <button className="btn" onClick={() => setDate("")}>
          <CalendarDays size={16} />
          {date ? "Show all dates" : "Showing all dates"}
        </button>
        {canWrite && (
          <button className="btn primary" onClick={() => setBook(true)}>
            <Plus size={17} />
            New appointment
          </button>
        )}
      </PageHeader>
      <div className="queue-stats">
        {[
          ["Scheduled", "Booked", CalendarDays, "blue"],
          ["Waiting", "Checked in", Users, "amber"],
          ["In consultation", "In consultation", Stethoscope, "teal"],
          ["Completed", "Completed", Check, "green"],
        ].map(([label, value, Icon, color]) => {
          const I = Icon as typeof Users;
          return (
            <button
              key={String(label)}
              className={`card queue-stat ${status === value ? "selected" : ""}`}
              onClick={() =>
                setStatus(status === value ? "All statuses" : String(value))
              }
            >
              <span className={`quick-icon ${color}`}>
                <I size={21} />
              </span>
              <span>
                {String(label)}
                <strong>
                  {daily.filter((a) => a.status === value).length}
                </strong>
              </span>
            </button>
          );
        })}
      </div>
      <section className="card">
        <div className="card-header">
          <h2>
            <span className="live-dot" />
            Patient queue
          </h2>
          <span className="muted small">{filtered.length} appointments</span>
        </div>
        <div className="list-toolbar wrap">
          <SearchBox
            value={search}
            onChange={setSearch}
            placeholder="Search patient, UHID or token…"
          />
          <input
            aria-label="Appointment date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          <select
            aria-label="Filter practitioner"
            value={doctor}
            onChange={(e) => setDoctor(e.target.value)}
          >
            <option>All practitioners</option>
            {data.doctors.map((d) => (
              <option value={d.id} key={d.id}>
                {d.name}
              </option>
            ))}
          </select>
          <select
            aria-label="Filter appointment status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {[
              "All statuses",
              "Booked",
              "Checked in",
              "In consultation",
              "Completed",
              "Cancelled",
            ].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Token</th>
                <th>Patient</th>
                <th>Practitioner</th>
                <th>Time</th>
                <th>Visit type</th>
                <th>Status</th>
                <th>Next action</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => {
                const p = data.patients.find((p) => p.id === a.patientId),
                  d = data.doctors.find((d) => d.id === a.doctorId);
                return (
                  <tr key={a.id}>
                    <td>
                      <span className={`token ${a.token ? "" : "unassigned"}`}>
                        {a.token ? String(a.token).padStart(2, "0") : "—"}
                      </span>
                    </td>
                    <td>
                      <div className="person-cell">
                        <Avatar name={p?.fullName || "Patient"} />
                        <div>
                          <strong>{p?.fullName}</strong>
                          <small>{p?.uhid}</small>
                        </div>
                      </div>
                    </td>
                    <td>
                      <div className="doctor-cell">
                        {d?.name}
                        <small>{d?.department}</small>
                      </div>
                    </td>
                    <td>
                      {time(a.startsAt)}
                      <small className="cell-small">
                        {a.durationMinutes} min
                      </small>
                    </td>
                    <td>{a.mode}</td>
                    <td>
                      <Badge status={a.status} />
                    </td>
                    <td>
                      <div className="row-actions">
                        {canWrite && a.status === "Booked" && (
                          <button
                            className="btn small-btn"
                            disabled={busy}
                            onClick={() => void transition(a, "check-in")}
                          >
                            <Check size={14} />
                            Check in
                          </button>
                        )}
                        {a.status === "Checked in" &&
                          user.permissions.includes("clinical.sign") &&
                          a.doctorId === user.id && (
                            <button
                              className="btn small-btn teal-btn"
                              disabled={busy}
                              onClick={() => void transition(a, "start")}
                            >
                              <Stethoscope size={14} />
                              Consult
                            </button>
                          )}
                        {a.status === "In consultation" && (
                          <button
                            className="btn small-btn"
                            onClick={() => {
                              const e = data.encounters.find(
                                (e) => e.appointmentId === a.id,
                              );
                              if (e) navigate(`/consultations/${e.id}`);
                              else
                                notify(
                                  "The assigned doctor needs to open this consultation.",
                                  "error",
                                );
                            }}
                          >
                            View record
                          </button>
                        )}
                        <button
                          className="icon-btn"
                          aria-label={`Actions for ${p?.fullName}`}
                          onClick={() => {
                            setSelected(a);
                            setAction(null);
                            setError("");
                          }}
                        >
                          <MoreHorizontal size={19} />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <Empty
            title="No appointments in this view"
            description="Adjust the filters or book a new appointment."
          />
        )}
      </section>
      <div className="info-note standalone">
        <Clock3 size={18} />
        <span>
          Tokens are assigned per practitioner and service date at check-in.
          Queue changes are recorded in the audit trail.
        </span>
      </div>
      {book && <BookingModal onClose={() => setBook(false)} />}
      {selected && (
        <Modal
          title={
            action === "cancel"
              ? "Cancel appointment"
              : action === "reschedule"
                ? "Reschedule appointment"
                : "Appointment details"
          }
          subtitle={`${data.patients.find((p) => p.id === selected.patientId)?.fullName} · ${time(selected.startsAt)}`}
          onClose={() => setSelected(null)}
        >
          <div className="modal-body">
            {!action ? (
              <>
                <div className="appointment-summary">
                  <Badge status={selected.status} />
                  <p>{selected.reason || "General consultation"}</p>
                  <span>
                    {selected.mode} · {selected.durationMinutes} minutes
                  </span>
                </div>
                <div className="stack-actions">
                  <button
                    className="btn"
                    onClick={() => navigate(`/patients/${selected.patientId}`)}
                  >
                    View patient record
                  </button>
                  {canWrite && selected.status === "Booked" && (
                    <>
                      <button
                        className="btn"
                        onClick={() => {
                          setAction("reschedule");
                          setNewDate(
                            localInputDate(new Date(selected.startsAt)),
                          );
                        }}
                      >
                        <RefreshCw size={16} />
                        Reschedule appointment
                      </button>
                      <button
                        className="btn danger-outline"
                        onClick={() => setAction("cancel")}
                      >
                        <X size={16} />
                        Cancel appointment
                      </button>
                    </>
                  )}
                  {canWrite && selected.status === "In consultation" && (
                    <button
                      className="btn"
                      disabled={busy}
                      onClick={() => void transition(selected, "complete")}
                    >
                      Mark visit complete
                    </button>
                  )}
                </div>
              </>
            ) : (
              <form
                onSubmit={async (e) => {
                  e.preventDefault();
                  setBusy(true);
                  setError("");
                  try {
                    if (action === "cancel")
                      await api(`/appointments/${selected.id}/cancel`, "POST", {
                        version: selected.version,
                        reason,
                      });
                    else
                      await api(
                        `/appointments/${selected.id}/reschedule`,
                        "POST",
                        {
                          version: selected.version,
                          reason,
                          startsAt: new Date(newDate).toISOString(),
                        },
                      );
                    await refresh();
                    notify(
                      action === "cancel"
                        ? "Appointment cancelled"
                        : "Appointment rescheduled",
                    );
                    setSelected(null);
                  } catch (err) {
                    setError((err as Error).message);
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {action === "reschedule" && (
                  <Field label="New date and time" required>
                    <input
                      type="datetime-local"
                      required
                      value={newDate}
                      onChange={(e) => setNewDate(e.target.value)}
                    />
                  </Field>
                )}
                <Field label="Reason" required>
                  <textarea
                    required
                    minLength={5}
                    rows={3}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Record why this appointment is changing"
                  />
                </Field>
                {error && <ErrorNote>{error}</ErrorNote>}
                <div className="modal-footer inline">
                  <button
                    type="button"
                    className="btn"
                    onClick={() => setAction(null)}
                  >
                    Back
                  </button>
                  <Submit busy={busy}>
                    Confirm {action === "cancel" ? "cancellation" : "new time"}
                  </Submit>
                </div>
              </form>
            )}
          </div>
        </Modal>
      )}
    </>
  );
}
