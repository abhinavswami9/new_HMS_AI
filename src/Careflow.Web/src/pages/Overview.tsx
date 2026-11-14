import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  ArrowDownToLine,
  ArrowRight,
  CalendarDays,
  ChevronRight,
  Clock3,
  FileCheck2,
  IndianRupee,
  Plus,
  Stethoscope,
  TrendingUp,
  UserRoundPlus,
  Users,
  Activity,
  CircleAlert,
} from "lucide-react";
import { useApp } from "../store";
import { Avatar, Badge, PageHeader } from "../components";
import { BookingModal, RegistrationModal } from "../forms";
import { age, money, time } from "../api";
export function Overview() {
  const { data, user, demo, truncated } = useApp();
  const navigate = useNavigate();
  const [modal, setModal] = useState<"book" | "register" | null>(null);
  const today = new Date().toDateString();
  const appointments = data.appointments
    .filter(
      (a) =>
        new Date(a.startsAt).toDateString() === today &&
        a.status !== "Cancelled",
    )
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  const checkedIn = appointments.filter(
    (a) => a.status === "Checked in",
  ).length;
  const active = data.encounters.filter((e) => e.status === "Draft").length;
  const revenue = data.invoices.reduce(
    (s, i) =>
      s +
      i.payments
        .filter((p) => new Date(p.receivedAt).toDateString() === today)
        .reduce((a, p) => a + p.amount, 0),
    0,
  );
  const outstanding = data.invoices.filter((i) =>
    ["Issued", "Part paid"].includes(i.status),
  );
  const drafts = data.encounters.filter((e) => e.status === "Draft");
  const departments = data.doctors.map((d) => ({
    ...d,
    count: appointments.filter((a) => a.doctorId === d.id).length,
  }));
  const max = Math.max(1, ...departments.map((d) => d.count));
  const firstName = user.name.replace("Dr. ", "").split(" ")[0];
  const hour = new Date().getHours();
  const greeting =
    hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
  return (
    <>
      <PageHeader
        eyebrow="YOUR HOSPITAL, AT A GLANCE"
        title={`${greeting}, ${firstName}`}
        description="Here’s what’s happening at your hospital today."
      >
        <div className="date-chip">
          <CalendarDays size={16} />
          {new Date().toLocaleDateString("en-GB", {
            day: "numeric",
            month: "short",
            year: "numeric",
          })}
        </div>
        {user.permissions.includes("schedule.write") && (
          <button className="btn primary" onClick={() => setModal("book")}>
            <Plus size={17} />
            New appointment
          </button>
        )}
      </PageHeader>
      <div className="welcome-strip">
        <div className="welcome-icon">
          <Activity size={21} />
        </div>
        <div>
          <strong>A little more connected. A lot more care.</strong>
          <span>
            Your clinical and administrative workflows, in one workspace.
          </span>
        </div>
        <span className="live-label">
          <i />
          {demo ? "Synthetic preview" : "Hospital workspace"}
        </span>
      </div>
      {truncated && (
        <div className="info-note standalone">
          This overview shows recent records only. Use governed reports for
          complete hospital totals.
        </div>
      )}
      <div className="kpi-grid">
        <Kpi
          title="Today’s appointments"
          value={appointments.length.toString().padStart(2, "0")}
          icon={<CalendarDays size={21} />}
          color="teal"
          detail={
            <>
              <span className="positive">{checkedIn} checked in</span>
              <span>
                across {new Set(appointments.map((a) => a.doctorId)).size}{" "}
                practitioners
              </span>
            </>
          }
          points={[3, 5, 4, 7, 5, 8, 10, 8, 12, 11, 14]}
        />
        <Kpi
          title="Registered patients"
          value={data.patients.length.toLocaleString("en-IN")}
          icon={<Users size={21} />}
          color="blue"
          detail={
            <>
              <span className="positive">
                +
                {
                  data.patients.filter(
                    (p) => new Date(p.createdAt).toDateString() === today,
                  ).length
                }{" "}
                today
              </span>
              <span>in the recent registry</span>
            </>
          }
          points={[2, 4, 3, 5, 7, 6, 8, 7, 10, 11, 12]}
        />
        <Kpi
          title="Active consultations"
          value={active.toString().padStart(2, "0")}
          icon={<Stethoscope size={21} />}
          color="amber"
          detail={
            <>
              <span className="warning-text">
                {drafts.length} awaiting sign-off
              </span>
              <span>clinical records</span>
            </>
          }
          points={[8, 8, 6, 7, 5, 6, 4, 5, 3, 4, 3]}
        />
        <Kpi
          title="Payments recorded"
          value={money(revenue)}
          icon={<IndianRupee size={21} />}
          color="purple"
          detail={
            <>
              <span className="positive">
                {data.invoices.filter((i) => i.status === "Paid").length}{" "}
                settled invoices
              </span>
              <span>in recent billing</span>
            </>
          }
          points={[3, 4, 3, 6, 5, 7, 9, 8, 11, 12, 13]}
        />
      </div>
      <div className="dashboard-grid">
        <section className="card appointments-card">
          <div className="card-header">
            <div>
              <h2>
                Today’s appointments{" "}
                <span className="count-badge">{appointments.length}</span>
              </h2>
              <p>A clear view of your patient flow</p>
            </div>
            <Link className="text-link" to="/appointments">
              View all <ArrowRight size={15} />
            </Link>
          </div>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Practitioner</th>
                  <th>Time</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {appointments.slice(0, 6).map((a) => {
                  const p = data.patients.find((p) => p.id === a.patientId),
                    d = data.doctors.find((d) => d.id === a.doctorId);
                  if (!p || !d) return null;
                  return (
                    <tr
                      key={a.id}
                      onClick={() => navigate(`/patients/${p.id}`)}
                      tabIndex={0}
                      onKeyDown={(e) =>
                        e.key === "Enter" && navigate(`/patients/${p.id}`)
                      }
                    >
                      <td>
                        <div className="person-cell">
                          <Avatar name={p.fullName} />
                          <div>
                            <strong>{p.fullName}</strong>
                            <small>
                              {p.uhid} <span className="dot-separator">·</span>{" "}
                              {age(p.dateOfBirth)} yrs, {p.sex[0]}
                            </small>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div className="doctor-cell">
                          {d.name}
                          <small>{d.department}</small>
                        </div>
                      </td>
                      <td className="time-cell">{time(a.startsAt)}</td>
                      <td>
                        <Badge status={a.status} />
                      </td>
                      <td>
                        <ChevronRight size={16} className="muted" />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {!appointments.length && (
              <div className="empty-state">
                <CalendarDays size={26} />
                <h3>A clear calendar</h3>
                <p>Book an appointment to get started.</p>
              </div>
            )}
          </div>
          <div className="card-bottom">
            <span>
              <Clock3 size={14} />
              Times shown in your browser’s timezone
            </span>
            <Link to="/appointments">
              Open live queue <ArrowRight size={14} />
            </Link>
          </div>
        </section>
        <div className="dashboard-right">
          <section className="card quick-actions">
            <div className="card-header">
              <h2>Quick actions</h2>
              <span className="subtle-icon">↗</span>
            </div>
            <div className="quick-grid">
              {user.permissions.includes("patient.write") && (
                <button onClick={() => setModal("register")}>
                  <span className="quick-icon teal">
                    <UserRoundPlus size={21} />
                  </span>
                  <span>
                    Register patient<small>Create a new UHID</small>
                  </span>
                  <ChevronRight size={15} />
                </button>
              )}
              <button onClick={() => navigate("/consultations")}>
                <span className="quick-icon blue">
                  <FileCheck2 size={21} />
                </span>
                <span>
                  Clinical workspace<small>Records & prescriptions</small>
                </span>
                <ChevronRight size={15} />
              </button>
              <button onClick={() => navigate("/billing")}>
                <span className="quick-icon purple">
                  <ArrowDownToLine size={21} />
                </span>
                <span>
                  Billing desk<small>Invoices & payments</small>
                </span>
                <ChevronRight size={15} />
              </button>
            </div>
          </section>
          <section className="card department-card">
            <div className="card-header">
              <div>
                <h2>Appointments by department</h2>
                <p>Today’s scheduled visits</p>
              </div>
              <TrendingUp size={18} className="muted" />
            </div>
            <div className="department-bars">
              {departments.slice(0, 4).map((d, i) => (
                <div key={d.id}>
                  <div>
                    <span>
                      <i
                        style={{
                          background: [
                            "#168b80",
                            "#60a5c9",
                            "#9b8ac4",
                            "#e5b566",
                          ][i],
                        }}
                      />
                      {d.department}
                    </span>
                    <strong>{d.count}</strong>
                  </div>
                  <div className="bar-track">
                    <span
                      style={{
                        width: `${Math.max(4, (d.count / max) * 100)}%`,
                        background: [
                          "#168b80",
                          "#60a5c9",
                          "#9b8ac4",
                          "#e5b566",
                        ][i],
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      </div>
      <div className="dashboard-bottom">
        <section className="card attention-card">
          <div className="card-header">
            <h2>
              <CircleAlert size={18} className="warning-text" />
              Needs your attention
            </h2>
            <span className="count-badge">
              {(outstanding.length > 0 ? 1 : 0) + (drafts.length > 0 ? 1 : 0)}
            </span>
          </div>
          <div className="attention-list">
            {drafts.length > 0 && (
              <Link to="/consultations">
                <span className="attention-icon amber">
                  <FileCheck2 size={19} />
                </span>
                <div>
                  <strong>
                    {drafts.length} consultation{drafts.length > 1 ? "s" : ""}{" "}
                    awaiting sign-off
                  </strong>
                  <p>Review clinical notes and finalize patient records.</p>
                </div>
                <ArrowRight size={17} />
              </Link>
            )}
            {outstanding.length > 0 && (
              <Link to="/billing">
                <span className="attention-icon blue">
                  <IndianRupee size={19} />
                </span>
                <div>
                  <strong>
                    {money(outstanding.reduce((s, i) => s + i.balance, 0))}{" "}
                    outstanding
                  </strong>
                  <p>{outstanding.length} issued invoices awaiting payment.</p>
                </div>
                <ArrowRight size={17} />
              </Link>
            )}
            {!drafts.length && !outstanding.length && (
              <div className="all-clear">
                <FileCheck2 size={24} />
                You’re all caught up. Nice work.
              </div>
            )}
          </div>
        </section>
        <section className="card activity-card">
          <div className="card-header">
            <h2>Recent activity</h2>
            <Link to="/administration?tab=audit" className="text-link">
              Audit trail <ArrowRight size={14} />
            </Link>
          </div>
          <div className="activity-list">
            {data.audit.slice(0, 3).map((a) => (
              <div key={a.id}>
                <span className="activity-dot" />
                <div>
                  <strong>
                    {a.action
                      .replaceAll(".", " ")
                      .replace(/^./, (c) => c.toUpperCase())}
                  </strong>
                  <p>
                    {a.actorName} <span>·</span> {time(a.occurredAt)}
                  </p>
                </div>
                <span className="audit-tag">{a.entity}</span>
              </div>
            ))}
            {!data.audit.length && (
              <p className="muted small">
                Activity visible to your role will appear here.
              </p>
            )}
          </div>
        </section>
      </div>
      <div className="workspace-footnote">
        <ShieldIcon />
        Patient care comes first. Every change is traceable.
        <span>
          Careflow HIS <span>·</span> Hospital workspace
        </span>
      </div>
      {modal === "book" && <BookingModal onClose={() => setModal(null)} />}
      {modal === "register" && (
        <RegistrationModal onClose={() => setModal(null)} />
      )}
    </>
  );
}
function ShieldIcon() {
  return <FileCheck2 size={14} />;
}
function Kpi({
  title,
  value,
  icon,
  color,
  detail,
}: {
  title: string;
  value: string;
  icon: React.ReactNode;
  color: string;
  detail: React.ReactNode;
  points: number[];
}) {
  return (
    <section className={`card kpi ${color}`}>
      <div className="kpi-top">
        <span>{title}</span>
        <span className={`kpi-icon ${color}`}>{icon}</span>
      </div>
      <div className="kpi-value">{value}</div>
      <div className="kpi-detail">{detail}</div>
    </section>
  );
}
