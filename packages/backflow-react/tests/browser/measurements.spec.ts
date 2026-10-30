import { expect, test, type Page } from "@playwright/test";
import type { BrowserHarness } from "./fixture.js";

// Real-browser coverage is necessary for ResizeObserver/layout and pointer dragging;
// jsdom cannot establish either ReactFlow's measured-node lifecycle or resize loops.
test("retains measurements across controlled rerenders, selection, dragging and dynamic content", async ({
    page,
}) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.stack ?? error.message));
    page.on("console", (message) => {
        if (message.type() === "error" || message.type() === "warning")
            errors.push(message.text());
    });
    await page.addInitScript(() => {
        window.backflowBrowserErrors = [];
        window.addEventListener("error", (event) => {
            window.backflowBrowserErrors.push(
                event.error instanceof Error
                    ? (event.error.stack ?? event.message)
                    : event.message,
            );
        });
        window.addEventListener("unhandledrejection", (event) => {
            const reason: unknown = event.reason;
            window.backflowBrowserErrors.push(
                reason instanceof Error
                    ? (reason.stack ?? reason.message)
                    : String(reason),
            );
        });
    });
    await page.goto("/");
    await expect
        .poll(() => page.evaluate(() => Boolean(window.backflowTest?.instance)))
        .toBe(true);
    await expect(page.getByTestId("rf__node-first")).toBeVisible();
    expect(await page.evaluate(() => window.backflowTest.documentChanges)).toBe(
        0,
    );
    await page.evaluate(() => window.backflowTest.rerender());
    await page.getByTestId("content-first").click();
    await dragFirst(page, 95, 55);
    expect([
        ...errors,
        ...(await page.evaluate(() => window.backflowBrowserErrors)),
    ]).toEqual([]);
    await expect
        .poll(() =>
            page.evaluate(
                () => window.backflowTest.document.positions.first?.x,
            ),
        )
        .toBeGreaterThan(120);
    await expect.poll(() => measured(page)).toBe(true);
    const before = await page.getByTestId("rf__node-first").boundingBox();
    await page.evaluate(() => window.backflowTest.expand());
    await expect
        .poll(
            async () =>
                (await page.getByTestId("rf__node-first").boundingBox())
                    ?.height ?? 0,
        )
        .toBeGreaterThan((before?.height ?? 0) + 100);
    await expect
        .poll(() =>
            page.evaluate(() => {
                const node = window.backflowTest.instance?.getNode("first");
                const element = document.querySelector(
                    '[data-testid="rf__node-first"]',
                );
                return (
                    node?.measured?.height ===
                    (element as HTMLElement | null)?.offsetHeight
                );
            }),
        )
        .toBe(true);
    await page.evaluate(() => window.backflowTest.setReadOnly(true));
    const changesBeforeResize = await page.evaluate(
        () => window.backflowTest.documentChanges,
    );
    await page.evaluate(() => window.backflowTest.expand());
    await expect
        .poll(
            async () =>
                (await page.getByTestId("rf__node-first").boundingBox())
                    ?.height ?? 1000,
        )
        .toBeLessThan(150);
    await expect
        .poll(() =>
            page.evaluate(() => {
                const node = window.backflowTest.instance?.getNode("first");
                const element = document.querySelector(
                    '[data-testid="rf__node-first"]',
                );
                return (
                    node?.measured?.height ===
                    (element as HTMLElement | null)?.offsetHeight
                );
            }),
        )
        .toBe(true);
    expect(await page.evaluate(() => window.backflowTest.documentChanges)).toBe(
        changesBeforeResize,
    );
    await expect.poll(() => measured(page)).toBe(true);
    await page.evaluate(() => window.backflowTest.setReadOnly(false));
    await dragFirst(page, 50, 20);
    await page.evaluate(() => window.backflowTest.removeFirst());
    await expect(page.getByTestId("rf__node-first")).toHaveCount(0);
    await page.evaluate(() => window.backflowTest.restoreFirst());
    await expect(page.getByTestId("rf__node-first")).toBeVisible();
    await expect.poll(() => measured(page)).toBe(true);
    await dragFirst(page, 60, 20);
    // Let native ResizeObserver deliveries and React updates settle; capture remains active.
    await page.waitForTimeout(250);
    expect([
        ...errors,
        ...(await page.evaluate(() => window.backflowBrowserErrors)),
    ]).toEqual([]);
    const saved = await page.evaluate(() => window.backflowTest.document);
    expect(JSON.stringify(saved)).not.toContain('"measured"');
});

async function measured(page: Page): Promise<boolean> {
    return page.evaluate(() => {
        const nodes = window.backflowTest.instance?.getNodes() ?? [];
        return (
            nodes.length > 0 &&
            nodes.every(
                (node) =>
                    (node.measured?.width ?? 0) > 0 &&
                    (node.measured?.height ?? 0) > 0,
            )
        );
    });
}
async function dragFirst(page: Page, dx: number, dy: number): Promise<void> {
    const bounds = await page.getByTestId("content-first").boundingBox();
    if (!bounds) throw new Error("Node content is not laid out");
    const x = bounds.x + 35;
    const y = bounds.y + 12;
    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x + dx, y + dy, { steps: 12 });
    await page.mouse.up();
}
// Keep the fixture's Window augmentation available to the test's browser callbacks.
type HarnessContract = BrowserHarness;
export type { HarnessContract };
