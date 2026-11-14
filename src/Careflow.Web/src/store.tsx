import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";
import { api } from "./api";
import type { Principal, Workspace } from "./types";
type Context = {
  data: Workspace;
  truncated: boolean;
  user: Principal;
  demo: boolean;
  refresh: (as?: Principal | null) => Promise<void>;
  notify: (message: string, kind?: "success" | "error") => void;
  toast: { message: string; kind: string } | null;
  switchRole: (role: string) => Promise<void>;
  logout: () => Promise<void>;
};
const AppContext = createContext<Context | null>(null);
export function AppProvider({ children }: { children: ReactNode }) {
  const [data, setData] = useState<Workspace | null>(null);
  const [user, setUser] = useState<Principal | null>(null);
  const [demo, setDemo] = useState(false);
  const [truncated, setTruncated] = useState(false);
  const [error, setError] = useState("");
  const [toast, setToast] = useState<{ message: string; kind: string } | null>(
    null,
  );
  const notify = useCallback(
    (message: string, kind: "success" | "error" = "success") =>
      setToast({ message, kind }),
    [],
  );
  const emptyShell = (name: string, role: string): Workspace =>
    ({
      hospitalName: "Careflow Hospitals",
      branchName: "Central",
      userName: name,
      role,
      clock: new Date().toISOString(),
      metrics: [],
      recentPatients: [],
      patients: [],
      doctors: [],
      services: [],
      medicines: [],
      appointments: [],
      encounters: [],
      invoices: [],
      auditEvents: [],
      roles: [],
      users: [],
      activity: [],
      labs: [],
      labOrders: [],
      radiologyReports: [],
      prescriptions: [],
      dispenses: [],
      admissions: [],
      beds: [],
      wards: [],
      nurses: [],
      assessments: [],
      medicationAdministrations: [],
      handovers: [],
      surgicalCases: [],
      theatres: [],
      checklists: [],
      implants: [],
      hrStaff: [],
      shifts: [],
      attendances: [],
      leaves: [],
      payroll: [],
      payrollRuns: [],
      incentives: [],
      incentiveRuns: [],
      coverage: [],
      claims: [],
      telemedicine: [],
      diets: [],
      dietOrders: [],
      bloodUnits: [],
      bloodIssues: [],
      donors: [],
      cssdSets: [],
      cssdCycles: [],
      cssdIssues: [],
      infections: [],
      incidents: [],
      risks: [],
      capas: [],
      audits: [],
      qualityIndicators: [],
      indicatorResults: [],
      qualityDocuments: [],
      portalAccounts: [],
      proxyGrants: [],
      reportDefinitions: [],
      reportExecutions: [],
      modules: [],
      truncated: false,
    }) as unknown as Workspace;
  const refresh = useCallback(async (as?: Principal | null) => {
    const who = as ?? user;
    try {
      const result = await api<{ workspace: Workspace; truncated: boolean }>(
        "/workspace",
      );
      setData(result.workspace);
      setTruncated(result.truncated);
    } catch (e) {
      // Patient-facing portal principals have no staff workspace payload; give
      // the app a shell so the portal audience can render against portal APIs.
      if (who && !who.permissions.includes("patient.read")) {
        setData(emptyShell(who.name, who.role));
        setTruncated(false);
        return;
      }
      throw e;
    }
  }, [user]);
  const boot = useCallback(async () => {
    try {
      const config = await api<{ demo: boolean; principal: Principal | null }>(
        "/config",
      );
      setDemo(config.demo);
      const me =
        config.principal ||
        (config.demo ? await api<Principal>("/auth/demo", "POST", {}) : null);
      if (!me) {
        setUser({
          id: "login",
          name: "Sign in",
          role: "Login",
          permissions: [],
          hospitalId: "",
          branchId: "",
        });
        return;
      }
      setUser(me);
      await refresh(me);
      setError("");
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Unable to connect to the hospital API.",
      );
    }
  }, [refresh]);
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void boot();
  }, [boot]);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 5000);
    return () => clearTimeout(t);
  }, [toast]);
  const switchRole = async (role: string) => {
    if (!demo) return;
    try {
      const p = await api<Principal>("/auth/demo", "POST", { role });
      setUser(p);
      await refresh(p);
      notify(`Viewing as ${role}`);
    } catch (e) {
      notify((e as Error).message, "error");
    }
  };
  const logout = async () => {
    await api("/auth/logout", "POST", {});
    setData(null);
    setUser({
      id: "login",
      name: "Sign in",
      role: "Login",
      permissions: [],
      hospitalId: "",
      branchId: "",
    });
  };
  if (error)
    return (
      <div className="connection-screen">
        <div className="brand-mark">+</div>
        <h1>Let’s reconnect your workspace</h1>
        <p>{error}</p>
        <p className="muted">
          The API must be running on port 5080. No patient data is stored in
          this browser.
        </p>
        <button className="btn primary" onClick={() => void boot()}>
          Try again
        </button>
      </div>
    );
  if (user?.role === "Login")
    return (
      <Login
        demo={demo}
        onDemo={boot}
        onLogin={async (userName, password) => {
          await api("/auth/login", "POST", { userName, password });
          await boot();
        }}
      />
    );
  if (!data || !user)
    return (
      <div className="connection-screen">
        <div className="brand-mark pulse">+</div>
        <h2>Preparing your workspace</h2>
        <p className="muted">Connecting securely to Careflow…</p>
      </div>
    );
  return (
    <AppContext.Provider
      value={{
        data,
        truncated,
        user,
        demo,
        refresh,
        notify,
        toast,
        switchRole,
        logout,
      }}
    >
      {children}
    </AppContext.Provider>
  );
}
export function useApp() {
  const value = useContext(AppContext);
  if (!value) throw new Error("AppProvider is required");
  return value;
}
function Login({
  onLogin,
  demo,
  onDemo,
}: {
  onLogin: (u: string, p: string) => Promise<void>;
  demo: boolean;
  onDemo: () => Promise<void>;
}) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <div className="login-page">
      <form
        className="login-card"
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            await onLogin(username, password);
          } catch (err) {
            setError((err as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="brand">
          <div className="brand-mark">+</div>
          <span>
            careflow<span className="brand-dot">.</span>
          </span>
        </div>
        <h1>Welcome back</h1>
        <p className="muted">Sign in to your hospital workspace.</p>
        <label>
          Username
          <input
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        {error && (
          <p role="alert" className="error-text">
            {error}
          </p>
        )}
        <button className="btn primary full" disabled={busy}>
          {busy ? "Signing in…" : "Sign in securely"}
        </button>
        {demo && (
          <button
            type="button"
            className="btn full"
            onClick={() => void onDemo()}
          >
            Open synthetic preview
          </button>
        )}
        <p className="small muted">
          Access is restricted to authorized hospital staff.
        </p>
      </form>
    </div>
  );
}
