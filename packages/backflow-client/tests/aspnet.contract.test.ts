import { spawn, type ChildProcess } from "node:child_process";
import { setTimeout } from "node:timers/promises";
import { createServer } from "node:net";
import { afterAll, beforeAll, expect, it } from "vitest";
import {
    createBackflowClient,
    BackflowErrorCode,
    BackflowHttpError,
} from "../src/index.js";

// Integration is necessary here: actual ASP routing, CLR-derived catalogs and serialization
// cannot be established by a fetch double or a TypeScript-only unit test.
let host: ChildProcess;
let baseUrl: string;
let logs = "";
beforeAll(async () => {
    const port = await new Promise<number>((resolve, reject) => {
        const server = createServer();
        server.on("error", reject);
        server.listen(0, "127.0.0.1", () => {
            const address = server.address();
            if (!address || typeof address === "string")
                return reject(new Error("No port"));
            server.close(() => resolve(address.port));
        });
    });
    baseUrl = `http://127.0.0.1:${port}/api/graphs`;
    host = spawn(
        "dotnet",
        [
            "run",
            "--project",
            "samples/Backflow.AspNetCore",
            "--configuration",
            "Release",
            "--no-launch-profile",
            "--",
            "--urls",
            `http://127.0.0.1:${port}`,
        ],
        { cwd: process.cwd(), stdio: ["ignore", "pipe", "pipe"] },
    );
    host.stdout?.on("data", (chunk: Buffer) => {
        logs += chunk.toString();
    });
    host.stderr?.on("data", (chunk: Buffer) => {
        logs += chunk.toString();
    });
    for (let attempt = 0; attempt < 300; attempt++) {
        if (host.exitCode !== null) throw new Error(logs);
        try {
            if ((await fetch(`${baseUrl}/catalog`)).ok) return;
        } catch {
            /* Startup may still be compiling. */
        }
        await setTimeout(200);
    }
    throw new Error(`ASP sample did not start: ${logs}`);
}, 90000);
afterAll(async () => {
    if (!host || host.exitCode !== null) return;
    const stopped = new Promise<void>((resolve) =>
        host.once("exit", () => resolve()),
    );
    host.kill("SIGTERM");
    await stopped;
});
it("consumes the actual generated CLR catalog, compiles topology and decodes ASP diagnostics", async () => {
    const client = createBackflowClient({ baseUrl });
    const catalog = await client.catalog();
    const addition = catalog.nodes.find((node) => node.id === "math.add");
    expect(addition?.isExecutable).toBe(true);
    expect(
        addition?.ports.some((port) => port.clrType === "System.Int32"),
    ).toBe(true);
    const graph = {
        nodes: [
            { id: "first", catalogId: "math.add" },
            { id: "second", catalogId: "math.add" },
        ],
        edges: [
            {
                sourceNodeId: "first",
                sourcePort: "sum",
                destinationNodeId: "second",
                destinationPort: "left",
                destinationIndex: null,
            },
        ],
    };
    expect(await client.validate(graph)).toEqual({ isValid: true, errors: [] });
    expect(await client.build(graph)).toEqual({ graph });
    const invalid = await client.validate({
        nodes: [{ id: "no", catalogId: "not-published" }],
        edges: [],
    });
    expect(invalid.errors[0]?.code).toBe(BackflowErrorCode.UnknownNode);
    await expect(
        client
            .withExecution({ baseUrl, executionEnabled: true })
            .submit({ graph, inputs: [] }),
    ).rejects.toBeInstanceOf(BackflowHttpError);
});
