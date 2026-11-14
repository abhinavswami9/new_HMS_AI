import { afterEach, describe, expect, it, vi } from "vitest";
import { age, csvCell, localInputDate, money } from "./api";
afterEach(() => vi.useRealTimers());
describe("clinical display helpers", () => {
  it("calculates age without counting an unreached birthday", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-11-13T12:00:00Z"));
    expect(age("1992-11-13")).toBe(34);
    expect(age("1992-11-14")).toBe(33);
  });
  it("formats Indian rupees without fractional display noise", () => {
    expect(money(123456)).toContain("1,23,456");
    expect(money(0)).toContain("0");
  });
  it("formats a local datetime for an input without shifting the date", () => {
    expect(localInputDate(new Date(2026, 10, 13, 9, 30))).toBe(
      "2026-11-13T09:30",
    );
  });
});
describe("CSV export safety", () => {
  it("quotes separators and embedded quotes", () => {
    expect(csvCell('A, "B"')).toBe('"A, ""B"""');
  });
  it("neutralizes spreadsheet formulas", () => {
    expect(csvCell('=HYPERLINK("x")')).toBe('"\'=HYPERLINK(""x"")"');
    expect(csvCell("+SUM(1)")).toBe('"\'+SUM(1)"');
  });
  it("preserves identifiers and numeric zero", () => {
    expect(csvCell("NCH-2026-000001")).toBe('"NCH-2026-000001"');
    expect(csvCell(0)).toBe('"0"');
  });
});
