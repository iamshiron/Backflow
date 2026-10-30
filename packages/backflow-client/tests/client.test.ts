import { describe, expect, it, vi } from "vitest";
import {
    BackflowHttpError,
    createBackflowClient,
    BackflowErrorCode,
} from "../src/index.js";
const graph = { nodes: [], edges: [] };
describe("ASP client boundary", () => {
    it("propagates cancellation and refreshed authentication", async () => {
        const transport = vi
            .fn<typeof fetch>()
            .mockResolvedValue(new Response('{"nodes":[]}'));
        const signal = new AbortController().signal;
        const client = createBackflowClient({
            baseUrl: "/backflow/",
            fetch: transport,
            requestInit: async () => ({
                headers: { Authorization: "Bearer fresh" },
                credentials: "include",
            }),
        });
        await client.catalog(signal);
        const [url, init] = transport.mock.calls[0]!;
        expect(url).toBe("/backflow/catalog");
        expect(init?.signal).toBe(signal);
        expect(new Headers(init?.headers).get("Authorization")).toBe(
            "Bearer fresh",
        );
        expect(init?.credentials).toBe("include");
    });
    it("returns actual validation diagnostics on HTTP 400", async () => {
        const failure = {
            isValid: false,
            errors: [
                {
                    code: BackflowErrorCode.InvalidGraph,
                    message: "Cycle",
                    nodeId: null,
                    edgeIndex: null,
                },
            ],
        };
        const client = createBackflowClient({
            baseUrl: "/",
            fetch: vi
                .fn<typeof fetch>()
                .mockImplementation(
                    async () =>
                        new Response(JSON.stringify(failure), { status: 400 }),
                ),
        });
        expect(await client.validate(graph)).toEqual(failure);
        await expect(client.build(graph)).rejects.toBeInstanceOf(
            BackflowHttpError,
        );
    });
    it("rejects malformed success contracts and preserves authentication failures", async () => {
        const transport = vi
            .fn<typeof fetch>()
            .mockResolvedValueOnce(new Response("{}"))
            .mockResolvedValueOnce(new Response("Denied", { status: 401 }));
        const client = createBackflowClient({ baseUrl: "/", fetch: transport });
        await expect(client.catalog()).rejects.toThrow(
            "generated Backflow schema",
        );
        await expect(client.catalog()).rejects.toMatchObject({
            status: 401,
            body: "Denied",
        });
    });
    it("does not swallow aborts or execute as a side effect of build", async () => {
        const abort = new DOMException("Cancelled", "AbortError");
        const transport = vi.fn<typeof fetch>().mockRejectedValue(abort);
        const client = createBackflowClient({ baseUrl: "/", fetch: transport });
        await expect(client.build(graph)).rejects.toBe(abort);
        expect(transport.mock.calls[0]?.[0]).toBe("/graphs");
    });
    it("only accepts a scheduler-issued 202 receipt with a job ID", async () => {
        const transport = vi
            .fn<typeof fetch>()
            .mockResolvedValueOnce(
                new Response('{"jobId":"host-job"}', { status: 202 }),
            )
            .mockResolvedValueOnce(new Response("{}", { status: 202 }));
        const options = {
            baseUrl: "/api",
            fetch: transport,
            executionEnabled: true as const,
        };
        const execution = createBackflowClient(options).withExecution(options);
        expect(await execution.submit({ graph, inputs: [] })).toEqual({
            jobId: "host-job",
        });
        await expect(execution.submit({ graph, inputs: [] })).rejects.toThrow();
        expect(transport.mock.calls[0]?.[0]).toBe("/api/executions");
    });
});
