import { useState } from "react";
import { Video, ShieldCheck, Upload } from "lucide-react";
import { useApp } from "../store";
import { api, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { TeleSession } from "../types";
export function Telemedicine() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [upload, setUpload] = useState<TeleSession | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const doctor = (id: string) => data.doctors.find((d) => d.id === id);
  const filtered = care.teleSessions.filter((s) =>
    `${s.number} ${patient(s.patientId)?.fullName ?? ""}`
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
        eyebrow="TELEMEDICINE"
        title="Consent-gated video consultations"
        description="Scheduled tele-consultations with recorded patient consent, a waiting room and an ephemeral join token. The token authorises this consultation only — no external video provider is configured."
      />
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
      </div>
      <div className="panel">
        <table>
          <thead>
            <tr>
              <th>Session</th>
              <th>Patient</th>
              <th>Doctor</th>
              <th>Scheduled</th>
              <th>Consent</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((s) => (
              <tr key={s.id}>
                <td>
                  <strong>{s.number}</strong>
                </td>
                <td>{patient(s.patientId)?.fullName}</td>
                <td>{doctor(s.doctorId)?.name}</td>
                <td>{time(s.scheduledAt)}</td>
                <td>
                  {s.consentObtained ? (
                    <Badge>Recorded</Badge>
                  ) : (
                    <Badge tone="warning">Pending</Badge>
                  )}
                </td>
                <td>
                  <Badge>{s.status}</Badge>
                </td>
                <td>
                  {can("tele.consent") && !s.consentObtained && (
                    <button
                      className="btn ghost"
                      onClick={() =>
                        void act(
                          `/telemedicine/sessions/${s.id}/consent`,
                          {
                            kind: "PatientConsent",
                            given: true,
                            note: "Consent for tele-consultation recorded.",
                          },
                          "Consent recorded",
                        )
                      }
                    >
                      <ShieldCheck size={14} /> Consent
                    </button>
                  )}
                  {can("tele.consent") && s.consentObtained && s.status === "Scheduled" && (
                    <button
                      className="btn ghost"
                      onClick={() =>
                        void act(
                          `/telemedicine/sessions/${s.id}/join-waiting`,
                          null,
                          "Patient in the waiting room",
                        )
                      }
                    >
                      Join waiting
                    </button>
                  )}
                  {can("tele.join") && s.status === "Waiting" && (
                    <>
                      <button
                        className="btn ghost"
                        onClick={() =>
                          void act(
                            `/telemedicine/sessions/${s.id}/join-token`,
                            { version: s.version, reason: "" },
                            "Ephemeral join token issued",
                          )
                        }
                      >
                        <Video size={14} /> Join token
                      </button>
                      <button
                        className="btn ghost"
                        onClick={() =>
                          void act(
                            `/telemedicine/sessions/${s.id}/admit`,
                            { version: s.version, reason: "" },
                            "Patient admitted to consultation",
                          )
                        }
                      >
                        Admit
                      </button>
                    </>
                  )}
                  {can("tele.join") && s.status === "InConsultation" && (
                    <>
                      <button
                        className="btn ghost"
                        onClick={() =>
                          void act(
                            `/telemedicine/sessions/${s.id}/end`,
                            {
                              note: "Reviewed symptoms, vitals and home readings.",
                              emergencyAdvice:
                                "Escalate to the nearest facility for chest pain or breathlessness.",
                              version: s.version,
                            },
                            "Consultation closed",
                          )
                        }
                      >
                        End
                      </button>
                      <button
                        className="btn ghost"
                        onClick={() => setUpload(s)}
                      >
                        <Upload size={14} /> Document
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && <Empty title="No tele-sessions booked" />}
      </div>
      {upload && (
        <Modal
          title={`Upload document · ${upload.number}`}
          onClose={() => setUpload(null)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(
                  `/telemedicine/sessions/${upload.id}/uploads`,
                  "POST",
                  {
                    fileName: f.get("fileName"),
                    contentType: f.get("contentType"),
                    sizeBytes: Number(f.get("sizeBytes")),
                  },
                );
                await refresh();
                notify("Document uploaded");
                setUpload(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <p className="muted">
              Allowed: pdf, png, jpg, jpeg, docx · up to 10 MB.
            </p>
            <Field label="File name">
              <input name="fileName" placeholder="discharge-summary.pdf" required />
            </Field>
            <Field label="Content type">
              <select name="contentType">
                <option>application/pdf</option>
                <option>image/png</option>
                <option>image/jpeg</option>
                <option>application/vnd.openxmlformats-officedocument.wordprocessingml.document</option>
              </select>
            </Field>
            <Field label="Size (bytes)">
              <input type="number" name="sizeBytes" required />
            </Field>
            <Submit label="Upload" />
          </form>
        </Modal>
      )}
    </>
  );
}
