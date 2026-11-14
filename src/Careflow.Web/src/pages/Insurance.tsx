import { useState } from "react";
import { FileCheck2, ReceiptText } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { Claim } from "../types";
export function Insurance() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [register, setRegister] = useState(false);
  const [settle, setSettle] = useState<Claim | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const patient = (id: string) => data.patients.find((p) => p.id === id);
  const plan = (id: string) => care.plans.find((x) => x.id === id);
  const coverage = (id: string) => care.coverages.find((x) => x.id === id);
  const filtered = care.coverages.filter((c) =>
    `${c.number} ${c.policyNumber} ${patient(c.patientId)?.fullName ?? ""}`
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
        eyebrow="INSURANCE & TPA"
        title="Payer desk"
        description="Payer empanelment, policy coverage and eligibility, preauthorisation, claim lifecycle and cashless settlement."
      >
        {can("insurance.coverage") && (
          <button className="btn primary" onClick={() => setRegister(true)}>
            <FileCheck2 size={16} /> Register coverage
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
      </div>
      <div className="panel">
        <table>
          <thead>
            <tr>
              <th>Coverage</th>
              <th>Patient</th>
              <th>Plan</th>
              <th>Policy period</th>
              <th>Sum insured</th>
              <th>Claims</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((c) => {
              const claims = care.claims.filter((x) => x.coverageId === c.id);
              return (
                <tr key={c.id}>
                  <td>
                    <strong>{c.number}</strong>
                    <br />
                    <small>{c.policyNumber}</small>
                  </td>
                  <td>{patient(c.patientId)?.fullName}</td>
                  <td>{plan(c.planId)?.name}</td>
                  <td>
                    {shortDate(c.validFrom)} – {shortDate(c.validTo)}
                  </td>
                  <td>₹{c.sumInsured.toLocaleString("en-IN")}</td>
                  <td>
                    {claims.length === 0 ? (
                      <span className="muted">None</span>
                    ) : (
                      claims.map((x) => (
                        <button
                          key={x.id}
                          className="link-btn"
                          onClick={() => setSettle(x)}
                        >
                          {x.number} · {x.status}
                        </button>
                      ))
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {filtered.length === 0 && <Empty title="No policy coverages yet" />}
      </div>
      <div className="split-2">
        <div className="panel">
          <h3>Preauthorizations</h3>
          {care.preauthorizations.length === 0 && (
            <Empty title="No preauthorizations raised" />
          )}
          {care.preauthorizations.map((p) => (
            <div key={p.id} className="list-row">
              <div>
                <strong>{p.number}</strong>
                <small>
                  {patient(p.patientId)?.fullName} · {p.diagnosisCode} · ₹
                  {p.requestedAmount.toLocaleString("en-IN")}
                </small>
              </div>
              <div>
                <Badge>{p.status}</Badge>
                {can("insurance.preauth") && p.status === "Requested" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/insurance/preauthorizations/${p.id}/respond`,
                        {
                          decision: "Approved",
                          approvedAmount: p.requestedAmount,
                          note: "Approved at panel tariff rates.",
                          version: p.version,
                        },
                        "Payer response recorded",
                      )
                    }
                  >
                    Approve
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
        <div className="panel">
          <h3>Claims pipeline</h3>
          {care.claims.length === 0 && <Empty title="No claims raised" />}
          {care.claims.map((x) => (
            <div key={x.id} className="list-row">
              <div>
                <strong>{x.number}</strong>
                <small>
                  {patient(x.patientId)?.fullName} · ₹
                  {x.totalAmount.toLocaleString("en-IN")} ·{" "}
                  {coverage(x.coverageId)?.policyNumber}
                </small>
              </div>
              <div>
                <Badge>{x.status}</Badge>
                {can("insurance.claim") && x.status === "Draft" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/insurance/claims/${x.id}/submit`,
                        { version: x.version, reason: "" },
                        "Claim submitted to payer",
                      )
                    }
                  >
                    Submit
                  </button>
                )}
                {can("insurance.settle") && x.status === "Approved" && (
                  <button
                    className="btn ghost"
                    onClick={() => setSettle(x)}
                  >
                    <ReceiptText size={14} /> Settle
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      {register && (
        <Modal
          title="Register policy coverage"
          onClose={() => setRegister(false)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/insurance/coverages", "POST", {
                  patientId: f.get("patientId"),
                  planId: f.get("planId"),
                  policyNumber: f.get("policyNumber"),
                  memberId: f.get("memberId"),
                  validFrom: f.get("validFrom"),
                  validTo: f.get("validTo"),
                  sumInsured: Number(f.get("sumInsured")),
                  deductible: Number(f.get("deductible")),
                });
                await refresh();
                notify("Coverage registered");
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
            <Field label="Insurance plan">
              <select name="planId" required>
                {care.plans.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Policy number">
              <input name="policyNumber" required />
            </Field>
            <Field label="Member ID">
              <input name="memberId" required />
            </Field>
            <div className="form-grid">
              <Field label="Valid from">
                <input type="date" name="validFrom" required />
              </Field>
              <Field label="Valid to">
                <input type="date" name="validTo" required />
              </Field>
            </div>
            <div className="form-grid">
              <Field label="Sum insured">
                <input type="number" name="sumInsured" required />
              </Field>
              <Field label="Deductible">
                <input type="number" name="deductible" required />
              </Field>
            </div>
            <Submit label="Register coverage" />
          </form>
        </Modal>
      )}
      {settle && (
        <Modal title={`Settle ${settle.number}`} onClose={() => setSettle(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(
                  `/insurance/claims/${settle.id}/settle`,
                  "POST",
                  {
                    amount: Number(f.get("amount")),
                    mode: f.get("mode"),
                    reference: f.get("reference"),
                    version: settle.version,
                  },
                );
                await refresh();
                notify("Settlement recorded");
                setSettle(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <p className="muted">
              Approved ₹{settle.approvedAmount?.toLocaleString("en-IN")} · patient
              liability ₹{settle.patientLiability.toLocaleString("en-IN")}
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
            <Submit label="Record settlement" />
          </form>
        </Modal>
      )}
    </>
  );
}
