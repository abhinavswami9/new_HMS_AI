import { test, expect } from "@playwright/test";

test("synthetic patient → appointment → signed EMR → invoice → payment", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  const last = `Review${Date.now().toString(36)}${Math.floor(Math.random() * 10000)}`;
  const name = `Asha ${last}`;
  await page.goto("/");
  await expect(
    page.getByRole("heading", {
      name: /Good (morning|afternoon|evening), Anjali/,
    }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: /Register patient/ })
    .first()
    .click();
  const registration = page.getByRole("dialog");
  await registration.getByLabel("First name").fill("Asha");
  await registration.getByLabel("Last name").fill(last);
  await registration.getByLabel("Date of birth").fill("1992-04-12");
  await registration
    .getByLabel("Mobile number")
    .fill(`+91 90${Date.now().toString().slice(-8)}`);
  await registration
    .getByRole("button", { name: "Register patient", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("registered");
  await page.getByRole("link", { name: "Patients", exact: true }).click();
  await page.getByLabel("Search by name, UHID or phone number…").fill(name);
  await page.getByRole("row").filter({ hasText: name }).click();
  await expect(page.getByRole("heading", { name })).toBeVisible();
  const patientId = page.url().split("/").pop()!;
  await page.getByRole("button", { name: "Book appointment" }).click();
  const booking = page.getByRole("dialog");
  const workspace = await (await page.request.get("/api/v1/workspace")).json();
  const doctorId = workspace.workspace.doctors[0].id;
  const slot = new Date(Math.floor(Date.now() / 60000) * 60000 + 86400000);
  while (
    workspace.workspace.appointments.some(
      (a: {
        doctorId: string;
        status: string;
        startsAt: string;
        durationMinutes: number;
      }) =>
        a.doctorId === doctorId &&
        a.status !== "Cancelled" &&
        new Date(a.startsAt).getTime() < slot.getTime() + 1200000 &&
        new Date(a.startsAt).getTime() + a.durationMinutes * 60000 >
          slot.getTime(),
    )
  )
    slot.setTime(slot.getTime() + 1200000);
  await booking
    .getByLabel("Date and time")
    .fill(slot.toISOString().slice(0, 16));
  await booking
    .getByLabel("Reason for visit")
    .fill("Synthetic workflow verification");
  await booking
    .getByRole("button", { name: "Book appointment", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText("Appointment booked");
  await page
    .getByRole("link", { name: /Appointments/ })
    .first()
    .click();
  await page.getByRole("button", { name: "Show all dates" }).click();
  await page.getByLabel("Search patient, UHID or token…").fill(name);
  await page
    .getByRole("row")
    .filter({ hasText: name })
    .getByRole("button", { name: "Check in" })
    .click();
  await expect(page.getByRole("row").filter({ hasText: name })).toContainText(
    "Checked in",
  );
  await page.getByLabel("Switch demo role").selectOption("Doctor");
  await page
    .getByRole("link", { name: /Appointments/ })
    .first()
    .click();
  await page.getByRole("button", { name: "Show all dates" }).click();
  await page.getByLabel("Search patient, UHID or token…").fill(name);
  await page
    .getByRole("row")
    .filter({ hasText: name })
    .getByRole("button", { name: "Consult", exact: true })
    .click();
  await expect(page.getByRole("heading", { name })).toBeVisible();
  await page
    .getByLabel("Chief complaint")
    .fill("Synthetic test complaint for workflow verification");
  await page
    .getByLabel("Diagnosis", { exact: false })
    .fill("Synthetic test diagnosis — not clinical advice");
  await page.getByRole("button", { name: "Prescription", exact: true }).click();
  const medicineId = await page
    .getByLabel("Select medicine to prescribe")
    .locator("option")
    .nth(1)
    .getAttribute("value");
  await page
    .getByLabel("Select medicine to prescribe")
    .selectOption(medicineId!);
  await page.getByRole("button", { name: "Add", exact: true }).click();
  await page.getByLabel("Dose", { exact: false }).fill("1 tablet");
  await page.getByLabel("Frequency", { exact: false }).fill("Once daily");
  await page
    .getByLabel("Instructions", { exact: true })
    .fill("Synthetic test instructions");
  await page.getByRole("checkbox", { name: /I have reconciled/ }).check();
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByRole("status")).toContainText("draft saved");
  await page.getByRole("button", { name: "Review & sign" }).click();
  await page.getByRole("button", { name: "Confirm & sign" }).click();
  await expect(page.getByRole("status")).toContainText("signed and locked");
  await expect(
    page.getByText("Electronically signed by Dr. Anjali Sharma"),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Save draft" })).toHaveCount(0);
  await page.getByLabel("Switch demo role").selectOption("Hospital Admin");
  await page.getByRole("link", { name: "Billing", exact: true }).click();
  await page.getByRole("button", { name: "Create invoice" }).click();
  const invoice = page.getByRole("dialog");
  await invoice.getByLabel("Patient", { exact: false }).selectOption(patientId);
  await invoice.getByRole("checkbox", { name: /General consultation/ }).check();
  const createdResponse = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/v1/invoices") && r.request().method() === "POST",
  );
  await invoice.getByRole("button", { name: "Create draft invoice" }).click();
  const created = await (await createdResponse).json();
  await expect(page.getByRole("status")).toContainText("Draft invoice created");
  await page
    .getByLabel("Search invoice number or patient…")
    .fill(created.number);
  await page.getByRole("row").filter({ hasText: created.number }).click();
  await page.getByRole("button", { name: "Issue invoice" }).click();
  await expect(page.getByRole("status")).toContainText("Invoice issued");
  await page
    .getByRole("button", { name: "Record payment", exact: true })
    .click();
  await page.getByRole("button", { name: "Confirm payment" }).click();
  await expect(page.getByRole("status")).toContainText("Payment recorded");
  await expect(page.getByText("Fully settled")).toBeVisible();
  expect(errors).toEqual([]);
});

test("mobile navigation, module honesty and dark appearance", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(
    page.getByText("Synthetic preview · no real patient data"),
  ).toBeVisible();
  await page.getByRole("button", { name: "Open navigation" }).click();
  await page.getByRole("link", { name: "Modules", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "One platform. Every possibility." }),
  ).toBeVisible();
  await page.getByLabel("Filter module phase").selectOption("Phase 2");
  await page.getByRole("button", { name: /Laboratory · LIS/ }).click();
  await expect(page.getByRole("dialog")).toContainText(
    "implementation planned",
  );
  await page.getByRole("button", { name: "Got it" }).click();
  await page.getByRole("button", { name: "Switch to dark mode" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
