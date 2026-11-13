import { useState } from "react";
import { Scissors } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { OtBooking } from "../types";
export function OperationTheatre() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [request, setRequest] = useState(false);
  const [schedule, setSchedule] = useState<OtBooking | null>(null);
  const [checklist, setChecklist] = useState<OtBooking | null>(null);
  const [complete, setComplete] = useState<OtBooking | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const surgeon = (id: string) => data.doctors.find((d) => d.id === id);
  const filtered = care.otBookings.filter(
    (b) =>
      `${b.number} ${b.procedure} ${b.theatre} ${patient(b.patientId)?.fullName ?? ""}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All" || b.status === status),
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
        eyebrow="OPERATION THEATRE"
        title="Theatre schedule"
        description="Surgical bookings with conflict-free theatre slots, the five-point safety checklist and recorded procedure times."
      >
        {can("ot.request") && (
          <button className="btn primary" onClick={() => setRequest(true)}>
            <Scissors size={16} /> Request surgery
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          {[
            "All",
            "Requested",
            "Scheduled",
            "In Theatre",
            "Recovery",
            "Completed",
            "Cancelled",
          ].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </div>
      {filtered.length === 0 ? (
        <Empty
          title="No theatre bookings"
          description="Request a surgical booking to schedule a theatre."
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Number</th>
                <th>Patient / procedure</th>
                <th>Surgeon</th>
                <th>Slot</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((b) => (
                <tr key={b.id}>
                  <td>
                    <strong>{b.number}</strong>
                    <div className="muted">
                      {b.anaesthesia} · {b.priority}
                    </div>
                  </td>
                  <td>
                    {patient(b.patientId)?.fullName ?? "—"}
                    <div className="muted">
                      {b.procedure} · {b.laterality} {b.site}
                    </div>
                  </td>
                  <td>{surgeon(b.surgeonId)?.name ?? "—"}</td>
                  <td>
                    {b.scheduledStart ? (
                      <>
                        {shortDate(b.scheduledStart)} {time(b.scheduledStart)}
                        <div className="muted">
                          {b.theatre}
                          {b.timeIn && ` · in ${time(b.timeIn)}`}
                          {b.timeOut && ` · out ${time(b.timeOut)}`}
                        </div>
                      </>
                    ) : (
                      <span className="muted">unscheduled</span>
                    )}
                  </td>
                  <td>
                    <Badge
                      tone={
                        b.status === "Completed"
                          ? "green"
                          : b.status === "Cancelled"
                            ? "gray"
                            : b.status === "In Theatre"
                              ? "amber"
                              : "blue"
                      }
                    >
                      {b.status}
                    </Badge>
                  </td>
                  <td className="row-actions">
                    {b.status === "Requested" && can("ot.schedule") && (
                      <button
                        className="btn small"
                        onClick={() => setSchedule(b)}
                      >
                        Schedule
                      </button>
                    )}
                    {b.status === "Scheduled" && can("ot.record") && (
                      <button
                        className="btn small"
                        onClick={() => setChecklist(b)}
                      >
                        Checklist
                      </button>
                    )}
                    {b.status === "Scheduled" && can("ot.record") && (
                      <button
                        className="btn small"
                        onClick={() =>
                          act(
                            `/ot/bookings/${b.id}/time-in`,
                            { reason: "", version: b.version },
                            "Time in recorded",
                          )
                        }
                      >
                        Time in
                      </button>
                    )}
                    {b.status === "In Theatre" && can("ot.record") && (
                      <button
                        className="btn small"
                        onClick={() =>
                          act(
                            `/ot/bookings/${b.id}/time-out`,
                            { reason: "", version: b.version },
                            "Time out recorded",
                          )
                        }
                      >
                        Time out
                      </button>
                    )}
                    {b.status === "Recovery" && can("ot.record") && (
                      <button
                        className="btn small"
                        onClick={() => setComplete(b)}
                      >
                        Complete
                      </button>
                    )}
                    {(b.status === "Requested" || b.status === "Scheduled") &&
                      can("ot.schedule") && (
                        <button
                          className="btn small ghost"
                          onClick={() =>
                            act(
                              `/ot/bookings/${b.id}/cancel`,
                              {
                                reason: "Cancelled from theatre board",
                                version: b.version,
                              },
                              "Booking cancelled",
                            )
                          }
                        >
                          Cancel
                        </button>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Modal
        open={request}
        onClose={() => setRequest(false)}
        title="Request surgical booking"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/ot/bookings",
              {
                patientId: f.get("patientId"),
                admissionId: f.get("admissionId") || null,
                procedure: f.get("procedure"),
                laterality: f.get("laterality"),
                site: f.get("site"),
                surgeonId: f.get("surgeonId"),
                anaesthesia: f.get("anaesthesia"),
                priority: f.get("priority"),
              },
              "Surgery requested",
            );
            setRequest(false);
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
          <Field label="Active admission (optional)">
            <select name="admissionId" defaultValue="">
              <option value="">—</option>
              {care.admissions
                .filter((a) => a.status === "Active")
                .map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.number}
                  </option>
                ))}
            </select>
          </Field>
          <div className="grid-2">
            <Field label="Procedure">
              <input name="procedure" required minLength={3} />
            </Field>
            <Field label="Site">
              <input name="site" required minLength={2} />
            </Field>
            <Field label="Laterality">
              <select name="laterality">
                {["Not applicable", "Left", "Right", "Bilateral"].map((l) => (
                  <option key={l}>{l}</option>
                ))}
              </select>
            </Field>
            <Field label="Surgeon">
              <select name="surgeonId" required>
                {data.doctors.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Anaesthesia">
              <select name="anaesthesia">
                {["General", "Regional", "Local", "Sedation"].map((a) => (
                  <option key={a}>{a}</option>
                ))}
              </select>
            </Field>
            <Field label="Priority">
              <select name="priority">
                {["Elective", "Urgent", "Emergency"].map((p) => (
                  <option key={p}>{p}</option>
                ))}
              </select>
            </Field>
          </div>
          <Submit label="Request booking" />
        </form>
      </Modal>
      <Modal
        open={!!schedule}
        onClose={() => setSchedule(null)}
        title="Schedule theatre slot"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/ot/bookings/${schedule!.id}/schedule`,
              {
                theatre: f.get("theatre"),
                start: new Date(String(f.get("start"))).toISOString(),
                durationMinutes: Number(f.get("durationMinutes")),
                version: schedule!.version,
              },
              "Theatre scheduled",
            );
            setSchedule(null);
          }}
        >
          <Field label="Theatre">
            <input name="theatre" required minLength={2} placeholder="OT-3" />
          </Field>
          <div className="grid-2">
            <Field label="Start">
              <input type="datetime-local" name="start" required />
            </Field>
            <Field label="Duration (minutes)">
              <input
                type="number"
                name="durationMinutes"
                defaultValue={60}
                min={15}
                max={480}
                required
              />
            </Field>
          </div>
          <Submit label="Schedule" />
        </form>
      </Modal>
      <Modal
        open={!!checklist}
        onClose={() => setChecklist(null)}
        title="Surgical safety checklist"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/ot/bookings/${checklist!.id}/checklist`,
              {
                consent: f.get("consent") === "on",
                anaesthesiaAssessment: f.get("anaesthesiaAssessment") === "on",
                fastingConfirmed: f.get("fastingConfirmed") === "on",
                siteMarked: f.get("siteMarked") === "on",
                safetyChecklist: f.get("safetyChecklist") === "on",
                notes: f.get("notes") ?? "",
              },
              "Checklist recorded",
            );
            setChecklist(null);
          }}
        >
          <p className="muted">
            All five items must be confirmed before the patient enters the
            theatre. One checklist per booking.
          </p>
          {[
            ["consent", "Informed consent verified"],
            ["anaesthesiaAssessment", "Anaesthesia assessment completed"],
            ["fastingConfirmed", "Fasting status confirmed"],
            ["siteMarked", "Surgical site marked"],
            ["safetyChecklist", "Team safety checklist completed"],
          ].map(([name, label]) => (
            <label className="check-row" key={name}>
              <input type="checkbox" name={name} required /> {label}
            </label>
          ))}
          <Field label="Notes">
            <input name="notes" maxLength={1000} />
          </Field>
          <Submit label="Record checklist" />
        </form>
      </Modal>
      <Modal
        open={!!complete}
        onClose={() => setComplete(null)}
        title="Complete operation"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/ot/bookings/${complete!.id}/complete`,
              {
                operationNote: f.get("operationNote"),
                version: complete!.version,
              },
              "Operation completed",
            );
            setComplete(null);
          }}
        >
          <Field label="Operation note">
            <textarea name="operationNote" required minLength={5} rows={4} />
          </Field>
          <Submit label="Complete operation" />
        </form>
      </Modal>
    </>
  );
}
