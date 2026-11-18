import { createServer } from "node:http";
import { createReadStream, statSync } from "node:fs";
import { extname, resolve, sep } from "node:path";

const root = resolve(process.env.DSP_PUBLISH_DIR ?? "../../src/DspNetEnergy.Web/bin/Release/net10.0/publish/wwwroot");
const base = "/DSP-Net-Power-Calculator/";
const types = { ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript",
    ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm",
    ".webmanifest": "application/manifest+json", ".svg": "image/svg+xml", ".png": "image/png" };

createServer((request, response) => {
    const path = new URL(request.url, "http://localhost").pathname;
    const file = resolve(root, path.slice(base.length) || "index.html");
    if (!path.startsWith(base) || !file.startsWith(root + sep)) {
        response.writeHead(404).end();
        return;
    }
    try {
        if (!statSync(file).isFile()) throw new Error("Not a file");
    } catch {
        response.writeHead(404).end();
        return;
    }
    response.setHeader("Content-Type", types[extname(file)] ?? "application/octet-stream");
    createReadStream(file).pipe(response);
}).listen(4173, "localhost");
