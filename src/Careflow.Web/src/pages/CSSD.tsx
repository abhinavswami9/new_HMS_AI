import { useState } from "react";
import { FlaskConical, RotateCcw } from "lucide-react";
import { useApp } from "../store";
import { api, shortDate, time } from "../api";
import { Badge, Empty, Modal, PageHeader, SearchBox } from "../components";
import { Field, Submit } from "../components";
import type { InstrumentSet, SterilizerCycle } from "../types";
export function CSSD() {
  const { data, user, refresh, notify } = useApp();
  const care = data.care;
  const [search, setSearch] = useState("");
  const [cycle, setCycle] = useState(false);
  const [indicators, setIndicators] = useState<SterilizerCycle | null>(null);
  const can = (p: string) => user.permissions.includes(p);
  const filtered = care.cssdSets.filter((s) =>
    `${s.code} ${s.name}`.toLowerCase().includes(search.toLowerCase()),
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
        eyebrow="CSSD"
        title="Sterile supply chain"
        description="Instrument sets from usage return through decontamination, packing, validated sterilizer cycles with indicator results, sterile-stock release and issue to theatre — with recall traceability."
      >
        {can("cssd.cycle") && (
          <button className="btn primary" onClick={() => setCycle(true)}>
            <FlaskConical size={16} /> Start cycle
          </button>
        )}
      </PageHeader>
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} />
      </div>
      <div className="split-2">
        <div className="panel">
          <h3>Instrument sets</h3>
          {filtered.length === 0 && <Empty title="No instrument sets registered" />}
          {filtered.map((s: InstrumentSet) => (
            <div key={s.id} className="list-row">
              <div>
                <strong>
                  {s.code} · {s.name}
                </strong>
                <small>
                  {s.missingCount > 0
                    ? `${s.missingCount} missing instruments`
                    : "Complete"}
                  {s.sterileExpiry ? ` · sterile until ${shortDate(s.sterileExpiry)}` : ""}
                </small>
              </div>
              <div>
                <Badge>{s.status}</Badge>
                {can("cssd.process") && s.status === "Dirty" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/cssd/sets/${s.id}/clean-pack`,
                        { version: s.version, reason: "Decontaminated and packed." },
                        "Set cleaned and packed",
                      )
                    }
                  >
                    Clean & pack
                  </button>
                )}
                {can("cssd.issue") && s.status === "Sterile" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        "/cssd/issues",
                        { setId: s.id, bookingId: null, issuedTo: "Main theatre" },
                        "Set issued to theatre",
                      )
                    }
                  >
                    Issue
                  </button>
                )}
                {can("cssd.process") && s.status === "Issued" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/cssd/sets/${s.id}/return`,
                        { returnCondition: "Used", version: s.version },
                        "Usage return recorded",
                      )
                    }
                  >
                    <RotateCcw size={14} /> Return
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
        <div className="panel">
          <h3>Sterilizer cycles</h3>
          {care.cssdCycles.length === 0 && <Empty title="No cycles run" />}
          {care.cssdCycles.map((c) => (
            <div key={c.id} className="list-row">
              <div>
                <strong>{c.number}</strong>
                <small>
                  {c.method} · {c.machine} · {c.tempC}°C · {c.durationMin} min ·{" "}
                  {time(c.startedAt)}
                </small>
              </div>
              <div>
                <Badge>{c.status}</Badge>
                {can("cssd.process") && c.status === "In progress" && (
                  <button
                    className="btn ghost"
                    onClick={() => setIndicators(c)}
                  >
                    Indicators
                  </button>
                )}
                {can("cssd.release") && c.status === "In progress" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        `/cssd/cycles/${c.id}/release`,
                        {
                          version: c.version,
                          reason: "Cycle parameters within validated band.",
                        },
                        "Cycle released to sterile stock",
                      )
                    }
                  >
                    Release
                  </button>
                )}
                {can("cssd.recall") && c.status === "Passed" && (
                  <button
                    className="btn ghost"
                    onClick={() =>
                      void act(
                        "/cssd/recalls",
                        {
                          cycleId: c.id,
                          reason: "Post-release concerns raised during review.",
                        },
                        "Recall opened",
                      )
                    }
                  >
                    Recall
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="panel">
        <h3>Issues to theatre</h3>
        {care.cssdIssues.length === 0 && <Empty title="No sets issued" />}
        {care.cssdIssues.map((x) => (
          <div key={x.id} className="list-row">
            <div>
              <strong>{x.number}</strong>
              <small>
                {x.issuedTo} · issued {time(x.issuedAt)}
                {x.returnCondition ? ` · returned ${x.returnCondition}` : ""}
              </small>
            </div>
            <div>
              <Badge>{x.status}</Badge>
            </div>
          </div>
        ))}
      </div>
      {cycle && (
        <Modal title="Start sterilizer cycle" onClose={() => setCycle(false)}>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api("/cssd/cycles", "POST", {
                  method: f.get("method"),
                  machine: f.get("machine"),
                  tempC: Number(f.get("tempC")),
                  durationMin: Number(f.get("durationMin")),
                  setIds: care.cssdSets
                    .filter((s) => s.status === "Packed")
                    .map((s) => s.id),
                });
                await refresh();
                notify("Cycle started");
                setCycle(false);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <Field label="Method">
              <select name="method">
                <option>Steam</option>
                <option>EO</option>
                <option>Plasma</option>
              </select>
            </Field>
            <Field label="Machine">
              <input name="machine" required />
            </Field>
            <div className="form-grid">
              <Field label="Temperature (°C)">
                <input type="number" name="tempC" required />
              </Field>
              <Field label="Duration (min)">
                <input type="number" name="durationMin" required />
              </Field>
            </div>
            <p className="muted">
              All packed sets are loaded into this cycle.
            </p>
            <Submit label="Start cycle" />
          </form>
        </Modal>
      )}
      {indicators && (
        <Modal
          title={`Indicator results · ${indicators.number}`}
          onClose={() => setIndicators(null)}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await api(
                  `/cssd/cycles/${indicators.id}/indicators`,
                  "POST",
                  {
                    kind: f.get("kind"),
                    result: f.get("result"),
                    note: f.get("note"),
                  },
                );
                await refresh();
                notify("Indicator recorded");
                setIndicators(null);
              } catch (err) {
                notify((err as Error).message, "error");
              }
            }}
          >
            <p className="muted">
              Release needs at least one passing chemical indicator and no failed
              indicator; biological indicators cannot be waived.
            </p>
            <Field label="Kind">
              <select name="kind">
                <option>Chemical</option>
                <option>Biological</option>
                <option>Physical</option>
              </select>
            </Field>
            <Field label="Result">
              <select name="result">
                <option>Pass</option>
                <option>Fail</option>
              </select>
            </Field>
            <Field label="Note">
              <input name="note" />
            </Field>
            <Submit label="Record indicator" />
          </form>
        </Modal>
      )}
    </>
  );
}
