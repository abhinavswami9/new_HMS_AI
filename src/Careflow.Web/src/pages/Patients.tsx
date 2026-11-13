import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  CalendarDays,
  Download,
  FileText,
  Heart,
  Phone,
  Plus,
  Search,
  ShieldCheck,
  UserRound,
  Users,
  Mail,
  MapPin,
  Stethoscope,
} from "lucide-react";
import { useApp } from "../store";
import {
  Avatar,
  Badge,
  Empty,
  PageHeader,
  Pagination,
  SearchBox,
} from "../components";
import { BookingModal, InvoiceModal, RegistrationModal } from "../forms";
import { age, api, downloadCsv, money, shortDate, time } from "../api";
import type { Chart } from "../types";
export function Patients() {
  const { data, user } = useApp();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [sex, setSex] = useState("All patients");
  const [page, setPage] = useState(1);
  const [register, setRegister] = useState(false);
  const filtered = data.patients.filter(
    (p) =>
      `${p.fullName} ${p.uhid} ${p.phone}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (sex === "All patients" || p.sex === sex),
  );
  const size = 8;
  const current = Math.min(
    page,
    Math.max(1, Math.ceil(filtered.length / size)),
  );
  return (
    <>
      <PageHeader
        eyebrow="PATIENT SERVICES"
        title="Patient registry"
        description="Every patient. One unique identity. A complete view of care."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-patients.csv", [
              ["UHID", "Name", "Date of birth", "Sex", "Phone"],
              ...filtered.map((p) => [
                p.uhid,
                p.fullName,
                p.dateOfBirth,
                p.sex,
                p.phone,
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {user.permissions.includes("patient.write") && (
          <button className="btn primary" onClick={() => setRegister(true)}>
            <Plus size={17} />
            Register patient
          </button>
        )}
      </PageHeader>
      <div className="summary-strip">
        <div>
          <span className="quick-icon teal">
            <Users size={20} />
          </span>
          <span>
            Recent registered patients<strong>{data.patients.length}</strong>
          </span>
        </div>
        <div>
          <span className="quick-icon blue">
            <UserRound size={20} />
          </span>
          <span>
            Registered today
            <strong>
              {
                data.patients.filter(
                  (p) =>
                    new Date(p.createdAt).toDateString() ===
                    new Date().toDateString(),
                ).length
              }
            </strong>
          </span>
        </div>
        <p>
          <ShieldCheck size={18} />
          Hospital-scoped UHID · audited record access
        </p>
      </div>
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={(s) => {
              setSearch(s);
              setPage(1);
            }}
            placeholder="Search by name, UHID or phone number…"
          />
          <select
            aria-label="Filter by gender"
            value={sex}
            onChange={(e) => {
              setSex(e.target.value);
              setPage(1);
            }}
          >
            {["All patients", "Female", "Male", "Other", "Undisclosed"].map(
              (s) => (
                <option key={s}>{s}</option>
              ),
            )}
          </select>
          <span className="muted small">{filtered.length} patients</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Patient name</th>
                <th>UHID</th>
                <th>Age / gender</th>
                <th>Contact number</th>
                <th>Registered</th>
                <th>Blood group</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.slice((current - 1) * size, current * size).map((p) => (
                <tr
                  key={p.id}
                  tabIndex={0}
                  onClick={() => navigate(`/patients/${p.id}`)}
                  onKeyDown={(e) =>
                    e.key === "Enter" && navigate(`/patients/${p.id}`)
                  }
                >
                  <td>
                    <div className="person-cell">
                      <Avatar name={p.fullName} />
                      <div>
                        <strong>{p.fullName}</strong>
                        <small>{p.email || "No email provided"}</small>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className="uhid">{p.uhid}</span>
                  </td>
                  <td>
                    {age(p.dateOfBirth)} yrs{" "}
                    <span className="muted">/ {p.sex}</span>
                  </td>
                  <td>{p.phone || "—"}</td>
                  <td>{shortDate(p.createdAt)}</td>
                  <td>
                    <span className="blood-tag">{p.bloodGroup}</span>
                  </td>
                  <td>
                    <ChevronIcon />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <Empty
            title={search ? "No matching patients" : "No patients yet"}
            description={
              search
                ? "Try a different name, UHID or phone number."
                : "Register a patient to create their hospital identity."
            }
          >
            {search && (
              <button className="btn" onClick={() => setSearch("")}>
                <Search size={15} />
                Clear search
              </button>
            )}
          </Empty>
        )}
        <Pagination
          page={current}
          total={filtered.length}
          pageSize={size}
          onChange={setPage}
        />
      </section>
      <div className="info-note standalone">
        <ShieldCheck size={18} />
        <span>
          Showing the most recent 200 registry records. Probable duplicates
          require review before a new patient is registered.
        </span>
      </div>
      {register && <RegistrationModal onClose={() => setRegister(false)} />}
    </>
  );
}
function ChevronIcon() {
  return <ArrowRight size={16} className="muted" />;
}
export function PatientDetail() {
  const { id } = useParams();
  const { data, user } = useApp();
  const [chart, setChart] = useState<Chart | null>(null);
  const [error, setError] = useState("");
  const [tab, setTab] = useState("Overview");
  const [modal, setModal] = useState<"book" | "bill" | null>(null);
  useEffect(() => {
    if (id)
      api<Chart>(`/patients/${id}/chart`)
        .then(setChart)
        .catch((e) => setError(e.message));
  }, [id, data]);
  if (error)
    return (
      <Empty title="Patient record unavailable" description={error}>
        <Link className="btn" to="/patients">
          Back to registry
        </Link>
      </Empty>
    );
  if (!chart)
    return <div className="loading-panel">Loading patient record…</div>;
  const p = chart.patient;
  const latest = chart.encounters[0];
  const signed = chart.encounters.filter((e) => e.status === "Signed");
  return (
    <>
      <Link to="/patients" className="back-link">
        <ArrowLeft size={16} />
        Patient registry
      </Link>
      <div className="patient-banner">
        <Avatar name={p.fullName} size="large" />
        <div className="patient-identity">
          <div>
            <h1>{p.fullName}</h1>
            <span className="badge green">
              <span />
              Active patient
            </span>
          </div>
          <p>
            <span className="uhid">{p.uhid}</span>
            <span>
              {age(p.dateOfBirth)} years · {p.sex}
            </span>
            <span className="blood-tag">{p.bloodGroup}</span>
          </p>
        </div>
        <div className="heading-actions">
          {user.permissions.includes("schedule.write") && (
            <button className="btn" onClick={() => setModal("book")}>
              <CalendarDays size={16} />
              Book appointment
            </button>
          )}
          {user.permissions.includes("billing.write") && (
            <button className="btn primary" onClick={() => setModal("bill")}>
              <Plus size={16} />
              Create invoice
            </button>
          )}
        </div>
      </div>
      <div className="patient-facts">
        <span>
          <Phone size={16} />
          {p.phone || "No phone recorded"}
        </span>
        <span>
          <Mail size={16} />
          {p.email || "No email recorded"}
        </span>
        <span>
          <MapPin size={16} />
          {p.address || "No address recorded"}
        </span>
      </div>
      {chart.historyTruncated && (
        <div className="info-note standalone">
          Showing the latest 200 records per category. Older history requires
          the paginated archive workflow; do not treat this view as the complete
          chart.
        </div>
      )}
      <div className="tabs">
        {[
          "Overview",
          "Medical record",
          "Care history",
          "Appointments",
          "Billing",
        ].map((t) => (
          <button
            key={t}
            className={tab === t ? "active" : ""}
            onClick={() => setTab(t)}
          >
            {t}
            {t === "Medical record" && <span>{signed.length}</span>}
          </button>
        ))}
      </div>
      {tab === "Care history" && (
        <div className="care-main">
          <section className="card">
            <div className="card-header">
              <h2>Admissions & discharge</h2>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Admission</th>
                    <th>Bed</th>
                    <th>Status</th>
                    <th>Admitted</th>
                    <th>Discharge summary</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.care.admissions.map((a) => (
                    <tr key={a.id}>
                      <td>
                        {a.number}
                        <small className="cell-small">{a.reason}</small>
                      </td>
                      <td>
                        {data.care.beds.find((b) => b.id === a.bedId)?.number ||
                          a.bedId}
                      </td>
                      <td>
                        <Badge status={a.status} />
                      </td>
                      <td>{shortDate(a.admittedAt)}</td>
                      <td className="note-cell">{a.dischargeSummary || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!chart.care.admissions.length && (
              <Empty
                title="No inpatient history"
                description="Admissions and discharge summaries will appear here."
              />
            )}
          </section>
          <section className="card">
            <div className="card-header">
              <h2>Nursing notes</h2>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Recorded</th>
                    <th>Type</th>
                    <th>Note</th>
                    <th>Pain / fall</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.care.nursingNotes.map((n) => (
                    <tr key={n.id}>
                      <td>
                        {shortDate(n.createdAt)} {time(n.createdAt)}
                      </td>
                      <td>
                        <Badge status={n.kind} />
                      </td>
                      <td className="note-cell">{n.text}</td>
                      <td>
                        {n.painScore ?? "—"} / {n.fallRisk}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          <section className="card">
            <div className="card-header">
              <h2>Medication orders & dispensing</h2>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Medicine</th>
                    <th>Dose</th>
                    <th>Frequency</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.care.medicationOrders.map((o) => (
                    <tr key={o.id}>
                      <td>{o.name}</td>
                      <td>
                        {o.dose} · {o.route}
                      </td>
                      <td>{o.frequency}</td>
                      <td>
                        <Badge status={o.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Dispense</th>
                    <th>Setting</th>
                    <th>Lines</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.care.dispenses.map((d) => (
                    <tr key={d.id}>
                      <td>{d.number}</td>
                      <td>
                        <Badge status={d.kind} />
                      </td>
                      <td>
                        {d.lines
                          .map((l) => `${l.name} × ${l.quantity}`)
                          .join(", ")}
                      </td>
                      <td>{shortDate(d.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          <section className="card">
            <div className="card-header">
              <h2>Laboratory orders & results</h2>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Order</th>
                    <th>Test</th>
                    <th>Status</th>
                    <th>Result</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.care.labOrders.map((o) => {
                    const r = chart.care.labResults
                      .filter((x) => x.orderId === o.id)
                      .sort((a, b) => b.version - a.version)[0];
                    return (
                      <tr key={o.id}>
                        <td>
                          {o.number}
                          <small className="cell-small">
                            {o.accession || ""}
                          </small>
                        </td>
                        <td>
                          {data.care.labTests.find((t) => t.id === o.testId)
                            ?.name || o.testId}
                        </td>
                        <td>
                          <Badge status={o.status} />
                        </td>
                        <td>{r ? `${r.value} ${r.unit} (${r.flag})` : "—"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      )}
      {tab === "Overview" && (
        <div className="detail-grid">
          <div>
            <section className="card">
              <div className="card-header">
                <h2>Patient information</h2>
                <UserRound size={18} className="muted" />
              </div>
              <div className="info-grid">
                <div>
                  <span>Date of birth</span>
                  <strong>{shortDate(p.dateOfBirth)}</strong>
                </div>
                <div>
                  <span>Registered on</span>
                  <strong>{shortDate(p.createdAt)}</strong>
                </div>
                <div>
                  <span>Emergency contact</span>
                  <strong>{p.emergencyContact || "Not provided"}</strong>
                </div>
                <div>
                  <span>ABHA linkage</span>
                  <strong>Not linked</strong>
                </div>
              </div>
            </section>
            <section className="card mt-20">
              <div className="card-header">
                <h2>Care timeline</h2>
                <span className="count-badge">
                  {chart.appointments.length + signed.length}
                </span>
              </div>
              <div className="timeline">
                {signed.slice(0, 4).map((e) => (
                  <Link to={`/consultations/${e.id}`} key={e.id}>
                    <span className="timeline-icon">
                      <FileText size={17} />
                    </span>
                    <div>
                      <strong>Consultation completed</strong>
                      <p>{e.diagnosis}</p>
                      <small>
                        {shortDate(e.updatedAt)} ·{" "}
                        {data.doctors.find((d) => d.id === e.doctorId)?.name}
                      </small>
                    </div>
                    <ArrowRight size={16} />
                  </Link>
                ))}
                {chart.appointments.slice(0, 4).map((a) => (
                  <div key={a.id}>
                    <span className="timeline-icon blue">
                      <CalendarDays size={17} />
                    </span>
                    <div>
                      <strong>Appointment {a.status.toLowerCase()}</strong>
                      <p>
                        {a.reason || "General consultation"} ·{" "}
                        {data.doctors.find((d) => d.id === a.doctorId)?.name}
                      </p>
                      <small>
                        {shortDate(a.startsAt)} at {time(a.startsAt)}
                      </small>
                    </div>
                    <Badge status={a.status} />
                  </div>
                ))}
                {!signed.length && !chart.appointments.length && (
                  <Empty
                    title="The care journey starts here"
                    description="Appointments and clinical records will appear in this timeline."
                  />
                )}
              </div>
            </section>
          </div>
          <div>
            <section className="card">
              <div className="card-header">
                <h2>
                  <Heart size={18} />
                  Allergies & alerts
                </h2>
              </div>
              <div className="card-body">
                {latest?.allergies.length ? (
                  latest.allergies.map((a, i) => (
                    <div className="allergy-item" key={i}>
                      <strong>{a.substance}</strong>
                      <span>{a.reaction}</span>
                      <Badge status={a.severity} />
                    </div>
                  ))
                ) : (
                  <div className="quiet-state">
                    <ShieldCheck size={25} />
                    <strong>No allergies in the recent record</strong>
                    <p>
                      This does not confirm the absence of allergies. Reconcile
                      with the patient at each visit.
                    </p>
                  </div>
                )}
              </div>
            </section>
            <section className="card mt-20">
              <div className="card-header">
                <h2>Latest vitals</h2>
                <span className="muted small">
                  {latest ? shortDate(latest.updatedAt) : "Not recorded"}
                </span>
              </div>
              <div className="vitals-summary">
                {[
                  ["Pulse", latest?.vitals.pulse, "bpm"],
                  [
                    "Blood pressure",
                    latest?.vitals.systolic
                      ? `${latest.vitals.systolic}/${latest.vitals.diastolic}`
                      : "—",
                    "mmHg",
                  ],
                  ["Temperature", latest?.vitals.temperatureC ?? "—", "°C"],
                  ["SpO₂", latest?.vitals.spo2 ?? "—", "%"],
                ].map(([label, value, unit]) => (
                  <div key={String(label)}>
                    <span>{label}</span>
                    <strong>
                      {value ?? "—"}
                      <small>{unit}</small>
                    </strong>
                  </div>
                ))}
              </div>
            </section>
            <div className="info-note standalone">
              <ShieldCheck size={18} />
              <span>
                Consent management, identity documents and ABHA linkage are
                planned integrations, not active in this slice.
              </span>
            </div>
          </div>
        </div>
      )}
      {tab === "Medical record" && (
        <section className="card">
          <div className="card-header">
            <h2>Longitudinal clinical record</h2>
            <Link to="/consultations" className="text-link">
              Clinical workspace <ArrowRight size={15} />
            </Link>
          </div>
          {chart.encounters.length ? (
            <div className="record-list">
              {chart.encounters.map((e) => (
                <Link key={e.id} to={`/consultations/${e.id}`}>
                  <span className="quick-icon teal">
                    <Stethoscope size={22} />
                  </span>
                  <div>
                    <strong>{e.diagnosis || "Consultation in progress"}</strong>
                    <p>
                      {e.chiefComplaint || "No chief complaint recorded"} ·{" "}
                      {shortDate(e.updatedAt)}
                    </p>
                    <small>
                      {data.doctors.find((d) => d.id === e.doctorId)?.name}
                    </small>
                  </div>
                  <Badge status={e.status} />
                  <ArrowRight size={17} />
                </Link>
              ))}
            </div>
          ) : (
            <Empty
              title={
                user.permissions.includes("clinical.read")
                  ? "No recent clinical records"
                  : "Clinical access restricted"
              }
              description="Authorized clinical records for this patient appear here."
            />
          )}
        </section>
      )}
      {tab === "Appointments" && (
        <section className="card">
          <div className="card-header">
            <h2>Appointments</h2>
            <button className="btn" onClick={() => setModal("book")}>
              <Plus size={16} />
              Book appointment
            </button>
          </div>
          {chart.appointments.length ? (
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Date & time</th>
                    <th>Practitioner</th>
                    <th>Type</th>
                    <th>Status</th>
                    <th>Token</th>
                  </tr>
                </thead>
                <tbody>
                  {chart.appointments.map((a) => (
                    <tr key={a.id}>
                      <td>
                        {shortDate(a.startsAt)} · {time(a.startsAt)}
                      </td>
                      <td>
                        {data.doctors.find((d) => d.id === a.doctorId)?.name}
                      </td>
                      <td>{a.mode}</td>
                      <td>
                        <Badge status={a.status} />
                      </td>
                      <td>{a.token || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty title="No recent appointments" />
          )}
        </section>
      )}
      {tab === "Billing" && (
        <section className="card">
          <div className="card-header">
            <h2>Invoices & payments</h2>
          </div>
          {chart.invoices.length ? (
            <div className="record-list">
              {chart.invoices.map((i) => (
                <Link to="/billing" key={i.id}>
                  <span className="quick-icon purple">
                    <FileText size={20} />
                  </span>
                  <div>
                    <strong>{i.number}</strong>
                    <p>
                      {shortDate(i.createdAt)} ·{" "}
                      {i.lines.map((l) => l.name).join(", ")}
                    </p>
                  </div>
                  <strong>{money(i.total)}</strong>
                  <Badge status={i.status} />
                </Link>
              ))}
            </div>
          ) : (
            <Empty
              title="No recent invoices"
              description="Create an invoice from the patient header or billing desk."
            />
          )}
        </section>
      )}
      {modal === "book" && (
        <BookingModal patientId={p.id} onClose={() => setModal(null)} />
      )}
      {modal === "bill" && (
        <InvoiceModal patientId={p.id} onClose={() => setModal(null)} />
      )}
    </>
  );
}
