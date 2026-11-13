import { test, expect } from "@playwright/test";

test("phase 3 radiology, emergency, theatre and blood bank workspaces", async ({
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
  ).toBeVisible();

  // Radiology: worklist renders seeded studies and a new order joins the board.
  await page.getByRole("link", { name: "Radiology", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Imaging worklist" }),
  ).toBeVisible();
  await expect(page.getByText("ACC-R-").first()).toBeVisible();
  await page.getByRole("button", { name: "New study" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  const patientSelect = page.getByLabel("Patient");
  const label = (await patientSelect.locator("option").first().textContent())!;
  const fullName = label.split(" · ")[0];
  await patientSelect.selectOption({ index: 0 });
  await page.getByLabel("Procedure").fill("CT Brain plain");
  await page.getByLabel("Body part").fill("Brain");
  await page
    .getByLabel("Clinical indication")
    .fill("Synthetic e2e acute onset deficit");
  await page.getByRole("button", { name: "Order study" }).click();
  await expect(page.getByText("Study ordered")).toBeVisible();
  await expect(page.getByText("CT Brain plain").first()).toBeVisible();

  // Emergency: register an arrival and triage it on the board.
  await page.getByRole("link", { name: "Emergency", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Emergency board" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Register arrival" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Linked patient (optional)").selectOption({ index: 1 });
  await page
    .getByLabel("Patient name (must match linked identity)")
    .fill(fullName);
  await page.getByRole("button", { name: "Register episode" }).click();
  await expect(page.getByText("Episode registered")).toBeVisible();
  await page.getByRole("button", { name: "Triage" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Triage category").selectOption("Yellow");
  await page
    .getByLabel("Presenting complaint")
    .fill("Synthetic e2e abdominal pain");
  await page.getByRole("button", { name: "Record triage" }).click();
  await expect(page.getByText("Triage recorded")).toBeVisible();

  // Theatre: request a case, schedule a conflict-free slot and record the checklist.
  await page.getByRole("link", { name: "Operation Theatre" }).click();
  await expect(
    page.getByRole("heading", { name: "Theatre schedule" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Request surgery" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Patient").selectOption({ index: 0 });
  await page.getByLabel("Procedure").fill("Synthetic e2e forearm procedure");
  await page.getByLabel("Site").fill("Left forearm");
  await page.getByLabel("Laterality").selectOption("Left");
  await page.getByRole("button", { name: "Request booking" }).click();
  await expect(page.getByText("Surgery requested")).toBeVisible();
  await page.getByRole("button", { name: "Schedule" }).first().click();
  const scheduleDialog = page.getByRole("dialog", {
    name: "Schedule theatre slot",
  });
  await expect(scheduleDialog).toBeVisible();
  await scheduleDialog.getByLabel("Theatre").fill(`E2E-OT-${suffix}`);
  const start = new Date(Date.now() + 3 * 24 * 3600 * 1000);
  start.setUTCHours(9, 0, 0, 0);
  await scheduleDialog
    .getByLabel("Start")
    .fill(start.toISOString().slice(0, 16));
  await scheduleDialog
    .getByRole("button", { name: "Schedule", exact: true })
    .click();
  await expect(page.getByText("Theatre scheduled")).toBeVisible();
  await page.getByRole("button", { name: "Checklist" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  for (const item of [
    "Informed consent verified",
    "Anaesthesia assessment completed",
    "Fasting status confirmed",
    "Surgical site marked",
    "Team safety checklist completed",
  ]) {
    await page.getByLabel(item).check();
  }
  await page.getByRole("button", { name: "Record checklist" }).click();
  await expect(page.getByText("Checklist recorded")).toBeVisible();

  // Blood bank: receive, screen and crossmatch a unit.
  await page.getByRole("link", { name: "Blood Bank" }).click();
  await expect(
    page.getByRole("heading", { name: "Inventory & issues" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Receive unit" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Unit number").fill(`E2E-${suffix}`);
  await page.getByLabel("Blood group").selectOption("O-");
  await page.getByLabel("Component").selectOption("PRBC");
  const expiry = new Date(Date.now() + 30 * 24 * 3600 * 1000);
  await page.getByLabel("Expiry date").fill(expiry.toISOString().slice(0, 10));
  await page.getByLabel("Donor").fill("Synthetic e2e donor");
  await page
    .getByRole("dialog", { name: "Receive blood unit" })
    .getByRole("button", { name: "Receive unit" })
    .click();
  await expect(page.getByText("Unit received into quarantine")).toBeVisible();
  await page.getByRole("button", { name: "Screen" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("button", { name: "Record screening" }).click();
  await expect(page.getByText("Screening recorded")).toBeVisible();

  expect(errors).toEqual([]);
});
