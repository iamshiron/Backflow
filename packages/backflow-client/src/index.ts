export * from "./contracts.js";
export { default as backflowSchemas } from "./schemas.json" with { type: "json" };
import schemas from "./schemas.json" with { type: "json" };
import Ajv from "ajv";
import type {
    BackflowCatalogDescriptor,
    BackflowGraph,
    BackflowGraphResult,
    BackflowValidationResult,
    BackflowExecutionSubmission,
    BackflowExecutionAccepted,
} from "./contracts.js";

/** HTTP failures retain the unmodified host response (including validation diagnostics). */
export class BackflowHttpError extends Error {
    constructor(
        public readonly status: number,
        public readonly body: unknown,
    ) {
        super(`Backflow HTTP ${status}`);
        this.name = "BackflowHttpError";
    }
}
export interface BackflowClientOptions {
    baseUrl: string;
    fetch?: typeof globalThis.fetch;
    /** Called for every request; supports refreshable authentication and credentials. */
    requestInit?: () => RequestInit | Promise<RequestInit>;
}
export interface BackflowExecutionOptions extends BackflowClientOptions {
    /** Must only be enabled when the host maps the scheduler-backed route. */
    executionEnabled: true;
}
const validator = new Ajv({ strict: false, allowUnionTypes: true });
const catalogSchema = validator.compile<BackflowCatalogDescriptor>(
    schemas.BackflowCatalogDescriptor,
);
const graphSchema = validator.compile<BackflowGraphResult>(
    schemas.BackflowGraphResult,
);
const validationSchema = validator.compile<BackflowValidationResult>(
    schemas.BackflowValidationResult,
);
const receiptSchema = validator.compile<BackflowExecutionAccepted>(
    schemas.BackflowExecutionAccepted,
);
function checked<T>(
    value: unknown,
    validate: (value: unknown) => value is T,
): T {
    if (!validate(value))
        throw new Error(
            "Host response does not match generated Backflow schema",
        );
    return value;
}
export function createBackflowClient(options: BackflowClientOptions) {
    const transport = options.fetch ?? globalThis.fetch;
    async function request(
        path: string,
        body?: unknown,
        signal?: AbortSignal,
    ): Promise<unknown> {
        const init = await options.requestInit?.();
        const headers = new Headers(init?.headers);
        headers.set("Accept", "application/json");
        if (body !== undefined) headers.set("Content-Type", "application/json");
        const response = await transport(
            `${options.baseUrl.replace(/\/$/, "")}/${path}`,
            {
                ...init,
                method: body === undefined ? "GET" : "POST",
                headers,
                body: body === undefined ? undefined : JSON.stringify(body),
                signal,
            },
        );
        const text = await response.text();
        let value: unknown;
        try {
            value = text.length ? (JSON.parse(text) as unknown) : null;
        } catch {
            throw new BackflowHttpError(response.status, text);
        }
        if (!response.ok) throw new BackflowHttpError(response.status, value);
        return value;
    }
    return {
        catalog: async (signal?: AbortSignal) =>
            checked(await request("catalog", undefined, signal), catalogSchema),
        validate: async (
            graph: BackflowGraph,
            signal?: AbortSignal,
        ): Promise<BackflowValidationResult> => {
            try {
                return checked(
                    await request("validate", graph, signal),
                    validationSchema,
                );
            } catch (error) {
                if (
                    error instanceof BackflowHttpError &&
                    error.status === 400 &&
                    validationSchema(error.body)
                )
                    return error.body;
                throw error;
            }
        },
        /** Compiles topology; does NOT persist or execute it. */
        build: async (graph: BackflowGraph, signal?: AbortSignal) =>
            checked(await request("graphs", graph, signal), graphSchema),
        /** Separate opt-in factory avoids accidentally advertising execution. */
        withExecution: (enabled: BackflowExecutionOptions) => ({
            submit: (
                submission: BackflowExecutionSubmission,
                signal?: AbortSignal,
            ) => {
                if (enabled.executionEnabled !== true)
                    throw new Error("Host execution is not enabled");
                return createExecutionRequest(enabled, submission, signal);
            },
        }),
    };
}
async function createExecutionRequest(
    options: BackflowExecutionOptions,
    submission: BackflowExecutionSubmission,
    signal?: AbortSignal,
): Promise<BackflowExecutionAccepted> {
    const init = await options.requestInit?.();
    const headers = new Headers(init?.headers);
    headers.set("Content-Type", "application/json");
    const response = await (options.fetch ?? globalThis.fetch)(
        `${options.baseUrl.replace(/\/$/, "")}/executions`,
        {
            ...init,
            method: "POST",
            headers,
            body: JSON.stringify(submission),
            signal,
        },
    );
    const text = await response.text();
    let body: unknown;
    try {
        body = text.length ? (JSON.parse(text) as unknown) : null;
    } catch {
        throw new BackflowHttpError(response.status, text);
    }
    if (response.status !== 202)
        throw new BackflowHttpError(response.status, body);
    return checked(body, receiptSchema);
}
export type BackflowClient = ReturnType<typeof createBackflowClient>;

/** Explicit opt-in to the real host scheduler. Does not invent job status or results routes. */
export function createBackflowExecutionClient(
    options: BackflowExecutionOptions,
) {
    return createBackflowClient(options).withExecution(options);
}
