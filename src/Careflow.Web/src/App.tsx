import { useEffect, useState } from "react";
import {
  BrowserRouter,
  NavLink,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  Activity,
  ArrowRight,
  Bell,
  Building2,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronRight,
  CircleHelp,
  ClipboardList,
  Command,
  FlaskConical,
  HeartPulse,
  LayoutDashboard,
  Layers3,
  LogOut,
  Menu,
  Moon,
  MoreHorizontal,
  Pill,
  Plus,
  Receipt,
  ScanLine,
  Scissors,
  Search,
  Settings2,
  ShieldCheck,
  Siren,
  Stethoscope,
  Sun,
  Syringe,
  Users,
  Calculator,
  FileCheck2,
  Package,
  Video,
} from "lucide-react";
import { AppProvider, useApp } from "./store";
import { Avatar, Empty, Modal } from "./components";
import { Overview } from "./pages/Overview";
import { Patients, PatientDetail } from "./pages/Patients";
import { Appointments } from "./pages/Appointments";
import { Consultations, ConsultationDetail } from "./pages/Consultations";
import { Billing } from "./pages/Billing";
import { Administration, Modules } from "./pages/Administration";
import { Inpatient } from "./pages/Inpatient";
import { Nursing } from "./pages/Nursing";
import { Pharmacy } from "./pages/Pharmacy";
import { Laboratory } from "./pages/Laboratory";
import { Radiology } from "./pages/Radiology";
import { Emergency } from "./pages/Emergency";
import { OperationTheatre } from "./pages/OperationTheatre";
import { BloodBank } from "./pages/BloodBank";
import { Insurance } from "./pages/Insurance";
import { Incentives } from "./pages/Incentives";
import { Telemedicine } from "./pages/Telemedicine";
import { Inventory } from "./pages/Inventory";
import { CSSD } from "./pages/CSSD";
const mainNav = [
  { to: "/", label: "Overview", icon: LayoutDashboard },
  { to: "/patients", label: "Patients", icon: Users },
  { to: "/appointments", label: "Appointments", icon: CalendarDays },
  { to: "/consultations", label: "Consultations", icon: Stethoscope },
  { to: "/billing", label: "Billing", icon: Receipt },
];
const careNav = [
  {
    to: "/inpatient",
    label: "Inpatient",
    icon: HeartPulse,
    module: "ipd",
    permission: "ipd.read",
  },
  {
    to: "/nursing",
    label: "Nursing",
    icon: ClipboardList,
    module: "nursing",
    permission: "nursing.read",
  },
  {
    to: "/pharmacy",
    label: "Pharmacy",
    icon: Pill,
    module: "pharmacy",
    permission: "pharmacy.read",
  },
  {
    to: "/laboratory",
    label: "Laboratory",
    icon: FlaskConical,
    module: "laboratory",
    permission: "lab.read",
  },
  {
    to: "/radiology",
    label: "Radiology",
    icon: ScanLine,
    module: "radiology",
    permission: "radiology.read",
  },
  {
    to: "/emergency",
    label: "Emergency",
    icon: Siren,
    module: "emergency",
    permission: "emergency.read",
  },
  {
    to: "/theatre",
    label: "Operation Theatre",
    icon: Scissors,
    module: "ot",
    permission: "ot.read",
  },
  {
    to: "/blood-bank",
    label: "Blood Bank",
    icon: Syringe,
    module: "blood",
    permission: "blood.read",
  },
  {
    to: "/insurance",
    label: "Insurance & TPA",
    icon: FileCheck2,
    module: "insurance",
    permission: "insurance.read",
  },
  {
    to: "/incentives",
    label: "Incentives",
    icon: Calculator,
    module: "incentives",
    permission: "incentive.read",
  },
  {
    to: "/telemedicine",
    label: "Telemedicine",
    icon: Video,
    module: "telemedicine",
    permission: "tele.read",
  },
  {
    to: "/inventory",
    label: "Inventory",
    icon: Package,
    module: "inventory",
    permission: "inventory.read",
  },
  {
    to: "/cssd",
    label: "CSSD",
    icon: Layers3,
    module: "cssd",
    permission: "cssd.read",
  },
];
export default function App() {
  return (
    <BrowserRouter>
      <AppProvider>
        <Shell />
      </AppProvider>
    </BrowserRouter>
  );
}
function CareRoute({
  module,
  permission,
  children,
}: {
  module: string;
  permission: string;
  children: React.ReactNode;
}) {
  const { data, user } = useApp();
  const enabled = data.modules.some((m) => m.key === module && m.enabled);
  if (!enabled || !user.permissions.includes(permission))
    return (
      <Empty
        title="Module unavailable"
        description="This module is disabled for your branch or your role does not have access."
      />
    );
  return <>{children}</>;
}
function Shell() {
  const { user, data, demo, toast, switchRole, logout } = useApp();
  const location = useLocation();
  const navigate = useNavigate();
  const [mobile, setMobile] = useState(false);
  const [search, setSearch] = useState(false);
  const [query, setQuery] = useState("");
  const [popover, setPopover] = useState<
    "notifications" | "hospital" | "profile" | null
  >(null);
  const [dark, setDark] = useState(
    localStorage.getItem("careflow-theme") === "dark",
  );
  useEffect(() => {
    document.documentElement.dataset.theme = dark ? "dark" : "light";
    localStorage.setItem("careflow-theme", dark ? "dark" : "light");
  }, [dark]);
  useEffect(() => {
    setMobile(false);
    setPopover(null);
    window.scrollTo(0, 0);
  }, [location.pathname]);
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        setSearch((s) => !s);
      }
      if (e.key === "Escape") {
        setPopover(null);
        setMobile(false);
      }
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, []);
  const page =
    mainNav.find((n) => n.to === location.pathname)?.label ||
    (location.pathname.startsWith("/patients/")
      ? "Patient chart"
      : location.pathname.startsWith("/consultations/")
        ? "Clinical record"
        : careNav.find((n) => n.to === location.pathname)?.label
          ? careNav.find((n) => n.to === location.pathname)!.label
          : location.pathname === "/modules"
            ? "Modules"
            : "Administration");
  const visibleCareNav = careNav.filter(
    (n) =>
      data.modules.some((m) => m.key === n.module && m.enabled) &&
      user.permissions.includes(n.permission),
  );
  const visibleNav = mainNav.filter(
    (n) =>
      (n.to !== "/billing" || user.permissions.includes("billing.read")) &&
      (n.to !== "/consultations" || user.permissions.includes("clinical.read")),
  );
  const results = data.patients
    .filter((p) =>
      `${p.fullName} ${p.uhid} ${p.phone}`
        .toLowerCase()
        .includes(query.toLowerCase()),
    )
    .slice(0, 6);
  return (
    <div className="app-shell">
      {mobile && (
        <div className="sidebar-backdrop" onClick={() => setMobile(false)} />
      )}
      <aside className={`sidebar ${mobile ? "open" : ""}`}>
        <NavLink to="/" className="brand">
          <span className="brand-mark">
            <Plus size={27} strokeWidth={3.5} />
          </span>
          <span>
            careflow<span className="brand-dot">.</span>
            <small>HOSPITAL INFORMATION SYSTEM</small>
          </span>
        </NavLink>
        <button
          className="hospital-switch"
          onClick={() => setPopover(popover === "hospital" ? null : "hospital")}
        >
          <span className="hospital-icon">
            <Building2 size={19} />
          </span>
          <span>
            <strong>{data.hospitalName}</strong>
            <small>{data.branchName}</small>
          </span>
          <ChevronDown size={15} />
        </button>
        <div className="nav-section-label">WORKSPACE</div>
        <nav>
          {visibleNav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.to === "/"}
              className={({ isActive }) =>
                `nav-item ${isActive ? "active" : ""}`
              }
            >
              <n.icon size={19} strokeWidth={1.7} />
              <span>{n.label}</span>
              {n.to === "/appointments" && (
                <span className="nav-count">
                  {
                    data.appointments.filter((a) => a.status === "Checked in")
                      .length
                  }
                </span>
              )}
              {n.to === "/" && <span className="active-nav-dot" />}
            </NavLink>
          ))}
        </nav>
        <div className="nav-section-label clinical-label">
          CLINICAL MODULES
          <span>{String(visibleCareNav.length).padStart(2, "0")}</span>
        </div>
        <nav>
          {visibleCareNav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              className={({ isActive }) =>
                `nav-item ${isActive ? "active" : ""}`
              }
            >
              <n.icon size={19} strokeWidth={1.7} />
              <span>{n.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="nav-section-label clinical-label">MANAGE</div>
        <nav>
          <NavLink
            to="/modules"
            className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
          >
            <Layers3 size={19} strokeWidth={1.7} />
            <span>Modules</span>
          </NavLink>
          {user.permissions.includes("security.admin") && (
            <NavLink
              to="/administration"
              className={({ isActive }) =>
                `nav-item ${isActive ? "active" : ""}`
              }
            >
              <Settings2 size={19} strokeWidth={1.7} />
              <span>Administration</span>
            </NavLink>
          )}
        </nav>
        <div className="sidebar-bottom">
          <button className="help-card" onClick={() => navigate("/modules")}>
            <span>
              <HeartPulse size={20} />
              <ArrowRight size={16} />
            </span>
            <strong>Built for better care.</strong>
            <p>
              Explore the connected
              <br />
              Careflow ecosystem.
            </p>
          </button>
          <div className="sidebar-user">
            <Avatar name={user.name} />
            <div>
              <strong>{user.name}</strong>
              {demo ? (
                <select
                  aria-label="Switch demo role"
                  value={user.role}
                  onChange={(e) => {
                    void switchRole(e.target.value);
                    navigate("/");
                  }}
                >
                  {[
                    "Hospital Admin",
                    "Doctor",
                    "Receptionist",
                    "Billing Staff",
                    "Nurse",
                    "Nurse 2",
                    "Pharmacist",
                    "Lab Technician",
                    "Pathologist",
                    "Radiologist",
                    "Blood Bank Technician",
                    "Insurance Coordinator",
                    "Accountant",
                    "Store Manager",
                    "CSSD Technician",
                  ].map((r) => (
                    <option key={r}>{r}</option>
                  ))}
                </select>
              ) : (
                <small>{user.role}</small>
              )}
            </div>
            <button
              className="icon-btn"
              aria-label="Account options"
              onClick={() =>
                setPopover(popover === "profile" ? null : "profile")
              }
            >
              <MoreHorizontal size={19} />
            </button>
          </div>
        </div>
      </aside>
      <div className="main-shell">
        <header className="topbar">
          <div className="breadcrumb">
            <button
              className="icon-btn mobile-menu"
              aria-label="Open navigation"
              onClick={() => setMobile(true)}
            >
              <Menu size={22} />
            </button>
            <span>Workspace</span>
            <ChevronRight size={14} />
            <strong>{page}</strong>
          </div>
          <div className="topbar-actions">
            <button className="global-search" onClick={() => setSearch(true)}>
              <Search size={17} />
              <span>Search anything…</span>
              <kbd>
                <Command size={11} /> K
              </kbd>
            </button>
            <span className="topbar-divider" />
            <button
              className="icon-btn theme-button"
              aria-label={dark ? "Switch to light mode" : "Switch to dark mode"}
              onClick={() => setDark(!dark)}
            >
              {dark ? <Sun size={19} /> : <Moon size={19} />}
            </button>
            <button
              className={`icon-btn notification-button ${popover === "notifications" ? "selected" : ""}`}
              aria-label="Workspace notifications"
              onClick={() =>
                setPopover(popover === "notifications" ? null : "notifications")
              }
            >
              <Bell size={19} />
              {data.audit.length > 0 && <i />}
            </button>
            <button
              className="topbar-avatar"
              aria-label="Your account"
              onClick={() =>
                setPopover(popover === "profile" ? null : "profile")
              }
            >
              <Avatar name={user.name} size="small" />
            </button>
          </div>
        </header>
        {demo && (
          <div className="demo-ribbon">
            <ShieldCheck size={13} />
            <span>Synthetic preview · no real patient data</span>
            <span className="demo-ribbon-right">
              Phase 4 <span>·</span> Connected to the Careflow API
            </span>
          </div>
        )}
        <main className="page-content">
          <Routes>
            <Route path="/" element={<Overview />} />
            <Route path="/patients" element={<Patients />} />
            <Route path="/patients/:id" element={<PatientDetail />} />
            <Route path="/appointments" element={<Appointments />} />
            <Route path="/consultations" element={<Consultations />} />
            <Route path="/consultations/:id" element={<ConsultationDetail />} />
            <Route path="/billing" element={<Billing />} />
            <Route
              path="/inpatient"
              element={
                <CareRoute module="ipd" permission="ipd.read">
                  <Inpatient />
                </CareRoute>
              }
            />
            <Route
              path="/nursing"
              element={
                <CareRoute module="nursing" permission="nursing.read">
                  <Nursing />
                </CareRoute>
              }
            />
            <Route
              path="/pharmacy"
              element={
                <CareRoute module="pharmacy" permission="pharmacy.read">
                  <Pharmacy />
                </CareRoute>
              }
            />
            <Route
              path="/laboratory"
              element={
                <CareRoute module="laboratory" permission="lab.read">
                  <Laboratory />
                </CareRoute>
              }
            />
            <Route
              path="/radiology"
              element={
                <CareRoute module="radiology" permission="radiology.read">
                  <Radiology />
                </CareRoute>
              }
            />
            <Route
              path="/emergency"
              element={
                <CareRoute module="emergency" permission="emergency.read">
                  <Emergency />
                </CareRoute>
              }
            />
            <Route
              path="/theatre"
              element={
                <CareRoute module="ot" permission="ot.read">
                  <OperationTheatre />
                </CareRoute>
              }
            />
            <Route
              path="/blood-bank"
              element={
                <CareRoute module="blood" permission="blood.read">
                  <BloodBank />
                </CareRoute>
              }
            />
            <Route
              path="/insurance"
              element={
                <CareRoute module="insurance" permission="insurance.read">
                  <Insurance />
                </CareRoute>
              }
            />
            <Route
              path="/incentives"
              element={
                <CareRoute module="incentives" permission="incentive.read">
                  <Incentives />
                </CareRoute>
              }
            />
            <Route
              path="/telemedicine"
              element={
                <CareRoute module="telemedicine" permission="tele.read">
                  <Telemedicine />
                </CareRoute>
              }
            />
            <Route
              path="/inventory"
              element={
                <CareRoute module="inventory" permission="inventory.read">
                  <Inventory />
                </CareRoute>
              }
            />
            <Route
              path="/cssd"
              element={
                <CareRoute module="cssd" permission="cssd.read">
                  <CSSD />
                </CareRoute>
              }
            />
            <Route path="/modules" element={<Modules />} />
            <Route path="/administration" element={<Administration />} />
            <Route
              path="*"
              element={
                <Empty
                  title="This page could not be found"
                  description="Return to your hospital workspace to continue."
                >
                  <button className="btn primary" onClick={() => navigate("/")}>
                    Back to overview
                  </button>
                </Empty>
              }
            />
          </Routes>
        </main>
      </div>
      {popover && (
        <>
          <div className="popover-dismiss" onClick={() => setPopover(null)} />
          {popover === "hospital" && (
            <div className="popover hospital-popover">
              <span className="eyebrow">CURRENT ORGANIZATION</span>
              <h3>{data.hospitalName}</h3>
              <div>
                <Building2 size={18} />
                <span>
                  {data.branchName}
                  <small>Active branch</small>
                </span>
                <Check size={17} className="teal-text" />
              </div>
              <p>
                Multi-branch provisioning is part of the hospital setup
                workflow. This preview is scoped to one branch.
              </p>
              <button
                className="text-link"
                onClick={() => {
                  navigate("/administration");
                  setPopover(null);
                }}
              >
                Organization settings <ArrowRight size={14} />
              </button>
            </div>
          )}
          {popover === "notifications" && (
            <div className="popover notifications-popover">
              <div className="popover-title">
                <h3>Workspace updates</h3>
                <Activity size={18} />
              </div>
              <p className="muted small">
                Recent activity visible to your role
              </p>
              {data.audit.slice(0, 5).map((a) => (
                <div className="notification-item" key={a.id}>
                  <span className="activity-dot" />
                  <div>
                    <strong>{a.action.replaceAll(".", " ")}</strong>
                    <p>
                      {a.actorName} ·{" "}
                      {new Date(a.occurredAt).toLocaleTimeString([], {
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </p>
                  </div>
                </div>
              ))}
              {!data.audit.length && (
                <p className="muted">No recent updates.</p>
              )}
              <button
                className="text-link"
                onClick={() => {
                  navigate(
                    user.permissions.includes("audit.read")
                      ? "/administration?tab=audit"
                      : "/",
                  );
                  setPopover(null);
                }}
              >
                View workspace activity <ArrowRight size={14} />
              </button>
            </div>
          )}
          {popover === "profile" && (
            <div className="popover profile-popover">
              <Avatar name={user.name} />
              <h3>{user.name}</h3>
              <p>
                {user.role} · {data.branchName}
              </p>
              <span className="badge green">
                <ShieldCheck size={13} />
                Authenticated session
              </span>
              <button
                className="btn full"
                onClick={() => {
                  setDark(!dark);
                }}
              >
                {dark ? <Sun size={16} /> : <Moon size={16} />}
                {dark ? "Light appearance" : "Dark appearance"}
              </button>
              <button className="btn full" onClick={() => void logout()}>
                <LogOut size={16} />
                Sign out
              </button>
              {demo && (
                <small className="muted">
                  Demo sessions can be restarted from the sign-in screen’s
                  preview button.
                </small>
              )}
            </div>
          )}
        </>
      )}
      {search && (
        <Modal
          title="Find a patient"
          subtitle="Search the recent registry by name, UHID or phone number."
          onClose={() => {
            setSearch(false);
            setQuery("");
          }}
        >
          <div className="modal-body">
            <div className="command-search">
              <Search size={21} />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Who are you looking for?"
                aria-label="Search all patients"
              />
            </div>
            <div className="command-results">
              {results.map((p) => (
                <button
                  key={p.id}
                  onClick={() => {
                    navigate(`/patients/${p.id}`);
                    setSearch(false);
                    setQuery("");
                  }}
                >
                  <Avatar name={p.fullName} />
                  <span>
                    <strong>{p.fullName}</strong>
                    <small>
                      {p.uhid} · {p.phone}
                    </small>
                  </span>
                  <ArrowRight size={16} />
                </button>
              ))}
              {!results.length && (
                <Empty
                  title="No matching patient"
                  description="Try another name or UHID."
                />
              )}
            </div>
          </div>
          <div className="command-footer">
            <span>
              <kbd>esc</kbd> to close
            </span>
            <span>
              Patient records open in a new view <ClipboardList size={14} />
            </span>
          </div>
        </Modal>
      )}
      {toast && (
        <div className={`toast ${toast.kind}`} role="status">
          {toast.kind === "error" ? (
            <CircleHelp size={19} />
          ) : (
            <Check size={19} />
          )}
          <span>{toast.message}</span>
        </div>
      )}
    </div>
  );
}
