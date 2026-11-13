import { useState } from "react";
import {
  ArrowRight,
  Check,
  CreditCard,
  Download,
  FileText,
  IndianRupee,
  Plus,
  Printer,
  Receipt,
  ShieldCheck,
  Wallet,
} from "lucide-react";
import { useApp } from "../store";
import {
  Avatar,
  Badge,
  Empty,
  Modal,
  PageHeader,
  Pagination,
  SearchBox,
} from "../components";
import { InvoiceModal, PaymentModal } from "../forms";
import { api, downloadCsv, money, shortDate, time } from "../api";
export function Billing() {
  const { data, user, refresh, notify } = useApp();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All invoices");
  const [page, setPage] = useState(1);
  const [create, setCreate] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [payId, setPayId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const filtered = data.invoices.filter(
    (i) =>
      `${i.number} ${data.patients.find((p) => p.id === i.patientId)?.fullName}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (status === "All invoices" || i.status === status),
  );
  const size = 8;
  const current = Math.min(
    page,
    Math.max(1, Math.ceil(filtered.length / size)),
  );
  const selected = data.invoices.find((i) => i.id === selectedId);
  const paying = data.invoices.find((i) => i.id === payId);
  const total = data.invoices
    .filter((i) => i.status !== "Draft")
    .reduce((s, i) => s + i.total, 0);
  const collected = data.invoices.reduce((s, i) => s + i.paid, 0);
  const outstanding = data.invoices
    .filter((i) => ["Issued", "Part paid"].includes(i.status))
    .reduce((s, i) => s + i.balance, 0);
  return (
    <>
      <PageHeader
        eyebrow="REVENUE CYCLE"
        title="Billing & payments"
        description="Transparent charges. Timely collections. Every rupee accounted for."
      >
        <button
          className="btn"
          onClick={() =>
            downloadCsv("careflow-invoices.csv", [
              [
                "Invoice",
                "Patient",
                "Status",
                "Total INR",
                "Paid INR",
                "Balance INR",
              ],
              ...filtered.map((i) => [
                i.number,
                data.patients.find((p) => p.id === i.patientId)?.fullName || "",
                i.status,
                i.total,
                i.paid,
                i.balance,
              ]),
            ])
          }
        >
          <Download size={16} />
          Export
        </button>
        {user.permissions.includes("billing.write") && (
          <button className="btn primary" onClick={() => setCreate(true)}>
            <Plus size={17} />
            Create invoice
          </button>
        )}
      </PageHeader>
      <div className="finance-grid">
        {[
          [
            "Total invoiced",
            total,
            FileText,
            "teal",
            "Issued invoices in this view",
          ],
          [
            "Payments recorded",
            collected,
            Wallet,
            "blue",
            "Receipts in the recent ledger",
          ],
          [
            "Outstanding balance",
            outstanding,
            IndianRupee,
            "amber",
            "Awaiting collection",
          ],
          [
            "Draft invoices",
            data.invoices.filter((i) => i.status === "Draft").length,
            Receipt,
            "purple",
            "Review before issuing",
          ],
        ].map(([label, value, Icon, color, note]) => {
          const I = Icon as typeof FileText;
          return (
            <section className="card finance-card" key={String(label)}>
              <span className={`quick-icon ${color}`}>
                <I size={21} />
              </span>
              <p>{String(label)}</p>
              <strong>
                {label === "Draft invoices"
                  ? (value as number)
                  : money(value as number)}
              </strong>
              <small>{String(note)}</small>
            </section>
          );
        })}
      </div>
      <section className="card">
        <div className="list-toolbar">
          <SearchBox
            value={search}
            onChange={(s) => {
              setSearch(s);
              setPage(1);
            }}
            placeholder="Search invoice number or patient…"
          />
          <select
            aria-label="Invoice status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            {["All invoices", "Draft", "Issued", "Part paid", "Paid"].map(
              (x) => (
                <option key={x}>{x}</option>
              ),
            )}
          </select>
          <span className="small muted">{filtered.length} invoices</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Patient</th>
                <th>Created</th>
                <th>Amount</th>
                <th>Balance</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.slice((current - 1) * size, current * size).map((i) => {
                const p = data.patients.find((p) => p.id === i.patientId);
                return (
                  <tr
                    key={i.id}
                    tabIndex={0}
                    onClick={() => setSelectedId(i.id)}
                    onKeyDown={(e) => e.key === "Enter" && setSelectedId(i.id)}
                  >
                    <td>
                      <span className="invoice-link">{i.number}</span>
                      <small className="cell-small">
                        {i.lines.length} service{i.lines.length > 1 ? "s" : ""}
                      </small>
                    </td>
                    <td>
                      <div className="person-cell">
                        <Avatar name={p?.fullName || "Patient"} />
                        <div>
                          <strong>{p?.fullName}</strong>
                          <small>{p?.uhid}</small>
                        </div>
                      </div>
                    </td>
                    <td>{shortDate(i.createdAt)}</td>
                    <td>
                      <strong>{money(i.total)}</strong>
                    </td>
                    <td
                      className={
                        i.balance > 0 && i.status !== "Draft"
                          ? "warning-text"
                          : "muted"
                      }
                    >
                      {money(i.balance)}
                    </td>
                    <td>
                      <Badge status={i.status} />
                    </td>
                    <td>
                      <ArrowRight size={16} className="muted" />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <Empty
            title="No invoices in this view"
            description="Create an invoice or adjust the filters."
          />
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
          Issued invoices are immutable. This slice records offline payments;
          refunds, discounts, insurance and payment-gateway collection require
          their approved workflows.
        </span>
      </div>
      {create && <InvoiceModal onClose={() => setCreate(false)} />}
      {selected && !paying && (
        <Modal
          title="Invoice details"
          subtitle={`${selected.number} · ${shortDate(selected.createdAt)}`}
          onClose={() => setSelectedId(null)}
          wide
        >
          <div className="modal-body print-area">
            <div className="print-brand">
              careflow<span>Nova Care Hospital</span>
            </div>
            <div className="invoice-detail-top">
              <div>
                <span className="muted small">BILL TO</span>
                <h3>
                  {
                    data.patients.find((p) => p.id === selected.patientId)
                      ?.fullName
                  }
                </h3>
                <p className="muted small">
                  {data.patients.find((p) => p.id === selected.patientId)?.uhid}
                </p>
              </div>
              <div>
                <Badge status={selected.status} />
                <p className="muted small">
                  {selected.issuedAt
                    ? `Issued ${shortDate(selected.issuedAt)}`
                    : "Not yet issued"}
                </p>
              </div>
            </div>
            <table className="invoice-table">
              <thead>
                <tr>
                  <th>Service</th>
                  <th>Qty</th>
                  <th>Unit price</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                {selected.lines.map((l, i) => (
                  <tr key={i}>
                    <td>{l.name}</td>
                    <td>{l.quantity}</td>
                    <td>{money(l.unitPrice)}</td>
                    <td>{money(l.quantity * l.unitPrice)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="invoice-totals">
              <div>
                <span>Subtotal</span>
                <strong>{money(selected.subtotal)}</strong>
              </div>
              <div>
                <span>Tax</span>
                <strong>{money(selected.tax)}</strong>
              </div>
              <div className="grand-total">
                <span>Total</span>
                <strong>{money(selected.total)}</strong>
              </div>
              <div>
                <span>Paid</span>
                <strong>{money(selected.paid)}</strong>
              </div>
              <div>
                <span>Balance due</span>
                <strong>{money(selected.balance)}</strong>
              </div>
            </div>
            {selected.payments.length > 0 && (
              <div className="receipt-history">
                <h3>Payment history</h3>
                {selected.payments.map((p) => (
                  <div key={p.id}>
                    <span className="quick-icon green">
                      <Check size={16} />
                    </span>
                    <div>
                      <strong>
                        {p.method} · {p.reference}
                      </strong>
                      <small>
                        {shortDate(p.receivedAt)} at {time(p.receivedAt)}
                      </small>
                    </div>
                    <strong>{money(p.amount)}</strong>
                  </div>
                ))}
              </div>
            )}
            <p className="small muted print-disclaimer">
              Synthetic development record. Not a valid tax invoice or payment
              receipt.
            </p>
          </div>
          <div className="modal-footer">
            <button className="btn" onClick={() => window.print()}>
              <Printer size={16} />
              Print
            </button>
            <div>
              {selected.status === "Draft" &&
                user.permissions.includes("billing.write") && (
                  <button
                    className="btn primary"
                    disabled={busy}
                    onClick={async () => {
                      setBusy(true);
                      try {
                        await api(`/invoices/${selected.id}/issue`, "POST", {
                          version: selected.version,
                        });
                        await refresh();
                        notify("Invoice issued and locked");
                      } catch (e) {
                        notify((e as Error).message, "error");
                      } finally {
                        setBusy(false);
                      }
                    }}
                  >
                    <FileText size={16} />
                    Issue invoice
                  </button>
                )}
              {["Issued", "Part paid"].includes(selected.status) &&
                user.permissions.includes("billing.collect") && (
                  <button
                    className="btn primary"
                    onClick={() => setPayId(selected.id)}
                  >
                    <CreditCard size={16} />
                    Record payment
                  </button>
                )}
              {selected.status === "Paid" && (
                <span className="settled-label">
                  <Check size={17} />
                  Fully settled
                </span>
              )}
            </div>
          </div>
        </Modal>
      )}
      {paying && (
        <PaymentModal invoice={paying} onClose={() => setPayId(null)} />
      )}
    </>
  );
}
