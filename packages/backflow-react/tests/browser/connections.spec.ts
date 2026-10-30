import { expect, test, type Locator, type Page } from "@playwright/test";
import type {} from "./fixture.js";

// Native handle hit testing and pointer gestures require browser layout, not jsdom.
const cases = [
    {
        name: "scalar",
        url: "/",
        sourcePort: "output",
        destinationPort: "input",
        destinationIndex: null,
    },
    {
        name: "quoted port names",
        url: "/?special",
        sourcePort: 'output "quoted" [slot] \\ %',
        destinationPort: 'input "quoted" [slot] \\ %',
        destinationIndex: null,
    },
    {
        name: "indexed array",
        url: "/?array",
        sourcePort: "output",
        destinationPort: "input",
        destinationIndex: 0,
    },
] as const;

for (const scenario of cases) {
    test(`pointer connects ${scenario.name} after host connect/disconnect and controlled rerenders`, async ({
        page,
    }) => {
        const errors: string[] = [];
        page.on("pageerror", (error) =>
            errors.push(error.stack ?? error.message),
        );
        page.on("console", (message) => {
            if (message.type() === "error" || message.type() === "warning")
                errors.push(message.text());
        });
        await page.addInitScript(() => {
            window.backflowBrowserErrors = [];
            window.addEventListener("error", (event) =>
                window.backflowBrowserErrors.push(
                    event.error instanceof Error
                        ? (event.error.stack ?? event.message)
                        : `${event.message}; documentChanges=${window.backflowTest?.documentChanges}; edges=${window.backflowTest?.document.graph.edges.length}`,
                ),
            );
            window.addEventListener("unhandledrejection", (event) => {
                const reason: unknown = event.reason;
                window.backflowBrowserErrors.push(
                    reason instanceof Error
                        ? (reason.stack ?? reason.message)
                        : String(reason),
                );
            });
        });
        try {
            await page.goto(scenario.url);
            await expect
                .poll(() =>
                    page.evaluate(() => Boolean(window.backflowTest?.instance)),
                )
                .toBe(true);
            const source = page
                .getByTestId("rf__node-first")
                .getByLabel(`${scenario.sourcePort} output`, { exact: true });
            const targetLabel = `${scenario.destinationPort}${scenario.destinationIndex === null ? "" : `[${scenario.destinationIndex}]`} input`;
            const target = page
                .getByTestId("rf__node-second")
                .getByLabel(targetLabel, { exact: true });
            await expect(source).toBeVisible();
            await expect(target).toBeVisible();
            await expect
                .poll(() =>
                    page.evaluate(() => {
                        const instance = window.backflowTest.instance;
                        const nodes = instance?.getNodes() ?? [];
                        return (
                            nodes.length === 2 &&
                            nodes.every((node) => {
                                const bounds = instance?.getInternalNode(
                                    node.id,
                                )?.internals.handleBounds;
                                return (
                                    (node.measured?.width ?? 0) > 0 &&
                                    (bounds?.source?.length ?? 0) > 0 &&
                                    (bounds?.target?.length ?? 0) > 0
                                );
                            })
                        );
                    }),
                )
                .toBe(true);
            await page.evaluate(() =>
                window.backflowTest.connectFirstToSecond(),
            );
            await expect(page.locator(".react-flow__edge-path")).toHaveCount(1);
            await page.evaluate(() => window.backflowTest.disconnect());
            await expect(page.locator(".react-flow__edge-path")).toHaveCount(0);
            await page.evaluate(() => window.backflowTest.rerender());
            await pointerConnect(page, source, target);
            await expect(page.locator(".react-flow__edge-path")).toHaveCount(1);
            expect(
                await page.evaluate(
                    () => window.backflowTest.document.graph.edges,
                ),
            ).toEqual([
                {
                    sourceNodeId: "first",
                    sourcePort: scenario.sourcePort,
                    destinationNodeId: "second",
                    destinationPort: scenario.destinationPort,
                    destinationIndex: scenario.destinationIndex,
                },
            ]);
            await page.evaluate(() => window.backflowTest.disconnect());
            await expect(page.locator(".react-flow__edge-path")).toHaveCount(0);
            await pointerConnect(page, target, source);
            await expect(page.locator(".react-flow__edge-path")).toHaveCount(1);
            expect(
                await page.evaluate(
                    () => window.backflowTest.document.graph.edges,
                ),
            ).toEqual([
                {
                    sourceNodeId: "first",
                    sourcePort: scenario.sourcePort,
                    destinationNodeId: "second",
                    destinationPort: scenario.destinationPort,
                    destinationIndex: scenario.destinationIndex,
                },
            ]);
            await page.waitForTimeout(150);
        } finally {
            await test.info().attach("canvas-state", {
                body: JSON.stringify(
                    await page.evaluate(() => ({
                        graph: window.backflowTest?.document.graph,
                        nodes: window.backflowTest?.instance
                            ?.getNodes()
                            .map((node) => ({
                                id: node.id,
                                measured: node.measured,
                                bounds: window.backflowTest.instance?.getInternalNode(
                                    node.id,
                                )?.internals.handleBounds,
                            })),
                    })),
                ),
                contentType: "application/json",
            });
            expect([
                ...errors,
                ...(await page.evaluate(
                    () => window.backflowBrowserErrors ?? [],
                )),
            ]).toEqual([]);
        }
    });
}

async function pointerConnect(
    page: Page,
    source: Locator,
    target: Locator,
): Promise<void> {
    const from = await source.boundingBox();
    const to = await target.boundingBox();
    if (!from || !to) throw new Error("Handles are not laid out");
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(
        from.x + from.width / 2 + 30,
        from.y + from.height / 2,
        { steps: 3 },
    );
    await expect(page.locator(".react-flow__connection-path")).toHaveCount(1);
    await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, {
        steps: 12,
    });
    await expect(target).toHaveClass(/valid/);
    await page.mouse.up();
}
