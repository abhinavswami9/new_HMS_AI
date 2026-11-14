import { useEffect, useState } from "react";
import { BarChart3, FileDown, Play } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate } from "../api";
import { Badge, Empty, Modal, PageHeader } from "../components";
import { Field, Submit } from "../components";
import type { ReportDefinition } from "../types";
type Card = { label: string; value: string; detail: string; freshness: string; provisional: boolean };
export function Reports() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [view, setView] = useState("management");
  const [cards, setCards] = useState<Card[]>([]);
  const [define, setDefine] = useState(false);
  const [execute, setExecute] = useState<ReportDefinition | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  useEffect(() => {
    void api(`/reports/dashboards/${view}`)
      .then((d) => setCards((d as { cards: Card[] }).cards))
      .catch(() => setCards([]));
  }, [view, data]);
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
        eyebrow="MANAGEMENT REPORTING"
        title="Dashboards & governed reports"
        description="Metric cards and report definitions over governed data — definitions select approved queries, never arbitrary SQL. Exports inherit your access, carry a watermark and are audited."
      >
        {can("report.define") && (
          <button className="btn primary" onClick={() => setDefine(true)}>
            <BarChart3 size={16} /> New definition
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <select value={view} onChange={(e) => setView(e.target.value)}>
          <option value="management">Management dashboard</option>
          <option value="clinical">Clinical dashboard</option>
          <option value="financial">Financial dashboard</option>
        </select>
      </div>
      <div className="metric-grid">
        {cards.map((c) => (
          <div key={c.label} className="metric-card">
            <small>{c.label}</small>
            <strong>{c.value}</strong>
            <span>{c.detail}</span>
            {c.provisional && <Badge tone="warning">Provisional</Badge>}
          </div>
        ))}
      </div>
      {cards.length > 0 && (
        <p className="muted">
          Freshness: {cards[0]?.freshness} · provisional values refresh with each
          request and may differ from month-close figures.
        </p>
      )}
      <div className="split-2">
        <div className="panel">
          <h3>Report definitions</h3>
          {care.reportDefinitions.length === 0 && (
            <Empty title="No report definitions" />
          )}
          {care.reportDefinitions.map((d: ReportDefinition) => (
            <div key={d.id} className="list-row">
              <div>
                <strong>
                  {d.code} · {d.name}
                </strong>
                <small>
                  {d.category} · owner {d.owner} · v{d.currentVersion}
                </small>
              </div>
              <div>
                <Badge>{d.status}</Badge>
                {can("report.define") && d.status === "Draft" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/reports/definitions/${d.id}/approve`,
                        { version: d.version, reason: "Reviewed against the metric dictionary." },
                        "Definition approved",
                      )
                    }
                  >
                    Approve
                  </button>
                )}
                {can("report.execute") && d.status === "Active" && (
                  <button className="btn ghost" onClick={() => setExecute(d)}>
                    <Play size={14} /> Run
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
        <div className="panel">
          <h3>Execution history</h3>
          {care.reportExecutions.length === 0 && (
            <Empty title="No report executions yet" />
          )}
          {care.reportExecutions.map((x) => (
            <div key={x.id} className="list-row">
              <div>
                <strong>{x.number}</strong>
                <small>
                  {x.rowCount} rows · {x.executedBy} · {shortDate(x.executedAt)} ·{" "}
                  {x.parameters}
                </small>
              </div>
              <div>
                <Badge>{x.status}</Badge>
                {can("report.export") && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        "/reports/exports",
                        { executionId: x.id, format: "PDF" },
                        "Watermarked export recorded",
                      )
                    }
                  >
                    <FileDown size={14} /> Export
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      {define && (
        <Modal
          title="New report definition"
          onClose={() => setDefine(false)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/reports/definitions", "POST", {
                  code: f.get("code"),
                  name: f.get("name"),
                  category: f.get("category"),
                  owner: user.name,
                  queryKey: f.get("queryKey"),
                  columns: f.get("columns"),
                  filters: f.get("filters"),
                });
                await refresh();
                notify("Definition drafted");
                setDefine(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <div className="form-grid">
              <Field label="Code">
                <input name="code" required />
              </Field>
              <Field label="Category">
                <select name="category">
                  <option>Management</option>
                  <option>Clinical</option>
                  <option>Financial</option>
                  <option>Quality</option>
                </select>
              </Field>
            </div>
            <Field label="Name">
              <input name="name" required />
            </Field>
            <Field label="Governed query">
              <select name="queryKey">
                <option value="census_summary">Census summary</option>
                <option value="revenue_by_day">Revenue by day</option>
                <option value="opd_volume_by_doctor">OPD volume by practitioner</option>
                <option value="claims_approval_rate">Claims approval rate</option>
                <option value="quality_incident_trend">Quality incident trend</option>
                <option value="capa_status">CAPA status</option>
              </select>
            </Field>
            <Field label="Columns">
              <input name="columns" placeholder="uhid,patient,encounters" required />
            </Field>
            <Field label="Filters">
              <input name="filters" placeholder="from,to" />
            </Field>
            <p className="muted">
              Definitions select governed queries only — arbitrary SQL is rejected
              server-side. A second person approves definitions.
            </p>
            <Submit label="Create draft" />
          </form>
        </Modal>
      )}
      {execute && (
        <Modal title={`Run ${execute.code}`} onClose={() => setExecute(null)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/reports/execute", "POST", {
                  definitionId: execute.id,
                  parameters: JSON.stringify({
                    from: f.get("from") || undefined,
                    to: f.get("to") || undefined,
                  }),
                });
                await refresh();
                notify("Report executed");
                setExecute(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <div className="form-grid">
              <Field label="From (optional)">
                <input type="date" name="from" />
              </Field>
              <Field label="To (optional)">
                <input type="date" name="to" />
              </Field>
            </div>
            <Submit label="Execute report" />
          </form>
        </Modal>
      )}
    </>
  );
}
