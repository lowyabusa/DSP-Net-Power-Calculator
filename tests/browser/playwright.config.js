import { defineConfig } from "@playwright/test";

export default defineConfig({
    testDir: ".",
    timeout: 90_000,
    use: { baseURL: "http://localhost:4173/DSP-Net-Power-Calculator/", browserName: "chromium" },
    webServer: {
        command: "node static-server.js",
        url: "http://localhost:4173/DSP-Net-Power-Calculator/",
    },
});
