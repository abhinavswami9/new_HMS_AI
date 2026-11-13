import { useState } from "react";
import { Download, PackagePlus, Pill, Plus, ShieldCheck } from "lucide-react";
import { useApp } from "../store";
import { api, downloadCsv, money, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { ErrorNote, Field, Submit } from "../components";
import type { Dispense } from "../types";
export function Pharmacy() {
  const { data, user } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [receive, setReceive] = useState(false);
  const [dispense, setDispense] = useState(false);
  const [selected, setSelected] = useState<Dispense | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const medicine = (id: string) => data.medicines.find((m) => m.id === id);
  const today = new Date().toISOString().slice(0, 10);
  const soon = new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10);
  const batches = care.stockBatches.filter((b) =>
    `${medicine(b.medicineId)?.genericName} ${b.batchNumber} ${b.supplier}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const expiring = care.stockBatches.filter(
    (b) =>
      b.status === "Active" && b.expiryDate <= soon && b.expiryDate > today,
  ).length;
  return (
    <>
      <PageHeader
        eyebrow="PHARMACY"
        title="Pharmacy & stock"
        description="Batch-traceable stock, expiry visibility and prescription-linked dispensing."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-pharmacy-stock.csv", [
              ["Medicine", "Batch", "Expiry", "Quantity", "Supplier", "Status"],
              ...batches.map((b) => [
                medicine(b.medicineId)?.genericName || "",
                b.batchNumber,
                b.expiryDate,
                b.quantity,
                b.supplier,
                b.status,
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {can("pharmacy.write") && (
          <button className="btn" onClick={() => setReceive(true)}>
            <PackagePlus size={16} />
            Receive stock
          </button>
        )}
        {can("pharmacy.dispense") && (
          <button className="btn primary" onClick={() => setDispense(true)}>
            <Plus size={17} />
            Dispense
          </button>
        )}
      </PageHeader>
      <div className="finance-grid">
        {[
          [
            "Active batches",
            care.stockBatches.filter((b) => b.status === "Active").length,
            Pill,
            "teal",
          ],
          ["Expiring ≤ 30 days", expiring, ShieldCheck, "amber"],
          [
            "Units in stock",
            care.stockBatches.reduce((s, b) => s + b.quantity, 0),
            PackagePlus,
            "blue",
          ],
          ["Dispenses", care.dispenses.length, Pill, "purple"],
        ].map(([label, value, Icon, color]) => {
          const I = Icon as typeof Pill;
          return (
            <section className="card finance-card" key={String(label)}>
              <span className={`quick-icon ${color}`}>
                <I size={21} />
              </span>
              <p>{String(label)}</p>
              <strong>{String(value)}</strong>
              <small>Current branch view</small>
            </section>
          );
        })}
      </div>
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={setSearch}
            placeholder="Search medicine, batch or supplier…"
          />
          <span className="small muted">{batches.length} batches</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Batch</th>
                <th>Expiry</th>
                <th>Quantity</th>
                <th>Unit cost</th>
                <th>Supplier</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {batches.map((b) => (
                <tr key={b.id}>
                  <td>
                    <strong>
                      {medicine(b.medicineId)?.genericName}{" "}
                      {medicine(b.medicineId)?.strength}
                    </strong>
                    <small className="cell-small">
                      {medicine(b.medicineId)?.brandName}
                    </small>
                  </td>
                  <td>
                    {b.batchNumber}
                    <small className="cell-small">{b.reference}</small>
                  </td>
                  <td className={b.expiryDate <= soon ? "warning-text" : ""}>
                    {b.expiryDate}
                  </td>
                  <td>{b.quantity}</td>
                  <td>{money(b.unitCost)}</td>
                  <td>{b.supplier}</td>
                  <td>
                    <Badge status={b.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!batches.length && (
          <Empty
            title="No stock batches"
            description="Receive stock to begin dispensing."
          />
        )}
      </section>
      <section className="card">
        <div className="section-heading">
          <h2>Recent dispenses</h2>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Dispense</th>
                <th>Patient</th>
                <th>Setting</th>
                <th>Lines</th>
                <th>Created</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {care.dispenses.map((d) => (
                <tr key={d.id} tabIndex={0} onClick={() => setSelected(d)}>
                  <td>
                    <span className="invoice-link">{d.number}</span>
                  </td>
                  <td>
                    {data.patients.find((p) => p.id === d.patientId)?.fullName}
                  </td>
                  <td>
                    <Badge status={d.kind} />
                  </td>
                  <td>{d.lines.length}</td>
                  <td>{shortDate(d.createdAt)}</td>
                  <td>
                    <button
                      className="btn tiny"
                      onClick={(e) => {
                        e.stopPropagation();
                        setSelected(d);
                      }}
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      <div className="info-note standalone">
        <ShieldCheck size={18} />
        <span>
          Dispensing uses first-expiry-first-out batch allocation. Returns,
          purchase orders and supplier reconciliation require their approved
          workflows.
        </span>
      </div>
      {receive && <ReceiveStockModal onClose={() => setReceive(false)} />}
      {dispense && <DispenseModal onClose={() => setDispense(false)} />}
      {selected && (
        <Modal
          title={`Dispense ${selected.number}`}
          subtitle="Batch allocations are immutable."
          onClose={() => setSelected(null)}
          wide
        >
          <div className="modal-body">
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Medicine</th>
                    <th>Quantity</th>
                    <th>Batch allocations</th>
                  </tr>
                </thead>
                <tbody>
                  {selected.lines.map((l) => (
                    <tr key={l.medicineId}>
                      <td>{l.name}</td>
                      <td>{l.quantity}</td>
                      <td>
                        {l.batches
                          .map((b) => `${b.batchNumber} × ${b.quantity}`)
                          .join(", ")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          <div className="modal-footer">
            <button className="btn" onClick={() => window.print()}>
              Print
            </button>
            <button className="btn primary" onClick={() => setSelected(null)}>
              Close
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
function ReceiveStockModal({ onClose }: { onClose: () => void }) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    medicineId: data.medicines[0]?.id || "",
    batchNumber: "",
    expiryDate: "",
    quantity: "10",
    unitCost: "0",
    supplier: "",
    reference: "",
  });
  return (
    <Modal
      title="Receive stock batch"
      subtitle="Expired or duplicate batches are rejected. Receipt creates an append-only stock movement."
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/pharmacy/stock", "POST", {
              ...form,
              quantity: Number(form.quantity),
              unitCost: Number(form.unitCost),
            });
            await refresh();
            notify("Stock batch received");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <Field label="Medicine" required>
            <select
              required
              value={form.medicineId}
              onChange={(e) => setForm({ ...form, medicineId: e.target.value })}
            >
              {data.medicines.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.genericName} {m.strength}
                </option>
              ))}
            </select>
          </Field>
          <div className="form-grid">
            <Field label="Batch number" required>
              <input
                required
                value={form.batchNumber}
                onChange={(e) =>
                  setForm({ ...form, batchNumber: e.target.value })
                }
              />
            </Field>
            <Field label="Expiry date" required>
              <input
                required
                type="date"
                value={form.expiryDate}
                onChange={(e) =>
                  setForm({ ...form, expiryDate: e.target.value })
                }
              />
            </Field>
            <Field label="Quantity" required>
              <input
                required
                type="number"
                min="0.001"
                step="0.001"
                value={form.quantity}
                onChange={(e) => setForm({ ...form, quantity: e.target.value })}
              />
            </Field>
            <Field label="Unit cost" required>
              <input
                required
                type="number"
                min="0"
                step="0.01"
                value={form.unitCost}
                onChange={(e) => setForm({ ...form, unitCost: e.target.value })}
              />
            </Field>
            <Field label="Supplier" required>
              <input
                required
                value={form.supplier}
                onChange={(e) => setForm({ ...form, supplier: e.target.value })}
              />
            </Field>
            <Field label="GRN / reference" required>
              <input
                required
                value={form.reference}
                onChange={(e) =>
                  setForm({ ...form, reference: e.target.value })
                }
              />
            </Field>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Receive stock</Submit>
        </div>
      </form>
    </Modal>
  );
}
function DispenseModal({ onClose }: { onClose: () => void }) {
  const { data, refresh, notify } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [kind, setKind] = useState("OPD");
  const [patientId, setPatient] = useState("");
  const [encounterId, setEncounter] = useState("");
  const [admissionId, setAdmission] = useState("");
  const [lines, setLines] = useState([
    { medicineId: data.medicines[0]?.id || "", quantity: "1" },
  ]);
  const [key] = useState(() => crypto.randomUUID());
  const encounters = data.encounters.filter(
    (e) => e.status === "Signed" && e.patientId === patientId,
  );
  const admissions = data.care.admissions.filter(
    (a) => a.status === "Active" && a.patientId === patientId,
  );
  return (
    <Modal
      title="Dispense medicines"
      subtitle="OPD requires a signed prescription; IPD requires an active admission."
      onClose={onClose}
      wide
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api("/pharmacy/dispenses", "POST", {
              kind,
              patientId,
              encounterId: kind === "OPD" ? encounterId : null,
              admissionId: kind === "IPD" ? admissionId : null,
              lines: lines.map((l) => ({
                medicineId: l.medicineId,
                quantity: Number(l.quantity),
              })),
              idempotencyKey: key,
            });
            await refresh();
            notify("Medicines dispensed");
            onClose();
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="modal-body">
          <div className="form-grid">
            <Field label="Setting" required>
              <select value={kind} onChange={(e) => setKind(e.target.value)}>
                <option>OPD</option>
                <option>IPD</option>
              </select>
            </Field>
            <Field label="Patient" required>
              <select
                required
                value={patientId}
                onChange={(e) => {
                  setPatient(e.target.value);
                  setEncounter("");
                  setAdmission("");
                }}
              >
                <option value="">Select patient</option>
                {data.patients.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.fullName} · {p.uhid}
                  </option>
                ))}
              </select>
            </Field>
            {kind === "OPD" ? (
              <div className="span-2">
                <Field label="Signed prescription" required>
                  <select
                    required
                    value={encounterId}
                    onChange={(e) => setEncounter(e.target.value)}
                  >
                    <option value="">Select signed encounter</option>
                    {encounters.map((e) => (
                      <option key={e.id} value={e.id}>
                        {e.diagnosis || "Signed encounter"} ·{" "}
                        {shortDate(e.updatedAt)}
                      </option>
                    ))}
                  </select>
                </Field>
              </div>
            ) : (
              <div className="span-2">
                <Field label="Active admission" required>
                  <select
                    required
                    value={admissionId}
                    onChange={(e) => setAdmission(e.target.value)}
                  >
                    <option value="">Select admission</option>
                    {admissions.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.number}
                      </option>
                    ))}
                  </select>
                </Field>
              </div>
            )}
          </div>
          <div className="form-section-label">Medicine lines</div>
          {lines.map((l, i) => (
            <div className="dispense-line" key={i}>
              <select
                aria-label={`Medicine ${i + 1}`}
                value={l.medicineId}
                onChange={(e) =>
                  setLines(
                    lines.map((x, j) =>
                      j === i ? { ...x, medicineId: e.target.value } : x,
                    ),
                  )
                }
              >
                {data.medicines.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.genericName} {m.strength}
                  </option>
                ))}
              </select>
              <input
                aria-label={`Quantity ${i + 1}`}
                type="number"
                min="0.001"
                step="0.001"
                value={l.quantity}
                onChange={(e) =>
                  setLines(
                    lines.map((x, j) =>
                      j === i ? { ...x, quantity: e.target.value } : x,
                    ),
                  )
                }
              />
              <button
                type="button"
                className="btn tiny"
                onClick={() => setLines(lines.filter((_, j) => j !== i))}
                disabled={lines.length === 1}
              >
                Remove
              </button>
            </div>
          ))}
          <button
            type="button"
            className="btn tiny"
            onClick={() =>
              setLines([
                ...lines,
                { medicineId: data.medicines[0]?.id || "", quantity: "1" },
              ])
            }
          >
            Add medicine
          </button>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <Submit busy={busy}>Dispense medicines</Submit>
        </div>
      </form>
    </Modal>
  );
}
