import { useState } from "react";
import { Calculator, IndianRupee } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader } from "../components";
import { Field, Submit } from "../components";
import type { IncentiveBatch } from "../types";
export function Incentives() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [calculate, setCalculate] = useState(false);
  const [pay, setPay] = useState<IncentiveBatch | null>(null);
  const [rule, setRule] = useState(false);
  const can = (p: string) => user.permissions.includes(p);
  const doctor = (id: string) => data.doctors.find((s) => s.id === id);
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
        eyebrow="DOCTOR INCENTIVES"
        title="Revenue share & payouts"
        description="Rule-based attribution of consultations, revenue share, surgeries and procedures to a closed-period payout batch with separation of duties."
      >
        {can("incentive.calculate") && (
          <button className="btn primary" onClick={() => setCalculate(true)}>
            <Calculator size={16} /> Calculate batch
          </button>
        )}
      </PageHeader>
      <div className="split-2">
        <div className="panel">
          <div className="panel-head">
            <h3>Payout batches</h3>
            {can("incentive.rule") && (
              <button className="btn ghost" onClick={() => setRule(true)}>
                New rule
              </button>
            )}
          </div>
          {care.incentiveBatches.length === 0 && (
            <Empty title="No incentive batches calculated" />
          )}
          {care.incentiveBatches.map((b) => (
            <div key={b.id} className="list-row">
              <div>
                <strong>{b.number}</strong>
                <small>
                  {shortDate(b.periodStart)} – {shortDate(b.periodEnd)} · by{" "}
                  {b.calculatedBy}
                </small>
              </div>
              <div>
                <strong>₹{b.totalAmount.toLocaleString("en-IN")}</strong>{" "}
                <Badge>{b.status}</Badge>
                {can("incentive.approve") && b.status === "Calculated" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/incentives/batches/${b.id}/approve`,
                        {
                          reason: "Reviewed against the payout register.",
                          version: b.version,
                        },
                        "Batch approved",
                      )
                    }
                  >
                    Approve
                  </button>
                )}
                {can("incentive.pay") && b.status === "Approved" && (
                  <button className="btn ghost" onClick={() => setPay(b)}>
                    <IndianRupee size={14} /> Pay
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
        <div className="panel">
          <h3>Rules</h3>
          {care.incentiveRules.length === 0 && (
            <Empty title="No incentive rules configured" />
          )}
          {care.incentiveRules.map((r) => (
            <div key={r.id} className="list-row">
              <div>
                <strong>{r.name}</strong>
                <small>
                  {r.scopeKind} · {r.basis} · {r.mode} {r.value}
                  {r.mode === "Percent" ? "%" : ""} · from{" "}
                  {shortDate(r.effectiveFrom)}
                </small>
              </div>
              <div>
                <Badge>{r.status}</Badge>
                {can("incentive.rule") && r.status === "Draft" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/incentives/rules/${r.id}/status/Active`,
                        { version: r.version, reason: "Approved for payouts." },
                        "Rule activated",
                      )
                    }
                  >
                    Activate
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="panel">
        <h3>Latest batch lines</h3>
        {care.incentiveLines.length === 0 && <Empty title="No calculated lines" />}
        {care.incentiveLines.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Doctor</th>
                <th>Basis</th>
                <th>Basis amount</th>
                <th>Incentive</th>
                <th>Snapshot</th>
              </tr>
            </thead>
            <tbody>
              {care.incentiveLines.map((l) => (
                <tr key={l.id}>
                  <td>{doctor(l.doctorId)?.name}</td>
                  <td>{l.basisLabel}</td>
                  <td>₹{l.basisAmount.toLocaleString("en-IN")}</td>
                  <td>
                    <strong>₹{l.incentiveAmount.toLocaleString("en-IN")}</strong>
                  </td>
                  <td>
                    <small>{l.snapshot}</small>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      {calculate && (
        <Modal title="Calculate payout batch" onClose={() => setCalculate(false)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/incentives/calculate", "POST", {
                  periodStart: f.get("periodStart"),
                  periodEnd: f.get("periodEnd"),
                });
                await refresh();
                notify("Batch calculated");
                setCalculate(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <div className="form-grid">
              <Field label="Period start">
                <input type="date" name="periodStart" required />
              </Field>
              <Field label="Period end">
                <input type="date" name="periodEnd" required />
              </Field>
            </div>
            <p className="muted">
              Closed period: the window must end before today.
            </p>
            <Submit label="Calculate" />
          </form>
        </Modal>
      )}
      {pay && (
        <Modal title={`Pay ${pay.number}`} onClose={() => setPay(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(`/incentives/batches/${pay.id}/pay`, "POST", {
                  amount: Number(f.get("amount")),
                  mode: f.get("mode"),
                  reference: f.get("reference"),
                  version: pay.version,
                });
                await refresh();
                notify("Payment recorded");
                setPay(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <p className="muted">
              Batch total ₹{pay.totalAmount.toLocaleString("en-IN")} · partial
              payments allowed up to the total.
            </p>
            <Field label="Amount">
              <input type="number" name="amount" required />
            </Field>
            <Field label="Mode">
              <select name="mode">
                <option>Bank transfer</option>
                <option>Cheque</option>
                <option>Cash</option>
                <option>Adjustment</option>
              </select>
            </Field>
            <Field label="Reference">
              <input name="reference" required />
            </Field>
            <Submit label="Record payment" />
          </form>
        </Modal>
      )}
      {rule && (
        <Modal title="New incentive rule" onClose={() => setRule(false)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/incentives/rules", "POST", {
                  name: f.get("name"),
                  scopeKind: f.get("scopeKind"),
                  doctorId: f.get("scopeKind") === "Doctor" ? f.get("doctorId") : null,
                  basis: f.get("basis"),
                  mode: f.get("mode"),
                  value: Number(f.get("value")),
                  effectiveFrom: f.get("effectiveFrom"),
                  effectiveTo: f.get("effectiveTo") || null,
                });
                await refresh();
                notify("Rule drafted");
                setRule(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <Field label="Name">
              <input name="name" required />
            </Field>
            <div className="form-grid">
              <Field label="Scope">
                <select name="scopeKind">
                  <option>All</option>
                  <option>Doctor</option>
                </select>
              </Field>
              <Field label="Doctor (scope=Doctor)">
                <select name="doctorId">
                  {data.doctors.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </Field>
            </div>
            <div className="form-grid">
              <Field label="Basis">
                <select name="basis">
                  <option>Consultation</option>
                  <option>Revenue</option>
                  <option>Surgery</option>
                  <option>Procedure</option>
                </select>
              </Field>
              <Field label="Mode">
                <select name="mode">
                  <option>Fixed</option>
                  <option>Percent</option>
                </select>
              </Field>
            </div>
            <Field label="Value">
              <input type="number" step="0.01" name="value" required />
            </Field>
            <div className="form-grid">
              <Field label="Effective from">
                <input type="date" name="effectiveFrom" required />
              </Field>
              <Field label="Effective to">
                <input type="date" name="effectiveTo" />
              </Field>
            </div>
            <Submit label="Create draft rule" />
          </form>
        </Modal>
      )}
    </>
  );
}
