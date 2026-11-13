import { test, expect } from "@playwright/test";

test("phase 2 inpatient, nursing, pharmacy and LIS workflow", async ({
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

  const registration = await page.request.post("/api/v1/patients", {
    data: {
      firstName: "Care",
      lastName: `E2E${suffix}`,
      dateOfBirth: "1990-01-01",
      sex: "Other",
      phone: "",
    },
    headers: {
      "X-Requested-With": "Careflow",
      Origin: "http://127.0.0.1:5173",
    },
  });
  expect(registration.ok()).toBeTruthy();
  const patient = await registration.json();
  const name = patient.fullName as string;
  await page.reload();
  await expect(
    page.getByRole("heading", {
      name: /Good (morning|afternoon|evening), Anjali/,
    }),
  ).toBeVisible();
  const workspace = await (await page.request.get("/api/v1/workspace")).json();
  const bed = workspace.workspace.care.beds.find(
    (b: { status: string }) => b.status === "Available",
  );

  await page.getByRole("link", { name: "Inpatient", exact: true }).click();
  await page.getByRole("button", { name: "Admit patient" }).click();
  const admit = page.getByRole("dialog");
  await admit.getByLabel("Patient", { exact: false }).selectOption(patient.id);
  await admit
    .getByLabel("Available bed", { exact: false })
    .selectOption(bed.id);
  await admit
    .getByLabel("Admission reason")
    .fill("Synthetic phase 2 e2e admission");
  await admit
    .getByRole("button", { name: "Admit patient", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("admitted");

  await page.getByLabel("Switch demo role").selectOption("Nurse");
  await page.getByRole("link", { name: "Nursing", exact: true }).click();
  await page.getByRole("button", { name: new RegExp(name) }).click();
  await page.getByRole("button", { name: "Nursing note" }).click();
  const note = page.getByRole("dialog");
  await note
    .getByLabel("Nursing note", { exact: false })
    .fill("Synthetic nursing assessment with escalation");
  await note.getByLabel("Pain score 0–10").fill("8");
  await note.getByLabel("Fall risk").selectOption("High");
  await note.getByRole("button", { name: "Record note" }).click();
  await expect(page.getByRole("status")).toContainText("Nursing note recorded");
  await expect(page.getByText(/open nursing escalation/)).toBeVisible();

  await page.getByLabel("Switch demo role").selectOption("Pharmacist");
  await page.getByRole("link", { name: "Pharmacy", exact: true }).click();
  await page.getByRole("button", { name: "Receive stock" }).click();
  const stock = page.getByRole("dialog");
  await stock.getByLabel("Batch number").fill(`E2E-${suffix}`);
  await stock.getByLabel("Expiry date").fill("2027-11-13");
  await stock.getByLabel("Quantity").fill("10");
  await stock.getByLabel("Unit cost").fill("1");
  await stock.getByLabel("Supplier").fill("Synthetic supplier");
  await stock.getByLabel("GRN / reference").fill(`GRN-${suffix}`);
  await stock
    .getByRole("button", { name: "Receive stock", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("Stock batch received");

  await page.getByLabel("Switch demo role").selectOption("Doctor");
  await page.getByRole("link", { name: "Laboratory", exact: true }).click();
  await page.getByRole("button", { name: "Order test" }).click();
  const order = page.getByRole("dialog");
  await order.getByLabel("Patient", { exact: false }).selectOption(patient.id);
  await order
    .getByLabel("Test", { exact: false })
    .selectOption({ label: "Hemoglobin (Blood)" });
  await order.getByLabel("Priority").selectOption("STAT");
  await order.getByRole("button", { name: "Order test", exact: true }).click();
  await expect(page.getByRole("status")).toContainText(
    "Laboratory test ordered",
  );

  await page.getByLabel("Switch demo role").selectOption("Lab Technician");
  await page.getByRole("link", { name: "Laboratory", exact: true }).click();
  await page.getByLabel("Search order, patient, test or barcode…").fill(name);
  const row = page.getByRole("row").filter({ hasText: name });
  await row.getByRole("button", { name: "Collect", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Collect specimen" })
    .click();
  await expect(page.getByRole("status")).toContainText("Specimen collected");
  await row.getByRole("button", { name: "Process", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("processing");
  await row.getByRole("button", { name: "Enter result" }).click();
  const result = page.getByRole("dialog");
  await result.getByLabel("Result value").fill("6.5");
  await result.getByRole("button", { name: "Save result" }).click();
  await expect(page.getByRole("status")).toContainText("Result entered");

  const validatorLogin = await page.request.post("/api/v1/auth/login", {
    data: { userName: "labtech2", password: "Preview!2026" },
    headers: {
      "X-Requested-With": "Careflow",
      Origin: "http://127.0.0.1:5173",
    },
  });
  expect(validatorLogin.ok()).toBeTruthy();
  await page.reload();
  await page.getByRole("link", { name: "Laboratory", exact: true }).click();
  await page.getByLabel("Search order, patient, test or barcode…").fill(name);
  await page
    .getByRole("row")
    .filter({ hasText: name })
    .getByRole("button", { name: "Validate", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("validated");

  await page.getByLabel("Switch demo role").selectOption("Pathologist");
  await page.getByRole("link", { name: "Laboratory", exact: true }).click();
  await page.getByLabel("Search order, patient, test or barcode…").fill(name);
  await page
    .getByRole("row")
    .filter({ hasText: name })
    .getByRole("button", { name: "Authorize", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("authorized");
  await expect(page.getByRole("row").filter({ hasText: name })).toContainText(
    "Authorized",
  );
  expect(errors).toEqual([]);
});
