import { describe, expect, it } from "vitest";
import {
    BackflowPortDirection as Direction,
    type BackflowCatalogDescriptor,
    type BackflowEdge,
    type BackflowPortDescriptor,
} from "@shiron/backflow-client";
import {
    addNode,
    canConnect,
    connect,
    createDocument,
    removeNode,
    setLiteral,
    edgeId,
} from "../src/document.js";
import { parseDocument, serializeDocument } from "../src/persistence.js";
const port = (
    name: string,
    direction: Direction,
    clrType = "System.Int32",
    array = false,
): BackflowPortDescriptor => ({
    name,
    direction,
    clrType,
    schema: { type: "integer" },
    required: true,
    minimumCount: array ? 0 : null,
    maximumCount: array ? 3 : null,
});
const catalog: BackflowCatalogDescriptor = {
    nodes: [
        {
            id: "add",
            isExecutable: true,
            configurationSchema: null,
            ports: [
                port("in", Direction.Input),
                port("out", Direction.Output),
                port("items", Direction.Input, "System.Int32[]", true),
                port("text", Direction.Input, "System.String"),
            ],
        },
    ],
};
const draft = () =>
    ["a", "b", "c"].reduce(
        (doc, id) => addNode(doc, catalog, "add", id),
        createDocument(),
    );
const edge = (
    sourceNodeId = "a",
    destinationNodeId = "b",
    destinationPort = "in",
    destinationIndex: number | null = null,
): BackflowEdge => ({
    sourceNodeId,
    sourcePort: "out",
    destinationNodeId,
    destinationPort,
    destinationIndex,
});
describe("controlled document operations", () => {
    it("preserves document, positions, literals and configuration across load/save", () => {
        const doc = {
            ...setLiteral(draft(), catalog, {
                nodeId: "a",
                portName: "in",
                value: 12,
            }),
            configuration: { a: { mode: "host-value" } },
        };
        expect(parseDocument(serializeDocument(doc))).toEqual(doc);
        expect(draft().inputs).toEqual([]);
    });
    it("rejects malformed persisted coordinates and graph structures", () => {
        expect(() => parseDocument('{"graph":null}')).toThrow();
        expect(() =>
            parseDocument(
                JSON.stringify({
                    ...draft(),
                    positions: { a: { x: "bad", y: 0 } },
                }),
            ),
        ).toThrow();
    });
    it("rejects unknown and duplicate declarations", () => {
        expect(() => addNode(draft(), catalog, "missing", "d")).toThrow();
        expect(() => addNode(draft(), catalog, "add", "a")).toThrow();
    });
    it("checks exact CLR compatibility and directions", () => {
        expect(canConnect(draft(), catalog, edge())).toBe(true);
        expect(canConnect(draft(), catalog, edge("a", "b", "text"))).toBe(
            false,
        );
        expect(
            canConnect(draft(), catalog, { ...edge(), sourcePort: "in" }),
        ).toBe(false);
    });
    it("rejects self and transitive cycles", () => {
        const doc = connect(
            connect(draft(), catalog, edge()),
            catalog,
            edge("b", "c"),
        );
        expect(canConnect(doc, catalog, edge("c", "a"))).toBe(false);
        expect(canConnect(doc, catalog, edge("a", "a"))).toBe(false);
    });
    it("rejects multiple writers and connected literals in either order", () => {
        const doc = connect(draft(), catalog, edge());
        expect(canConnect(doc, catalog, edge("c", "b"))).toBe(false);
        expect(() =>
            setLiteral(doc, catalog, { nodeId: "b", portName: "in", value: 1 }),
        ).toThrow();
        const literal = setLiteral(draft(), catalog, {
            nodeId: "b",
            portName: "in",
            value: 1,
        });
        expect(canConnect(literal, catalog, edge())).toBe(false);
    });
    it("allows indexed array elements, bounds indices and rejects whole-array mixing", () => {
        const doc = connect(draft(), catalog, edge("a", "b", "items", 0));
        expect(canConnect(doc, catalog, edge("c", "b", "items", 1))).toBe(true);
        expect(canConnect(doc, catalog, edge("c", "b", "items", 3))).toBe(
            false,
        );
        expect(canConnect(doc, catalog, edge("c", "b", "items", -1))).toBe(
            false,
        );
        expect(canConnect(doc, catalog, edge("c", "b", "items", null))).toBe(
            false,
        );
    });
    it("cleans edges, literals and editor metadata when deleting nodes", () => {
        const doc = setLiteral(connect(draft(), catalog, edge()), catalog, {
            nodeId: "a",
            portName: "in",
            value: 3,
        });
        const removed = removeNode(doc, "a");
        expect(removed.graph.edges).toEqual([]);
        expect(removed.inputs).toEqual([]);
        expect(removed.positions.a).toBeUndefined();
        expect(doc.graph.nodes).toHaveLength(3);
    });
    it("encodes edge IDs without delimiter collisions", () => {
        expect(edgeId({ ...edge(), sourcePort: "x:y" })).not.toBe(
            edgeId({ ...edge(), sourceNodeId: "a:x", sourcePort: "y" }),
        );
    });
});
