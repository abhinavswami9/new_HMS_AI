import { test, expect } from "@playwright/test";

test("phase 4 insurance, incentives, telemedicine, inventory and CSSD workspaces", async ({
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

  // Insurance & TPA: payer desk renders seeded plans and registers a coverage.
  await page.getByRole("link", { name: "Insurance & TPA", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Payer desk" })).toBeVisible();
  await page.getByRole("button", { name: "Register coverage" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Policy number").fill(`POL-E2E-${suffix}`);
  await page.getByLabel("Member ID").fill(`MBR-${suffix}`);
  await page.getByLabel("Valid from").fill("2026-11-01");
  await page.getByLabel("Valid to").fill("2027-11-01");
  await page.getByLabel("Sum insured").fill("150000");
  await page.getByLabel("Deductible").fill("1000");
  await page.getByRole("button", { name: "Register coverage" }).last().click();
  await expect(page.getByText(`POL-E2E-${suffix}`)).toBeVisible();

  // Incentives: rule and payout board render with the calculate action.
  await page.getByRole("link", { name: "Incentives", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Revenue share & payouts" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Calculate batch" }).click();
  await expect(page.getByRole("dialog")).toContainText("Closed period");
  await page.getByRole("button", { name: "Close dialog" }).click();

  // Telemedicine: consent-gated sessions board with the provider boundary note.
  await page.getByRole("link", { name: "Telemedicine", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Consent-gated video consultations" }),
  ).toBeVisible();
  await expect(
    page.getByText(/no external video provider is configured/i),
  ).toBeVisible();

  // Inventory: item master, indents and transfers with separation-of-duties copy.
  await page.getByRole("link", { name: "Inventory", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Purchase to consumption" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Raise indent" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Quantity").fill("12");
  await page.getByLabel("Reason").fill("Reorder level breach on sterile gloves.");
  await page.getByRole("button", { name: "Raise indent" }).last().click();
  await expect(page.getByText("IND-").first()).toBeVisible();

  // CSSD: instrument sets CS-001 and validated cycles render.
  await page.getByRole("link", { name: "CSSD", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Sterile supply chain" }),
  ).toBeVisible();
  await expect(page.getByText("CS-001").first()).toBeVisible();

  expect(errors).toEqual([]);
});
