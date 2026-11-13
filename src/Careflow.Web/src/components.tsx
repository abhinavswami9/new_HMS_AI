import { useEffect, useRef } from "react";
import type { ReactNode } from "react";
import {
  X,
  LoaderCircle,
  Search,
  Inbox,
  ChevronLeft,
  ChevronRight,
  AlertCircle,
} from "lucide-react";
import { initials } from "./api";
export function Avatar({
  name,
  size = "",
  color,
}: {
  name: string;
  size?: string;
  color?: string;
}) {
  return (
    <span
      className={`avatar ${size}`}
      style={color ? { background: `${color}18`, color } : undefined}
    >
      {initials(name)}
    </span>
  );
}
export function Badge({ status }: { status: string }) {
  const tone: Record<string, string> = {
    Booked: "blue",
    "Checked in": "amber",
    "In consultation": "teal",
    Completed: "green",
    Cancelled: grayClass(),
    Draft: "gray",
    Signed: "green",
    Issued: "amber",
    "Part paid": "blue",
    Paid: "green",
    Available: "green",
  };
  return (
    <span className={`badge ${tone[status] || "gray"}`}>
      <span />
      {status}
    </span>
  );
}
function grayClass() {
  return "gray";
}
export function PageHeader({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow?: string;
  title: string;
  description: string;
  children?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <div className="heading-actions">{children}</div>
    </div>
  );
}
export function Modal({
  title,
  subtitle,
  children,
  onClose,
  wide = false,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const closeRef = useRef(onClose);
  closeRef.current = onClose;
  useEffect(() => {
    const old = document.activeElement as HTMLElement;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const dialog = ref.current;
    const focusable = () =>
      dialog?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex="0"]',
      );
    focusable()?.[0]?.focus();
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeRef.current();
      if (e.key === "Tab") {
        const els = focusable();
        if (!els?.length) return;
        const first = els[0],
          last = els[els.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", key);
    return () => {
      document.body.style.overflow = overflow;
      document.removeEventListener("keydown", key);
      old?.focus();
    };
  }, []);
  return (
    <div
      className="modal-overlay"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        className={`modal ${wide ? "wide" : ""}`}
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div className="modal-header">
          <div>
            <h2>{title}</h2>
            {subtitle && <p>{subtitle}</p>}
          </div>
          <button
            className="icon-btn"
            aria-label="Close dialog"
            onClick={onClose}
          >
            <X size={20} />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
export function Empty({
  title = "Nothing here yet",
  description = "New records will appear here.",
  children,
}: {
  title?: string;
  description?: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <Inbox size={30} />
      <h3>{title}</h3>
      <p>{description}</p>
      {children}
    </div>
  );
}
export function SearchBox({
  value,
  onChange,
  placeholder = "Search…",
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}) {
  return (
    <div className="search-box">
      <Search size={17} />
      <input
        aria-label={placeholder}
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {value && (
        <button aria-label="Clear search" onClick={() => onChange("")}>
          <X size={14} />
        </button>
      )}
    </div>
  );
}
export function Field({
  label,
  children,
  hint,
  required,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
  required?: boolean;
}) {
  return (
    <label className="field">
      <span>
        {label}
        {required && <b> *</b>}
      </span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  );
}
export function ErrorNote({ children }: { children: ReactNode }) {
  return (
    <div className="error-note" role="alert">
      <AlertCircle size={17} />
      {children}
    </div>
  );
}
export function Submit({
  busy,
  children,
}: {
  busy: boolean;
  children: ReactNode;
}) {
  return (
    <button className="btn primary" type="submit" disabled={busy}>
      {busy && <LoaderCircle size={16} className="spin" />}
      {busy ? "Saving…" : children}
    </button>
  );
}
export function Pagination({
  page,
  total,
  pageSize,
  onChange,
}: {
  page: number;
  total: number;
  pageSize: number;
  onChange: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="pagination">
      <span>
        {total
          ? `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, total)} of ${total}`
          : "0 records"}
      </span>
      <div>
        <button
          className="icon-btn"
          aria-label="Previous page"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
        >
          <ChevronLeft size={17} />
        </button>
        <span>
          Page {page} of {pages}
        </span>
        <button
          className="icon-btn"
          aria-label="Next page"
          disabled={page >= pages}
          onClick={() => onChange(page + 1)}
        >
          <ChevronRight size={17} />
        </button>
      </div>
    </div>
  );
}
export function useAction() {
  const ref = useRef(false);
  return async (action: () => Promise<void>) => {
    if (ref.current) return;
    ref.current = true;
    try {
      await action();
    } finally {
      ref.current = false;
    }
  };
}
