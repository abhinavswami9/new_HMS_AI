import { useState } from "react";
import { UserCheck } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader } from "../components";
import { Field, Submit } from "../components";
import type { PortalAccount } from "../types";
export function Portal() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [register, setRegister] = useState(false);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
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
        eyebrow="PATIENT PORTAL"
        title="Verified access desk"
        description="Identity-proofed portal accounts, scoped proxy grants and revocable access. Patients see their own verified record, appointments, reports and bills — every view and download is audited. This console manages access; patients sign in to the portal audience."
      >
        {can("portal.admin") && (
          <button className="btn primary" onClick={() => setRegister(true)}>
            <UserCheck size={16} /> Register account
          </button>
        )}
      </PageHeader>
      <div className="panel">
        <h3>Portal accounts</h3>
        <table>
          <thead>
            <tr>
              <th>Account</th>
              <th>Patient</th>
              <th>Proofing</th>
              <th>Verified</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {care.portalAccounts.map((a: PortalAccount) => (
              <tr key={a.id}>
                <td>
                  <strong>{a.userName}</strong>
                </td>
                <td>{patient(a.patientId)?.fullName}</td>
                <td>
                  <small>{a.proofingEvidence}</small>
                </td>
                <td>{a.verifiedAt ? shortDate(a.verifiedAt) : "—"}</td>
                <td>
                  <Badge>{a.status}</Badge>
                </td>
                <td>
                  {can("portal.admin") && a.status === "Pending" && (
                    <button
                      className="btn ghost"
                      onClick={() =>
                        void act(
                          `/portal/accounts/${a.id}/verify`,
                          { verifiedBy: user.name, version: a.version },
                          "Account verified",
                        )
                      }
                    >
                      Verify
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {care.portalAccounts.length === 0 && (
          <Empty title="No portal accounts registered" />
        )}
      </div>
      <div className="panel">
        <h3>Proxy grants</h3>
        {care.proxyGrants.length === 0 && (
          <Empty title="No proxy access granted" />
        )}
        {care.proxyGrants.map((g) => (
          <div key={g.id} className="list-row">
            <div>
              <strong>{g.scope}</strong>
              <small>
                {patient(g.patientId)?.fullName} → account {g.accountId.slice(0, 8)} ·
                expires {shortDate(g.expiresOn)} · granted by {g.grantedBy}
              </small>
            </div>
            <div>
              <Badge>{g.status}</Badge>
              {g.revokedAt && (
                <small className="muted">revoked {shortDate(g.revokedAt)}</small>
              )}
            </div>
          </div>
        ))}
      </div>
      {register && (
        <Modal
          title="Register a portal account"
          onClose={() => setRegister(false)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/portal/accounts", "POST", {
                  patientId: f.get("patientId"),
                  userId: f.get("userId"),
                  userName: f.get("userName"),
                  proofingEvidence: f.get("proofingEvidence"),
                });
                await refresh();
                notify("Portal account registered — pending verification");
                setRegister(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
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
            <Field label="Login user">
              <select name="userId" required>
                <option value="30000000-0000-0000-0000-000000000020">patient · Dolly Chauhan</option>
                <option value="30000000-0000-0000-0000-000000000021">patient2 · Ramesh Gupta</option>
              </select>
            </Field>
            <Field label="Portal user name">
              <input name="userName" required />
            </Field>
            <Field label="Identity proofing evidence">
              <textarea name="proofingEvidence" rows={3} required />
            </Field>
            <p className="muted">
              Strong identity proofing is required before linkage. The account stays
              pending until verified.
            </p>
            <Submit label="Register account" />
          </form>
        </Modal>
      )}
    </>
  );
}
