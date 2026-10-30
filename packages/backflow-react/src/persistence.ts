import Ajv from "ajv";
import { backflowSchemas } from "@shiron/backflow-client";
import type { BackflowDocument } from "./document.js";

const validate = new Ajv({
    strict: false,
    allowUnionTypes: true,
}).compile<BackflowDocument>({
    type: "object",
    required: ["graph", "inputs", "positions", "configuration"],
    additionalProperties: false,
    properties: {
        graph: backflowSchemas.BackflowGraph,
        inputs: { type: "array", items: backflowSchemas.BackflowInput },
        positions: {
            type: "object",
            additionalProperties: {
                type: "object",
                required: ["x", "y"],
                additionalProperties: false,
                properties: { x: { type: "number" }, y: { type: "number" } },
            },
        },
        configuration: { type: "object" },
    },
});
/** Host persistence codec; performs structural checks, not authoritative DAG validation. */
export function parseDocument(json: string): BackflowDocument {
    const value: unknown = JSON.parse(json);
    if (!validate(value)) throw new Error("Invalid Backflow editor document");
    return value;
}
export function serializeDocument(document: BackflowDocument): string {
    if (!validate(document))
        throw new Error("Invalid Backflow editor document");
    return JSON.stringify(document);
}
