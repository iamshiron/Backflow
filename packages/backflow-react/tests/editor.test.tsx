// @vitest-environment jsdom
import { render, cleanup, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { BackflowPortDirection } from "@shiron/backflow-client";
import { BackflowEditor, handleId } from "../src/BackflowEditor.js";
import { createDocument } from "../src/document.js";
// jsdom has no layout engine; only that browser boundary is replaced.
beforeEach(() =>
    vi.stubGlobal(
        "ResizeObserver",
        class {
            observe() {}
            unobserve() {}
            disconnect() {}
        },
    ),
);
afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});
it("renders an actual ReactFlow canvas and host children without installing a toolbar or theme", () => {
    render(
        <div style={{ width: 800, height: 600 }}>
            <BackflowEditor
                document={createDocument()}
                catalog={{ nodes: [] }}
                readOnly
            >
                <span>Host customization</span>
            </BackflowEditor>
        </div>,
    );
    expect(screen.getByText("Host customization")).toBeDefined();
    expect(document.querySelector(".react-flow")).not.toBeNull();
    expect(document.querySelector(".react-flow__controls")).toBeNull();
});
it("keeps literal port names, directions and indexed array slots distinct", () => {
    expect(handleId("port[1]", null, BackflowPortDirection.Input)).not.toBe(
        handleId("port", 1, BackflowPortDirection.Input),
    );
    expect(handleId("value", null, BackflowPortDirection.Input)).not.toBe(
        handleId("value", null, BackflowPortDirection.Output),
    );
});
