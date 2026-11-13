import { useState } from "react";
import { Droplet, Syringe } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { BloodUnit } from "../types";
export function BloodBank() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [receive, setReceive] = useState(false);
  const [screen, setScreen] = useState<BloodUnit | null>(null);
  const [crossmatch, setCrossmatch] = useState<BloodUnit | null>(null);
  const [issue, setIssue] = useState<BloodUnit | null>(null);
  const [transfuse, setTransfuse] = useState<string | null>(null);
  const [ret, setRet] = useState<string | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const filtered = care.bloodUnits.filter(
    (u) =>
      `${u.unitNumber} ${u.bloodGroup} ${u.component} ${u.donorName}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All" || u.status === status),
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
        eyebrow="BLOOD BANK"
        title="Inventory & issues"
        description="Unit receipt, screening quarantine, crossmatch compatibility and closed-loop issue to transfusion."
      >
        {can("blood.receive") && (
          <button className="btn primary" onClick={() => setReceive(true)}>
            <Droplet size={16} /> Receive unit
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          {[
            "All",
            "Quarantined",
            "Available",
            "Reserved",
            "Issued",
            "Transfused",
            "Discarded",
          ].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </div>
      {filtered.length === 0 ? (
        <Empty
          title="No blood units"
          description="Receive a unit to begin the blood bank workflow."
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Unit</th>
                <th>Group / component</th>
                <th>Expiry</th>
                <th>Status</th>
                <th>Donor</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((u) => (
                <tr key={u.id}>
                  <td>
                    <strong>{u.unitNumber}</strong>
                    <div className="muted">{shortDate(u.collectedAt)}</div>
                  </td>
                  <td>
                    <Badge tone="red">{u.bloodGroup}</Badge> {u.component}
                  </td>
                  <td>{shortDate(u.expiryDate)}</td>
                  <td>
                    <Badge
                      tone={
                        u.status === "Available"
                          ? "green"
                          : u.status === "Discarded"
                            ? "gray"
                            : u.status === "Quarantined"
                              ? "amber"
                              : "blue"
                      }
                    >
                      {u.status}
                    </Badge>
                  </td>
                  <td>{u.donorName}</td>
                  <td className="row-actions">
                    {u.status === "Quarantined" && can("blood.screen") && (
                      <button
                        className="btn small"
                        onClick={() => setScreen(u)}
                      >
                        Screen
                      </button>
                    )}
                    {u.status === "Available" && can("blood.crossmatch") && (
                      <button
                        className="btn small"
                        onClick={() => setCrossmatch(u)}
                      >
                        Crossmatch
                      </button>
                    )}
                    {u.status === "Available" && can("blood.screen") && (
                      <button
                        className="btn small ghost"
                        onClick={() =>
                          act(
                            `/blood/units/${u.id}/dispose`,
                            {
                              reason: "Disposed from inventory board",
                              version: u.version,
                            },
                            "Unit disposed",
                          )
                        }
                      >
                        Dispose
                      </button>
                    )}
                    {u.status === "Reserved" && can("blood.issue") && (
                      <button className="btn small" onClick={() => setIssue(u)}>
                        <Syringe size={14} /> Issue
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <h2 className="section-title">Issues</h2>
      {care.bloodIssues.length === 0 ? (
        <Empty
          title="No blood issues"
          description="Issue a reserved unit to a patient."
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Number</th>
                <th>Patient</th>
                <th>Issued</th>
                <th>Status</th>
                <th>Reaction</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {care.bloodIssues.map((i) => (
                <tr key={i.id}>
                  <td>
                    <strong>{i.number}</strong>
                  </td>
                  <td>{patient(i.patientId)?.fullName ?? "—"}</td>
                  <td>
                    {shortDate(i.issuedAt)}
                    <div className="muted">{time(i.issuedAt)}</div>
                  </td>
                  <td>
                    <Badge
                      tone={
                        i.status === "Transfused"
                          ? "green"
                          : i.status === "Returned"
                            ? "gray"
                            : "blue"
                      }
                    >
                      {i.status}
                    </Badge>
                  </td>
                  <td>{i.reaction || <span className="muted">—</span>}</td>
                  <td className="row-actions">
                    {i.status === "Issued" && can("blood.transfuse") && (
                      <button
                        className="btn small"
                        onClick={() => setTransfuse(i.id)}
                      >
                        Transfuse
                      </button>
                    )}
                    {i.status === "Issued" && can("blood.issue") && (
                      <button
                        className="btn small ghost"
                        onClick={() => setRet(i.id)}
                      >
                        Return
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
        open={receive}
        onClose={() => setReceive(false)}
        title="Receive blood unit"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/blood/units",
              {
                unitNumber: f.get("unitNumber"),
                bloodGroup: f.get("bloodGroup"),
                component: f.get("component"),
                expiryDate: f.get("expiryDate"),
                donorName: f.get("donorName"),
              },
              "Unit received into quarantine",
            );
            setReceive(false);
          }}
        >
          <div className="grid-2">
            <Field label="Unit number">
              <input
                name="unitNumber"
                required
                minLength={2}
                placeholder="NB-2405"
              />
            </Field>
            <Field label="Blood group">
              <select name="bloodGroup">
                {["O-", "O+", "A-", "A+", "B-", "B+", "AB-", "AB+"].map((g) => (
                  <option key={g}>{g}</option>
                ))}
              </select>
            </Field>
            <Field label="Component">
              <select name="component">
                {["WholeBlood", "PRBC", "FFP", "Platelets"].map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </Field>
            <Field label="Expiry date">
              <input type="date" name="expiryDate" required />
            </Field>
          </div>
          <Field label="Donor">
            <input name="donorName" required minLength={2} />
          </Field>
          <Submit label="Receive unit" />
        </form>
      </Modal>
      <Modal
        open={!!screen}
        onClose={() => setScreen(null)}
        title="Screening results"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              `/blood/units/${screen!.id}/screen`,
              {
                hbsag: f.get("hbsag") === "on",
                hiv: f.get("hiv") === "on",
                hcv: f.get("hcv") === "on",
                vdrl: f.get("vdrl") === "on",
                notes: f.get("notes") ?? "",
              },
              "Screening recorded",
            );
            setScreen(null);
          }}
        >
          <p className="muted">
            Mark any reactive marker. Reactive units are discarded with the
            reason retained; clean units become available for crossmatch.
          </p>
          {[
            ["hbsag", "HBsAg reactive"],
            ["hiv", "HIV reactive"],
            ["hcv", "HCV reactive"],
            ["vdrl", "VDRL reactive"],
          ].map(([name, label]) => (
            <label className="check-row" key={name}>
              <input type="checkbox" name={name} /> {label}
            </label>
          ))}
          <Field label="Notes">
            <input name="notes" maxLength={500} />
          </Field>
          <Submit label="Record screening" />
        </form>
      </Modal>
      <Modal
        open={!!crossmatch}
        onClose={() => setCrossmatch(null)}
        title="Crossmatch"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/blood/crossmatches",
              { unitId: crossmatch!.id, patientId: f.get("patientId") },
              "Crossmatch recorded",
            );
            setCrossmatch(null);
          }}
        >
          <Field label="Recipient patient">
            <select name="patientId" required>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.bloodGroup} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <p className="muted">
            Component-specific compatibility is enforced server-side. An
            incompatible selection is rejected and audited; the unit is not
            reserved.
          </p>
          <Submit label="Perform crossmatch" />
        </form>
      </Modal>
      <Modal open={!!issue} onClose={() => setIssue(null)} title="Issue unit">
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            await act(
              "/blood/issues",
              {
                unitId: issue!.id,
                patientId: f.get("patientId"),
                admissionId: f.get("admissionId") || null,
                notes: f.get("notes") ?? "",
              },
              "Unit issued",
            );
            setIssue(null);
          }}
        >
          <Field label="Recipient patient">
            <select name="patientId" required>
              {data.patients.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName} · {p.bloodGroup} · {p.uhid}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Admission (optional)">
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
          <Field label="Notes">
            <input name="notes" maxLength={500} />
          </Field>
          <Submit label="Issue unit" />
        </form>
      </Modal>
      <Modal
        open={!!transfuse}
        onClose={() => setTransfuse(null)}
        title="Record transfusion"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            const issueRow = care.bloodIssues.find((i) => i.id === transfuse);
            await act(
              `/blood/issues/${transfuse}/transfuse`,
              {
                reaction: f.get("reaction"),
                notes: f.get("notes") ?? "",
                version: issueRow!.version,
              },
              "Transfusion recorded",
            );
            setTransfuse(null);
          }}
        >
          <Field label="Reaction (Nil if uneventful)">
            <input
              name="reaction"
              required
              maxLength={1000}
              placeholder="Nil"
            />
          </Field>
          <Field label="Notes">
            <input name="notes" maxLength={500} />
          </Field>
          <p className="muted">
            A reaction other than Nil/None raises a critical blood alert and
            locks the issue record.
          </p>
          <Submit label="Record transfusion" />
        </form>
      </Modal>
      <Modal
        open={!!ret}
        onClose={() => setRet(null)}
        title="Return issued unit"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            const issueRow = care.bloodIssues.find((i) => i.id === ret);
            await act(
              `/blood/issues/${ret}/return`,
              {
                assessedFit: f.get("assessedFit") === "on",
                notes: f.get("notes"),
                version: issueRow!.version,
              },
              "Unit returned",
            );
            setRet(null);
          }}
        >
          <label className="check-row">
            <input type="checkbox" name="assessedFit" /> Assessed fit for
            reissue
          </label>
          <Field label="Notes">
            <input name="notes" required minLength={3} maxLength={500} />
          </Field>
          <Submit label="Return unit" />
        </form>
      </Modal>
    </>
  );
}
