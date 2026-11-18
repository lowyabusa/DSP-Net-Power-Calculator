import { test, expect } from "@playwright/test";
import { readFile } from "node:fs/promises";

test("actual WebAssembly balances and route rankings equal native .NET exactly", async ({ page }) => {
    await page.goto("./");
    await expect(page.locator("#net-power")).toBeVisible();
    const native = await readFile(process.env.DSP_NATIVE_PARITY ?? "./native-parity.json", "utf8");
    const wasm = await page.evaluate(() => DotNet.invokeMethodAsync("DspNetEnergy.Web", "RunParityScenarios"));
    expect(wasm).toEqual(native);
});

test("scenario input, dialog, JSON and offline reload under the Pages subpath", async ({ page, context }) => {
    await page.goto("./");
    await expect(page.locator("#net-power")).toBeVisible();
    await page.waitForFunction(() => navigator.serviceWorker.controller);
    await page.getByRole("button", { name: "All fuels" }).click();
    await page.getByRole("searchbox").fill("hydrogen");
    await page.getByRole("dialog").getByRole("button", { name: "Hydrogen", exact: true }).click();
    await page.locator("#rate").fill("12.125");
    await expect(page.locator("#net-power")).not.toContainText("—");
    await expect.poll(() => page.evaluate(() => JSON.parse(dsp.load().scenario).Rate)).toBe(12.125);
    const valid = await page.locator("#net-power").textContent();

    await page.locator("#rate").fill("abc");
    await expect(page.getByRole("alert").filter({ hasText: "nonnegative number" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Export JSON" })).toBeDisabled();
    await page.locator("#rate").fill("12.125");

    const downloadEvent = page.waitForEvent("download");
    await page.getByRole("button", { name: "Export JSON" }).click();
    const download = await downloadEvent;
    const json = await readFile(await download.path(), "utf8");
    expect(JSON.parse(json)).toMatchObject({ SchemaVersion: 1, Fuel: "Hydrogen", Rate: 12.125 });

    const file = page.locator('input[accept^=".json"]');
    await file.setInputFiles({ name: "invalid.json", mimeType: "application/json", buffer: Buffer.from("{}") });
    await expect(page.locator("footer")).toContainText("Could not import");
    await expect(page.locator("#rate")).toHaveValue("12.125");

    await context.setOffline(true);
    await page.reload();
    await expect(page.locator("#rate")).toHaveValue("12.125");
    await expect(page.locator("#net-power")).toHaveText(valid.trim());
    await file.setInputFiles({ name: "scenario.json", mimeType: "application/json", buffer: Buffer.from(json.replace("12.125", "15.25")) });
    await expect(page.locator("#rate")).toHaveValue("15.25");
});

test("desktop JSON uses the same schema and numeric precision in WebAssembly", async ({ page }) => {
    await page.goto("./");
    await expect(page.locator("#net-power")).toBeVisible();
    const json = '{"Fuel":"Coal","Rate":12.123456789012345678901234567,"GeneratorCount":2,"Tier":"MkIII","SelfSpray":true}';
    const result = await page.evaluate(input => DotNet.invokeMethodAsync("DspNetEnergy.Web", "RoundTripScenario", input), json);
    expect(result).toContain("12.123456789012345678901234567");
    expect(JSON.parse(result)).toMatchObject({ SchemaVersion: 1, Fuel: "Coal", GeneratorCount: 2, Tier: "MkIII", SelfSpray: true });
});

test("lower proliferator tiers enable self-spray and update available charges", async ({ page }) => {
    await page.goto("./?transient=1");
    const toggle = page.getByRole("checkbox", { name: "Self-spray Proliferator" });
    await expect(toggle).toBeDisabled();
    const scenarioFile = page.locator("#import-scenario");

    for (const [tier, plainCharges, selfSprayedCharges] of [["MkI", 12, 12], ["MkII", 24, 27]]) {
        const scenario = JSON.stringify({ SchemaVersion: 1, Fuel: "Coal", Rate: 60, Tier: tier });
        await scenarioFile.setInputFiles({ name: "spray.json", mimeType: "application/json", buffer: Buffer.from(scenario) });
        await expect(toggle).toBeEnabled();
        await expect(toggle).not.toBeChecked();
        const charges = page.locator(".columns aside p").filter({ hasText: "sprays per item" });
        await expect(charges).toContainText(`${plainCharges} sprays per item`);
        await toggle.check();
        await expect(toggle).toBeChecked();
        await expect(charges).toContainText(`${selfSprayedCharges} sprays per item`);
    }
});

test("production and logistics editors retain overrides across reloads", async ({ page }) => {
    await page.goto("./");
    await expect(page.locator("#net-power")).toBeVisible();
    await page.getByRole("button", { name: "Machine settings", exact: true }).first().click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await page.locator("#edit-count").fill("0");
    await page.getByRole("dialog").getByRole("button", { name: "Apply", exact: true }).click();
    await expect(page.getByText("Check capacity or settings")).toBeVisible();
    await page.getByRole("button", { name: "Machine settings", exact: true }).first().click();
    await page.locator("#edit-count").fill("3");
    await page.getByRole("dialog").getByRole("button", { name: "Apply", exact: true }).click();
    await expect(page.locator("#net-power")).not.toContainText("—");

    await page.locator("#advanced summary").click();
    await page.getByRole("button", { name: "Edit logistics costs" }).click();
    await page.locator("#edit-sorters-count").fill("0");
    await page.locator("#edit-drones-rate").fill("2");
    await page.locator("#edit-drones-energy").fill("6000");
    await page.getByRole("dialog").getByRole("button", { name: "Apply", exact: true }).click();
    await expect.poll(() => page.evaluate(() => JSON.parse(dsp.load().scenario).Logistics?.Drones?.TripsPerMinute)).toBe(2);
    await page.getByRole("button", { name: "German", exact: true }).click();
    await page.reload();
    await expect(page.locator("html")).toHaveAttribute("lang", "de");
    await expect(page.getByText("3 ×", { exact: false }).first()).toBeVisible();
    await page.getByText("Logistikkosten im Detail").click();
    await expect(page.locator(".metric").filter({ hasText: "Frachtdrohnen" }).getByText("0,2 MW", { exact: true })).toBeVisible();
});

test("local Steam file slices preserve bytes without downloading artwork", async ({ page }) => {
    await page.goto("./");
    await expect(page.locator("#game-folder")).toBeAttached();
    const sample = await page.evaluate(() => {
        const input = document.querySelector("#game-folder");
        const file = new File([new Uint8Array([0, 127, 128, 255, 42])], "resources.assets");
        Object.defineProperty(file, "webkitRelativePath", { value: "Dyson Sphere Program/DSPGAME_Data/resources.assets" });
        Object.defineProperty(input, "files", { value: [file], configurable: true });
        input.dispatchEvent(new Event("change"));
        return Array.from(dsp.assetSlice("resources.assets", 1, 3));
    });
    expect(sample).toEqual([127, 128, 255]);
});

test("transient sessions start with defaults and do not replace the saved scenario", async ({ page }) => {
    await page.goto("./");
    await page.locator("#rate").fill("43");
    await expect.poll(() => page.evaluate(() => JSON.parse(dsp.load().scenario).Rate)).toBe(43);
    await page.goto("./?transient=1");
    await expect(page.locator("#rate")).toHaveValue("120");
    await page.locator("#rate").fill("25");
    await page.getByRole("button", { name: "German", exact: true }).click();
    await page.goto("./");
    await expect(page.locator("#rate")).toHaveValue("43");
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
});

test("ranked routes can be paged and applied to the current fuel", async ({ page }) => {
    await page.goto("./");
    await expect(page.locator("#net-power")).toBeVisible();
    await page.getByRole("button", { name: "All fuels" }).click();
    await page.getByRole("searchbox").fill("hydrogen");
    await page.getByRole("dialog").getByRole("button", { name: "Hydrogen", exact: true }).click();
    await page.getByText("Alternative recipe chains", { exact: true }).click();
    await expect(page.locator(".route")).toHaveCount(6);
    await page.getByRole("button", { name: "More chains" }).click();
    await expect(page.locator(".route").first()).toContainText("7.");
    await page.getByRole("button", { name: "Previous", exact: true }).click();
    await page.locator(".route").getByRole("button", { name: "Apply", exact: true }).first().click();
    await expect.poll(() => page.evaluate(() => Object.keys(JSON.parse(dsp.load().scenario).RecipeChoices).length)).toBeGreaterThan(0);
    await expect(page.locator("#net-power")).not.toContainText("—");
});

test("installed Steam artwork can be loaded and reopened offline", async ({ page, context }) => {
    test.skip(!process.env.DSP_GAME_FOLDER, "Set DSP_GAME_FOLDER to the local Steam installation folder.");
    await page.goto("./");
    await page.waitForFunction(() => navigator.serviceWorker.controller);
    await page.locator("#game-folder").setInputFiles(process.env.DSP_GAME_FOLDER);
    await expect(page.getByText(/game icons loaded\./)).toBeVisible({ timeout: 60_000 });
    await expect(page.locator(".tiles img").first()).toBeVisible();
    await context.setOffline(true);
    await page.reload();
    await expect(page.locator(".tiles img").first()).toBeVisible();
});
