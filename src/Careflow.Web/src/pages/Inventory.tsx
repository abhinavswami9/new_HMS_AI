import { useState } from "react";
import { PackagePlus, Truck } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { InventoryIndent, PurchaseOrder } from "../types";
export function Inventory() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [indent, setIndent] = useState(false);
  const [grn, setGrn] = useState<PurchaseOrder | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const item = (id: string) => care.inventoryItems.find((i) => i.id === id);
  const store = (id: string) => care.inventoryStores.find((s) => s.id === id);
  const vendor = (id: string) => care.vendors.find((v) => v.id === id);
  const filtered = care.inventoryBatches.filter((b) =>
    `${b.batchNumber} ${item(b.itemId)?.name ?? ""}`
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
        eyebrow="CENTRAL STORE & INVENTORY"
        title="Purchase to consumption"
        description="Item master with reorder levels, indents with separation of duties, authorized-vendor purchase orders, goods receipt, inter-store transfers and department consumption."
      >
        {can("inventory.indent") && (
          <button className="btn primary" onClick={() => setIndent(true)}>
            <PackagePlus size={16} /> Raise indent
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
      </div>
      <div className="split-2">
        <div className="panel">
          <h3>Indents</h3>
          {care.inventoryIndents.length === 0 && <Empty title="No indents raised" />}
          {care.inventoryIndents.map((x: InventoryIndent) => (
            <div key={x.id} className="list-row">
              <div>
                <strong>{x.number}</strong>
                <small>
                  {item(x.itemId)?.name} · {x.quantity} {item(x.itemId)?.uom} ·{" "}
                  {x.priority}
                </small>
              </div>
              <div>
                <Badge>{x.status}</Badge>
                {can("inventory.approve") && x.status === "Pending" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/inventory/indents/${x.id}/decision`,
                        {
                          approve: true,
                          note: "Approved against annual rate contract.",
                          version: x.version,
                        },
                        "Indent approved",
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
          <h3>Purchase orders</h3>
          {care.purchaseOrders.length === 0 && (
            <Empty title="No purchase orders issued" />
          )}
          {care.purchaseOrders.map((o: PurchaseOrder) => (
            <div key={o.id} className="list-row">
              <div>
                <strong>{o.number}</strong>
                <small>
                  {vendor(o.vendorId)?.name} · {item(o.itemId)?.name} ·{" "}
                  {o.quantity} × ₹{o.unitCost}
                </small>
              </div>
              <div>
                <Badge>{o.status}</Badge>
                {can("inventory.receive") && o.status === "Sent" && (
                  <button className="btn ghost" onClick={() => setGrn(o)}>
                    <Truck size={14} /> Receive
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="panel">
        <h3>Stock batches</h3>
        <table>
          <thead>
            <tr>
              <th>Batch</th>
              <th>Item</th>
              <th>Store</th>
              <th>Qty</th>
              <th>Expiry</th>
              <th>Unit cost</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((b) => (
              <tr key={b.id}>
                <td>
                  <strong>{b.batchNumber}</strong>
                </td>
                <td>{item(b.itemId)?.name}</td>
                <td>{store(b.storeId)?.name}</td>
                <td>{b.quantity}</td>
                <td>{b.expiryDate ? shortDate(b.expiryDate) : "—"}</td>
                <td>₹{b.unitCost.toLocaleString("en-IN")}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && <Empty title="No stock batches" />}
      </div>
      <div className="panel">
        <h3>Transfers</h3>
        {care.transfers.length === 0 && <Empty title="No transfers dispatched" />}
        {care.transfers.map((t) => (
          <div key={t.id} className="list-row">
            <div>
              <strong>{t.number}</strong>
              <small>
                {store(t.fromStoreId)?.name} → {store(t.toStoreId)?.name} ·{" "}
                {item(t.itemId)?.name} · qty {t.quantity}
              </small>
            </div>
            <div>
              <Badge>{t.status}</Badge>
              {can("inventory.transfer") && t.status === "Dispatched" && (
                <button
                  className="btn ghost"
                  onClick={() =>
                    void act(
                      `/inventory/transfers/${t.id}/receive`,
                      { version: t.version },
                      "Transfer received",
                    )
                  }
                >
                  Receive
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
      {indent && (
        <Modal title="Raise stock indent" onClose={() => setIndent(false)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/inventory/indents", "POST", {
                  storeId: f.get("storeId"),
                  itemId: f.get("itemId"),
                  quantity: Number(f.get("quantity")),
                  priority: f.get("priority"),
                  reason: f.get("reason"),
                });
                await refresh();
                notify("Indent raised");
                setIndent(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <Field label="Store">
              <select name="storeId" required>
                {care.inventoryStores.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Item">
              <select name="itemId" required>
                {care.inventoryItems.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.code} · {i.name}
                  </option>
                ))}
              </select>
            </Field>
            <div className="form-grid">
              <Field label="Quantity">
                <input type="number" name="quantity" required />
              </Field>
              <Field label="Priority">
                <select name="priority">
                  <option>Routine</option>
                  <option>Urgent</option>
                  <option>Emergency</option>
                </select>
              </Field>
            </div>
            <Field label="Reason">
              <input name="reason" required />
            </Field>
            <Submit label="Raise indent" />
          </form>
        </Modal>
      )}
      {grn && (
        <Modal title={`Goods receipt · ${grn.number}`} onClose={() => setGrn(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(
                  `/inventory/purchase-orders/${grn.id}/goods-receipt`,
                  "POST",
                  {
                    receivedQty: Number(f.get("receivedQty")),
                    acceptedQty: Number(f.get("acceptedQty")),
                    rejectedQty: Number(f.get("rejectedQty")),
                    inspectionNote: f.get("inspectionNote"),
                    batchNumber: f.get("batchNumber"),
                    expiryDate: f.get("expiryDate"),
                    version: grn.version,
                  },
                );
                await refresh();
                notify("Goods receipt recorded");
                setGrn(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <div className="form-grid">
              <Field label="Received qty">
                <input type="number" name="receivedQty" required />
              </Field>
              <Field label="Accepted qty">
                <input type="number" name="acceptedQty" required />
              </Field>
              <Field label="Rejected qty">
                <input type="number" name="rejectedQty" required />
              </Field>
            </div>
            <Field label="Batch number">
              <input name="batchNumber" required />
            </Field>
            <Field label="Expiry date">
              <input type="date" name="expiryDate" required />
            </Field>
            <Field label="Inspection note">
              <input name="inspectionNote" />
            </Field>
            <Submit label="Record receipt" />
          </form>
        </Modal>
      )}
    </>
  );
}
