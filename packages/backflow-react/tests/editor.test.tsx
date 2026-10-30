// @vitest-environment jsdom
import { render, cleanup, screen, fireEvent } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
    BackflowPortDirection,
    type BackflowCatalogDescriptor,
} from "@shiron/backflow-client";
import { BackflowEditor, handleId } from "../src/BackflowEditor.js";
import { createDocument, addNode } from "../src/document.js";
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

const catalog: BackflowCatalogDescriptor = {
    nodes: [
        {
            id: "real-declaration",
            isExecutable: false,
            configurationSchema: null,
            ports: [
                {
                    name: "value",
                    direction: BackflowPortDirection.Output,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: true,
                    minimumCount: null,
                    maximumCount: null,
                },
            ],
        },
    ],
};
it("supports typed host rendering, handle colors and local read-only selection", () => {
    const doc = addNode(createDocument(), catalog, "real-declaration", "a");
    const onSelectionChange = vi.fn<(ids: string[]) => void>();
    render(
        <BackflowEditor
            document={doc}
            catalog={catalog}
            readOnly
            onSelectionChange={onSelectionChange}
            renderNode={({ id, descriptor }) => (
                <span>
                    {id}:{descriptor.id}
                </span>
            )}
            portStyle={() => ({ background: "rgb(1, 2, 3)" })}
        />,
    );
    expect(screen.getByText("a:real-declaration")).toBeDefined();
    expect(
        (document.querySelector(".react-flow__handle") as HTMLElement).style
            .background,
    ).toBe("rgb(1, 2, 3)");
    fireEvent.click(screen.getByTestId("rf__node-a"));
    expect(onSelectionChange).toHaveBeenCalledWith(["a"]);
    expect(
        screen.getByTestId("rf__node-a").classList.contains("selected"),
    ).toBe(true);
});
it("passes actual catalog source ports to host edge styling without changing topology", () => {
    const doc = addNode(
        addNode(createDocument(), catalog, "real-declaration", "a"),
        catalog,
        "real-declaration",
        "b",
    );
    const edge = {
        sourceNodeId: "a",
        sourcePort: "value",
        destinationNodeId: "b",
        destinationPort: "value",
        destinationIndex: null,
    };
    doc.graph.edges = [edge];
    const edgeStyle = vi.fn(() => ({ stroke: "rgb(10, 20, 30)" }));
    render(
        <BackflowEditor
            document={doc}
            catalog={catalog}
            edgeStyle={edgeStyle}
        />,
    );
    expect(edgeStyle).toHaveBeenCalledWith(edge, catalog.nodes[0]?.ports[0]);
    expect(doc.graph.edges).toEqual([edge]);
});
it("restores a host default viewport without an automatic fit", () => {
    render(
        <BackflowEditor
            document={createDocument()}
            catalog={{ nodes: [] }}
            defaultViewport={{ x: 30, y: 40, zoom: 0.8 }}
        />,
    );
    expect(
        (document.querySelector(".react-flow__viewport") as HTMLElement).style
            .transform,
    ).toBe("translate(30px,40px) scale(0.8)");
});
