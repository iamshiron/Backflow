import { defineConfig } from "@playwright/test";

export default defineConfig({
    testDir: "packages/backflow-react/tests/browser",
    testMatch: "*.spec.ts",
    fullyParallel: false,
    workers: 1,
    use: { trace: "retain-on-failure" },
    projects: [
        { name: "development", use: { baseURL: "http://127.0.0.1:5189" } },
        { name: "production", use: { baseURL: "http://127.0.0.1:5190" } },
    ],
    webServer: [
        {
            command:
                "pnpm exec vite --config packages/backflow-react/tests/browser/vite.config.ts --host 127.0.0.1 --port 5189 --strictPort",
            url: "http://127.0.0.1:5189",
            reuseExistingServer: false,
        },
        {
            command:
                "pnpm exec vite build --config packages/backflow-react/tests/browser/vite.config.ts && pnpm exec vite preview --config packages/backflow-react/tests/browser/vite.config.ts --host 127.0.0.1 --port 5190 --strictPort",
            url: "http://127.0.0.1:5190",
            reuseExistingServer: false,
        },
    ],
});
