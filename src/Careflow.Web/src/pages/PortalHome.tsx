import { useEffect, useState } from "react";
import {
  CalendarDays,
  ClipboardList,
  FileText,
  Download,
  Receipt,
  LogOut,
} from "lucide-react";
import { api, shortDate } from "../api";
import { Badge, Empty } from "../components";
import { useApp } from "../store";
type Profile = {
  account: { id: string; userName: string; status: string; patientId: string };
  patient: { fullName: string; uhid: string };
};
type Bill = {
  id: string;
  number: string;
  status: string;
  total: number;
  paid: number;
  payments: unknown[];
};
export function PortalHome() {
  const { user, notify, refresh } = useApp();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [appointments, setAppointments] = useState<{ id: string; startsAt: string; status: string; reason: string }[]>([]);
  const [prescriptions, setPrescriptions] = useState<{ id: string; date: string; items: { medicineName: string; dose: string }[] }[]>([]);
  const [reports, setReports] = useState<{ labs: { id: string; number: string; status: string }[]; radiology: { id: string; number: string; status: string }[] }>({ labs: [], radiology: [] });
  const [bills, setBills] = useState<Bill[]>([]);
  const load = async () => {
    try {
      const [p, a, rx, r, b] = await Promise.all([
        api<Profile>("/portal/profile"),
        api("/portal/appointments"),
        api("/portal/prescriptions"),
        api("/portal/reports"),
        api<Bill[]>("/portal/bills"),
      ]);
      setProfile(p);
      setAppointments(a as typeof appointments);
      setPrescriptions((rx as { items: typeof prescriptions }).items);
      setReports(r as typeof reports);
      setBills(b);
    } catch (e) {
      notify((e as Error).message, "error");
    }
  };
  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return (
    <div className="shell">
      <header className="topbar">
        <span className="wordmark">
          Careflow <span>·</span> Patient portal
        </span>
        <div className="topbar-right">
          <span className="muted">{user.name}</span>
          <button className="icon-button" aria-label="Sign out" onClick={() => void refresh(null)}>
            <LogOut size={18} />
          </button>
        </div>
      </header>
      <main className="content">
        <div className="page-header">
          <div>
            <span className="eyebrow">YOUR HEALTH RECORD</span>
            <h1>{profile ? `${profile.patient.fullName}` : "Loading your portal"}</h1>
            <p>
              {profile
                ? `${profile.patient.uhid} · verified account ${profile.account.userName} · every view and download is audited.`
                : "Verified access to your appointments, records, reports and bills."}
            </p>
          </div>
        </div>
        <div className="split-2">
          <div className="panel">
            <h3>
              <CalendarDays size={16} /> Appointments
            </h3>
            {appointments.length === 0 && <Empty title="No upcoming appointments" />}
            {appointments.map((a) => (
              <div key={a.id} className="list-row">
                <div>
                  <strong>{a.reason}</strong>
                  <small>{shortDate(a.startsAt)}</small>
                </div>
                <Badge>{a.status}</Badge>
              </div>
            ))}
            <h3>
              <ClipboardList size={16} /> Prescriptions
            </h3>
            {prescriptions.length === 0 && <Empty title="No prescriptions recorded" />}
            {prescriptions.map((rx) => (
              <div key={rx.id} className="list-row">
                <div>
                  <strong>
                    {rx.items.map((i) => i.medicineName).join(", ")}
                  </strong>
                  <small>{shortDate(rx.date)}</small>
                </div>
              </div>
            ))}
          </div>
          <div className="panel">
            <h3>
              <FileText size={16} /> Reports
            </h3>
            {reports.labs.length === 0 && reports.radiology.length === 0 && (
              <Empty title="No reports released yet" />
            )}
            {[...reports.labs, ...reports.radiology].map((r) => (
              <div key={r.id} className="list-row">
                <div>
                  <strong>{r.number}</strong>
                  <small>Authorized for release</small>
                </div>
                <div>
                  <Badge>{r.status}</Badge>
                  <button
                    className="btn ghost"
                    onClick={async () => {
                      try {
                        const doc = await api<{
                          content: string;
                          watermark: string;
                        }>(
                          `/portal/documents/${r.id}/download?kind=${reports.labs.some((l) => l.id === r.id) ? "lab" : "radiology"}`,
                        );
                        notify(`Downloaded · ${doc.watermark}`);
                      } catch (e) {
                        notify((e as Error).message, "error");
                      }
                    }}
                  >
                    <Download size={14} /> Download
                  </button>
                </div>
              </div>
            ))}
            <h3>
              <Receipt size={16} /> Bills
            </h3>
            {bills.length === 0 && <Empty title="No bills on your account" />}
            {bills.map((b) => (
              <div key={b.id} className="list-row">
                <div>
                  <strong>{b.number}</strong>
                  <small>
                    ₹{b.total} · paid ₹{b.paid}
                  </small>
                </div>
                <div>
                  <Badge>{b.status}</Badge>
                  {b.status === "Issued" && (
                    <button
                      className="btn ghost"
                      onClick={async () => {
                        try {
                          await api("/portal/payments", "POST", {
                            invoiceId: b.id,
                            amount: 5,
                            method: "Card",
                            reference: `PORTAL-${b.number}`,
                          });
                          await load();
                          notify("Payment recorded on your ledger");
                        } catch (e) {
                          notify((e as Error).message, "error");
                        }
                      }}
                    >
                      Pay
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
        <p className="muted">
          This portal shows only verified records linked to your account. Proxy
          access is scoped, time-limited and revocable by you.
        </p>
      </main>
    </div>
  );
}
