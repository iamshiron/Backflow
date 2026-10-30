import { useCallback, useEffect, useState } from "react";
import type { Dimensions, NodeDimensionChange } from "@xyflow/react";

export function reconcileMeasurements(
    current: ReadonlyMap<string, Dimensions>,
    nodeIds: readonly string[],
    changes: readonly NodeDimensionChange[],
): ReadonlyMap<string, Dimensions> {
    const live = new Set(nodeIds);
    let next: Map<string, Dimensions> | undefined;
    for (const id of current.keys()) {
        if (live.has(id)) continue;
        next ??= new Map(current);
        next.delete(id);
    }
    for (const change of changes) {
        if (!live.has(change.id) || !change.dimensions) continue;
        const previous = (next ?? current).get(change.id);
        const { width, height } = change.dimensions;
        if (previous?.width === width && previous.height === height) continue;
        next ??= new Map(current);
        next.set(change.id, { width, height });
    }
    return next ?? current;
}

/** ReactFlow layout metadata stays local; it is neither topology nor persisted editor data. */
export function useNodeMeasurements(nodeIds: readonly string[]) {
    const [measurements, setMeasurements] = useState<
        ReadonlyMap<string, Dimensions>
    >(() => new Map());
    useEffect(() => {
        setMeasurements((current) =>
            reconcileMeasurements(current, nodeIds, []),
        );
    }, [nodeIds]);
    const onDimensionsChange = useCallback(
        (changes: readonly NodeDimensionChange[]) => {
            if (!changes.length) return;
            setMeasurements((current) =>
                reconcileMeasurements(current, nodeIds, changes),
            );
        },
        [nodeIds],
    );
    return { measurements, onDimensionsChange };
}
