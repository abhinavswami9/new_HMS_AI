import { test, expect } from "@playwright/test";

test("phase 5 quality, patient portal and reporting workspaces", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  const suffix = `${Date.now().toString(36)}${Math.floor(Math.random() * 10000)}`;
  await page.goto("/");
  await expect(
    page.getByRole("heading", {
      name: /Good (morning|afternoon|evening), Anjali/,
    }),
  ).toBeVisible({ timeout: 15000 });

  // Quality: incident board with seeded CAPA and controlled documents.
  await page.getByRole("link", { name: "Quality", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Incidents, CAPA & evidence" }),
  ).toBeVisible();
  await expect(page.getByText("QIR-SEED-").first()).toBeVisible();
  await expect(page.getByText("CAPA-SEED-").first()).toBeVisible();
  await page.getByRole("button", { name: "Report incident" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Location").fill(`Ward E2E ${suffix}`);
  await page
    .getByLabel("Description")
    .fill("Synthetic e2e near miss caught at the bedside double check.");
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(page.getByText("Ward E2E", { exact: false }).first()).toBeVisible();

  // Patient portal desk: verified account and proofing copy render.
  await page.getByRole("link", { name: "Patient Portal", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Verified access desk" }),
  ).toBeVisible();
  await expect(page.getByText("dolly.chauhan")).toBeVisible();
  await expect(
    page.getByText(/every view and download is audited/i),
  ).toBeVisible();

  // Reporting: governed dashboards and the no-arbitrary-SQL copy.
  await page.getByRole("link", { name: "Reporting", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Dashboards & governed reports" }),
  ).toBeVisible();
  await expect(page.getByText("Registered patients")).toBeVisible();
  await page.getByRole("button", { name: "New definition" }).click();
  await expect(page.getByRole("dialog")).toContainText(
    "arbitrary SQL is rejected",
  );
  await page.getByLabel("Code").fill(`E2E${suffix.slice(-4)}`);
  await page.getByLabel("Name").fill("E2E governed census");
  await page.getByLabel("Columns").fill("uhid,patient");
  await page.getByRole("button", { name: "Create draft" }).click();
  await expect(page.getByText("E2E governed census")).toBeVisible();

  expect(errors).toEqual([]);
});
