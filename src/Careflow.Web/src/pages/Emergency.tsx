import { useState } from "react";
import { Siren } from "lucide-react";
import { useApp } from "../store";
import { api, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { EmergencyRegistration } from "../types";
const triageTone = (category: string) =>
  category === "Red"
    ? "red"
    : category === "Yellow"
      ? "amber"
      : category === "Green"
        ? "green"
        : "gray";
export function Emergency() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [register, setRegister] = useState(false);
  const [triage, setTriage] = useState<EmergencyRegistration | null>(null);
  const [observe, setObserve] = useState<EmergencyRegistration | null>(null);
  const [dispose, setDispose] = useState<EmergencyRegistration | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string | null) =>
    id ? data.patients.find((p) => p.id === id) : undefined;
  const triageFor = (registrationId: string) =>
    care.triageAssessments
      .filter((t) => t.registrationId === registrationId)
      .sort((a, b) => b.assessedAt.localeCompare(a.assessedAt))[0];
  const filtered = care.emergencyRegistrations.filter(
    (r) =>
      `${r.number} ${r.patientName}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All" || r.status === status),
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
        eyebrow="EMERGENCY DEPARTMENT"
        title="Emergency board"
        description="Walk-in registration, triage categories, observations and dispositions with identity matching for linked patients."
      >
        {can("emergency.write") && (
          <button className="btn primary" onClick={() => setRegister(true)}>
            <Siren size={16} /> Register arrival
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          {["All", "Open", "Dispositioned"].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </div>
      {filtered.length === 0 ? (
        <Empty
          title="No emergency episodes"
          description="Register an arrival to open an episode."
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Number</th>
                <th>Patient</th>
                <th>Arrival</th>
                <th>Triage</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((r) => {
                const t = triageFor(r.id);
                return (
                  <tr key={r.id}>
                    <td>
                      <strong>{r.number}</strong>
                      <div className="muted">{time(r.arrivedAt)}</div>
                    </td>
                    <td>
                      {r.patientName}
                      <div className="muted">
                        {r.sex} · {patient(r.patientId)?.uhid ?? "unregistered"}
                      </div>
                    </td>
                    <td>
                      {r.arrivalMode}
                      <div className="muted">{time(r.arrivedAt)}</div>
                    </td>
                    <td>
                      {t ? (
                        <Badge
                          tone={
                            triageTone(t.category) as
                              "red" | "amber" | "green" | "gray"
                          }
                        >
                          {t.category}
                        </Badge>
                      ) : (
                        <span className="muted">awaiting triage</span>
                      )}
                    </td>
                    <td>
                      <Badge tone={r.status === "Open" ? "blue" : "gray"}>
                        {r.status}
                      </Badge>
                    </td>
                    <td className="row-actions">
                      {r.status === "Open" && can("emergency.triage") && (
                        <button
                          className="btn small"
                          onClick={() => setTriage(r)}
                        >
                          Triage
                        </button>
                      )}
                      {r.status === "Open" && can("emergency.write") && (
                        <button
                          className="btn small"
                          onClick={() => setObserve(r)}
                        >
                          Observe
                        </button>
                      )}
                      {r.status === "Open" && can("emergency.dispose") && (
                        <button
                          className="btn small"
                          onClick={() => setDispose(r)}
                        >
                          Disposition
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
        open={register}
        onClose={() => setRegister(false)}
        title="Register emergency arrival"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/emergency/registrations",
              {
                patientId: f.get("patientId") || null,
                patientName: f.get("patientName"),
                sex: f.get("sex"),
                dateOfBirth: f.get("dateOfBirth") || null,
                arrivalMode: f.get("arrivalMode"),
                referringFacility: f.get("referringFacility") || "",
                broughtBy: f.get("broughtBy") || "",
                medicoLegalCase: f.get("medicoLegalCase") === "on",
              },
              "Episode registered",
            );
            setRegister(false);
          }}
        >
          <Field label="Linked patient (optional)">
            <select name="patientId" defaultValue="">
              <option value="">Unregistered — unknown patient</option>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Patient name (must match linked identity)">
            <input name="patientName" required minLength={2} />
          </Field>
          <div className="grid-2">
            <Field label="Sex">
              <select name="sex">
                {["Unknown", "Male", "Female", "Other"].map((s) => (
                  <option key={s}>{s}</option>
                ))}
              </select>
            </Field>
            <Field label="Date of birth (optional)">
              <input type="date" name="dateOfBirth" />
            </Field>
            <Field label="Arrival mode">
              <select name="arrivalMode">
                {["Walk-in", "Ambulance", "Police", "Referral", "Other"].map(
                  (m) => (
                    <option key={m}>{m}</option>
                  ),
                )}
              </select>
            </Field>
          </div>
          <div className="grid-2">
            <Field label="Referring facility">
              <input name="referringFacility" maxLength={200} placeholder="If referred or transferred" />
            </Field>
            <Field label="Brought by">
              <input name="broughtBy" maxLength={120} placeholder="Family / police / ambulance crew" />
            </Field>
          </div>
          <label className="field">
            <span>
              <input type="checkbox" name="medicoLegalCase" /> Medico-legal
              case (MLC)
            </span>
          </label>
          <Submit label="Register episode" />
        </form>
      </Modal>
      <Modal
        open={!!triage}
        onClose={() => setTriage(null)}
        title="Triage assessment"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/emergency/registrations/${triage!.id}/triage`,
              {
                category: f.get("category"),
                complaint: f.get("complaint"),
                vitals: {
                  temperatureC: num(f.get("temperatureC")),
                  pulse: num(f.get("pulse")),
                  systolic: num(f.get("systolic")),
                  diastolic: num(f.get("diastolic")),
                  weightKg: null,
                  spo2: num(f.get("spo2")),
                },
              },
              "Triage recorded",
            );
            setTriage(null);
          }}
        >
          <Field label="Triage category">
            <select name="category">
              {["Red", "Yellow", "Green", "Black"].map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </Field>
          <Field label="Presenting complaint">
            <textarea name="complaint" required minLength={3} rows={3} />
          </Field>
          <div className="grid-2">
            <Field label="Temperature °C">
              <input name="temperatureC" type="number" step="0.1" />
            </Field>
            <Field label="Pulse">
              <input name="pulse" type="number" />
            </Field>
            <Field label="Systolic">
              <input name="systolic" type="number" />
            </Field>
            <Field label="Diastolic">
              <input name="diastolic" type="number" />
            </Field>
            <Field label="SpO₂ %">
              <input name="spo2" type="number" />
            </Field>
          </div>
          <Submit label="Record triage" />
        </form>
      </Modal>
      <Modal
        open={!!observe}
        onClose={() => setObserve(null)}
        title="Record observation"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/emergency/registrations/${observe!.id}/observations`,
              {
                note: f.get("note"),
                vitals: {
                  temperatureC: num(f.get("temperatureC")),
                  pulse: num(f.get("pulse")),
                  systolic: num(f.get("systolic")),
                  diastolic: num(f.get("diastolic")),
                  weightKg: null,
                  spo2: num(f.get("spo2")),
                },
              },
              "Observation recorded",
            );
            setObserve(null);
          }}
        >
          <Field label="Observation note">
            <textarea name="note" required minLength={3} rows={3} />
          </Field>
          <div className="grid-2">
            <Field label="Temperature °C">
              <input name="temperatureC" type="number" step="0.1" />
            </Field>
            <Field label="Pulse">
              <input name="pulse" type="number" />
            </Field>
            <Field label="Systolic">
              <input name="systolic" type="number" />
            </Field>
            <Field label="Diastolic">
              <input name="diastolic" type="number" />
            </Field>
            <Field label="SpO₂ %">
              <input name="spo2" type="number" />
            </Field>
          </div>
          <Submit label="Record observation" />
        </form>
      </Modal>
      <Modal
        open={!!dispose}
        onClose={() => setDispose(null)}
        title="Disposition"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/emergency/registrations/${dispose!.id}/disposition`,
              {
                kind: f.get("kind"),
                summary: f.get("summary"),
                bedId: f.get("bedId") || null,
                doctorId: f.get("doctorId") || null,
                receivingFacility: f.get("receivingFacility") || null,
              },
              "Disposition recorded",
            );
            setDispose(null);
          }}
        >
          <Field label="Disposition">
            <select name="kind">
              {["Admit", "Discharge", "Refer", "LAMA"].map((k) => (
                <option key={k}>{k}</option>
              ))}
            </select>
          </Field>
          <Field label="Summary">
            <textarea name="summary" required minLength={10} rows={3} />
          </Field>
          <p className="muted">
            Admission requires a linked registered patient, an available bed and
            a responsible practitioner. Referral requires a receiving facility.
          </p>
          <div className="grid-2">
            <Field label="Bed (for admission)">
              <select name="bedId" defaultValue="">
                <option value="">—</option>
                {care.beds
                  .filter((b) => b.status === "Available")
                  .map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.ward} · {b.number}
                    </option>
                  ))}
              </select>
            </Field>
            <Field label="Responsible practitioner">
              <select name="doctorId" defaultValue="">
                <option value="">—</option>
                {data.doctors.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Receiving facility (for referral)">
              <input name="receivingFacility" placeholder="—" />
            </Field>
          </div>
          <Submit label="Record disposition" />
        </form>
      </Modal>
    </>
  );
}
function num(value: FormDataEntryValue | null): number | null {
  const n = Number(value);
  return value === null || value === "" || Number.isNaN(n) ? null : n;
}
