import { describe, expect, it } from "vitest";
import type { Dimensions } from "@xyflow/react";
import { reconcileMeasurements } from "../src/useNodeMeasurements.js";

describe("canvas-local measurements", () => {
    it("retains dimensions across renders and deduplicates repeated observer notifications", () => {
        const empty = new Map<string, Dimensions>();
        const changes = [
            {
                id: "first",
                type: "dimensions" as const,
                dimensions: { width: 240, height: 120 },
            },
        ];
        const measured = reconcileMeasurements(empty, ["first"], changes);
        expect(measured.get("first")).toEqual({ width: 240, height: 120 });
        expect(empty.size).toBe(0);
        expect(reconcileMeasurements(measured, ["first"], changes)).toBe(
            measured,
        );
        expect(reconcileMeasurements(measured, ["first"], [])).toBe(measured);
    });
    it("accepts dynamic content resizing without fixing rendered width or height", () => {
        const original = new Map([["first", { width: 200, height: 100 }]]);
        const next = reconcileMeasurements(
            original,
            ["first"],
            [
                {
                    id: "first",
                    type: "dimensions",
                    dimensions: { width: 200, height: 300 },
                },
            ],
        );
        expect(next.get("first")?.height).toBe(300);
        expect(original.get("first")?.height).toBe(100);
    });
    it("prunes removed nodes and ignores late measurements for deleted IDs", () => {
        const original = new Map([["first", { width: 200, height: 100 }]]);
        const removed = reconcileMeasurements(
            original,
            [],
            [
                {
                    id: "first",
                    type: "dimensions",
                    dimensions: { width: 200, height: 300 },
                },
            ],
        );
        expect(removed.size).toBe(0);
        expect(reconcileMeasurements(removed, ["first"], []).has("first")).toBe(
            false,
        );
    });
    it("ignores notifications without dimensions without triggering another render", () => {
        const empty = new Map<string, Dimensions>();
        expect(
            reconcileMeasurements(
                empty,
                ["first"],
                [{ id: "first", type: "dimensions" }],
            ),
        ).toBe(empty);
    });
});
