/**
 * Core DAG and geometric algorithms for ProcessMap.
 * 100% synchronized with ZeroUI.Core.Process.ProcessFlowModels
 */

export type ProcessNodeType = 'Step' | 'Decision' | 'SubProcess' | 'End';
export type ProcessNodeStatus = 'Default' | 'Active' | 'Completed' | 'Warning' | 'Error' | 'Disabled';

export interface ProcessFlowNode {
  id: string;
  laneId?: string | null;
  label: string;
  subLabel?: string;
  type: ProcessNodeType;
  status: ProcessNodeStatus;
  x: number;
  y: number;
  width: number;
  height: number;
  icon?: string;
}

export interface ProcessFlowLane {
  id: string;
  title: string;
  x: number;
  y: number;
  width: number;
  height: number;
  headerColorHex?: string;
  backgroundColorHex?: string;
}

export interface ProcessFlowConnection {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  label?: string;
  strokeColorHex?: string;
  isDashed?: boolean;
}

export type ProcessNodeAlignment = 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom';

export class ProcessMapEngine {
  /**
   * Checks if a point is within lane boundaries.
   */
  public static laneContainsPoint(lane: ProcessFlowLane, px: number, py: number): boolean {
    return px >= lane.x && px <= lane.x + lane.width &&
           py >= lane.y && py <= lane.y + lane.height;
  }

  /**
   * Clamps lane dimensions to prevent invalid inverted boxes.
   */
  public static clampLane(lane: ProcessFlowLane, minW = 200, minH = 120): ProcessFlowLane {
    return {
      ...lane,
      width: Math.max(minW, lane.width),
      height: Math.max(minH, lane.height),
    };
  }

  /**
   * Adjusts all lanes to neatly enclose their member nodes with designated paddings.
   */
  public static fitLanesToNodes(
    lanes: ProcessFlowLane[],
    nodes: ProcessFlowNode[],
    padding = 36,
    headerPadding = 48
  ): ProcessFlowLane[] {
    return lanes.map((lane) => {
      const memberNodes = nodes.filter(
        (n) => n.laneId === lane.id || (
          n.x + n.width / 2 >= lane.x &&
          n.x + n.width / 2 <= lane.x + lane.width &&
          n.y + n.height / 2 >= lane.y &&
          n.y + n.height / 2 <= lane.y + lane.height
        )
      );

      if (memberNodes.length === 0) return lane;

      const minX = Math.min(...memberNodes.map((n) => n.x));
      const minY = Math.min(...memberNodes.map((n) => n.y));
      const maxX = Math.max(...memberNodes.map((n) => n.x + n.width));
      const maxY = Math.max(...memberNodes.map((n) => n.y + n.height));

      return {
        ...lane,
        x: Math.max(10, minX - padding),
        y: Math.max(10, minY - headerPadding),
        width: Math.max(260, maxX - minX + padding * 2),
        height: Math.max(160, maxY - minY + headerPadding + padding),
      };
    });
  }

  /**
   * Creates a new bounding swimlane grouping all selected nodes.
   */
  public static createLaneFromSelection(
    nodes: ProcessFlowNode[],
    selectedNodeIds: string[],
    title = 'Swimlane Frame',
    padding = 36,
    headerPadding = 48
  ): { newLane: ProcessFlowLane; updatedNodes: ProcessFlowNode[] } | null {
    const selectedNodes = nodes.filter((n) => selectedNodeIds.includes(n.id));
    if (selectedNodes.length === 0) return null;

    const minX = Math.min(...selectedNodes.map((n) => n.x));
    const minY = Math.min(...selectedNodes.map((n) => n.y));
    const maxX = Math.max(...selectedNodes.map((n) => n.x + n.width));
    const maxY = Math.max(...selectedNodes.map((n) => n.y + n.height));

    const laneId = `lane_${Date.now()}`;
    const newLane: ProcessFlowLane = {
      id: laneId,
      title,
      x: Math.max(10, minX - padding),
      y: Math.max(10, minY - headerPadding),
      width: Math.max(300, maxX - minX + padding * 2),
      height: Math.max(180, maxY - minY + headerPadding + padding),
      headerColorHex: '#3b82f6',
      backgroundColorHex: 'rgba(59, 130, 246, 0.05)',
    };

    const updatedNodes = nodes.map((n) =>
      selectedNodeIds.includes(n.id) ? { ...n, laneId } : n
    );

    return { newLane, updatedNodes };
  }

  /**
   * Automatically organizes nodes in a clear, layered DAG layout and adjusts lane bounds.
   * Port of ZeroUI DAG Layered Algorithm.
   */
  public static autoArrangeLayout(
    nodes: ProcessFlowNode[],
    connections: ProcessFlowConnection[],
    lanes: ProcessFlowLane[],
    horizontal = true,
    nodeSpacingX = 80,
    nodeSpacingY = 50
  ): { nodes: ProcessFlowNode[]; lanes: ProcessFlowLane[] } {
    if (nodes.length === 0) return { nodes, lanes };

    const nodesCopy = nodes.map((n) => ({ ...n }));
    const lanesCopy = lanes.map((l) => ({ ...l }));

    // Group nodes by LaneId
    const laneGroups = new Map<string, ProcessFlowNode[]>();
    for (const node of nodesCopy) {
      const key = node.laneId || '__unassigned__';
      if (!laneGroups.has(key)) laneGroups.set(key, []);
      laneGroups.get(key)!.push(node);
    }

    let currentLaneOffsetY = 30;
    const currentLaneOffsetX = 30;

    for (const [laneKey, groupNodes] of laneGroups.entries()) {
      const nodeIds = new Set(groupNodes.map((n) => n.id));
      const groupConns = connections.filter(
        (c) => nodeIds.has(c.sourceNodeId) && nodeIds.has(c.targetNodeId)
      );

      // In-Degree calculation
      const inDegrees = new Map<string, number>();
      for (const n of groupNodes) inDegrees.set(n.id, 0);
      for (const c of groupConns) {
        inDegrees.set(c.targetNodeId, (inDegrees.get(c.targetNodeId) || 0) + 1);
      }

      // Topological layered grouping
      const layers: ProcessFlowNode[][] = [];
      const assigned = new Set<string>();

      let currentLayer = groupNodes.filter((n) => inDegrees.get(n.id) === 0);
      if (currentLayer.length === 0 && groupNodes.length > 0) {
        currentLayer = [groupNodes[0]];
      }

      while (currentLayer.length > 0) {
        layers.push(currentLayer);
        for (const n of currentLayer) assigned.add(n.id);

        const nextLayer: ProcessFlowNode[] = [];
        for (const n of currentLayer) {
          const targets = groupConns
            .filter((c) => c.sourceNodeId === n.id)
            .map((c) => groupNodes.find((gn) => gn.id === c.targetNodeId))
            .filter(
              (gn): gn is ProcessFlowNode =>
                gn !== undefined && !assigned.has(gn.id) && !nextLayer.includes(gn)
            );
          for (const t of targets) nextLayer.push(t);
        }

        if (nextLayer.length === 0 && assigned.size < groupNodes.length) {
          const unassigned = groupNodes.find((gn) => !assigned.has(gn.id));
          if (unassigned) nextLayer.push(unassigned);
        }

        currentLayer = nextLayer;
      }

      const startX = horizontal ? 60 : currentLaneOffsetX;
      const startY = horizontal ? currentLaneOffsetY + 50 : 60;
      let maxGroupX = startX;
      let maxGroupY = startY;

      if (horizontal) {
        let curX = startX;
        for (const layer of layers) {
          const layerMaxW = Math.max(...layer.map((n) => n.width));
          let curY = startY;

          for (const n of layer) {
            n.x = curX;
            n.y = curY;
            curY += n.height + nodeSpacingY;
            if (n.x + n.width > maxGroupX) maxGroupX = n.x + n.width;
            if (n.y + n.height > maxGroupY) maxGroupY = n.y + n.height;
          }
          curX += layerMaxW + nodeSpacingX;
        }
      } else {
        let curY = startY;
        for (const layer of layers) {
          const layerMaxH = Math.max(...layer.map((n) => n.height));
          let curX = startX;

          for (const n of layer) {
            n.x = curX;
            n.y = curY;
            curX += n.width + nodeSpacingX;
            if (n.x + n.width > maxGroupX) maxGroupX = n.x + n.width;
            if (n.y + n.height > maxGroupY) maxGroupY = n.y + n.height;
          }
          curY += layerMaxH + nodeSpacingY;
        }
      }

      const matchedLane = lanesCopy.find((l) => l.id === laneKey);
      if (matchedLane) {
        matchedLane.x = Math.max(20, startX - 40);
        matchedLane.y = Math.max(20, currentLaneOffsetY);
        matchedLane.width = Math.max(400, maxGroupX - startX + 80);
        matchedLane.height = Math.max(200, maxGroupY - currentLaneOffsetY + 40);

        currentLaneOffsetY = matchedLane.y + matchedLane.height + 40;
      } else {
        currentLaneOffsetY = maxGroupY + 60;
      }
    }

    return { nodes: nodesCopy, lanes: lanesCopy };
  }

  /**
   * Aligns selected nodes horizontally or vertically.
   */
  public static alignNodes(
    nodes: ProcessFlowNode[],
    nodeIds: string[],
    alignment: ProcessNodeAlignment
  ): ProcessFlowNode[] {
    const selected = nodes.filter((n) => nodeIds.includes(n.id));
    if (selected.length < 2) return nodes;

    const minX = Math.min(...selected.map((n) => n.x));
    const maxX = Math.max(...selected.map((n) => n.x + n.width));
    const minY = Math.min(...selected.map((n) => n.y));
    const maxY = Math.max(...selected.map((n) => n.y + n.height));
    const centerX = (minX + maxX) / 2;
    const centerY = (minY + maxY) / 2;

    return nodes.map((node) => {
      if (!nodeIds.includes(node.id)) return node;
      const n = { ...node };
      switch (alignment) {
        case 'left':
          n.x = minX;
          break;
        case 'right':
          n.x = maxX - n.width;
          break;
        case 'center':
          n.x = centerX - n.width / 2;
          break;
        case 'top':
          n.y = minY;
          break;
        case 'bottom':
          n.y = maxY - n.height;
          break;
        case 'middle':
          n.y = centerY - n.height / 2;
          break;
      }
      return n;
    });
  }

  /**
   * Distributes selected nodes evenly across space.
   */
  public static distributeNodes(
    nodes: ProcessFlowNode[],
    nodeIds: string[],
    horizontal = true
  ): ProcessFlowNode[] {
    const selected = nodes.filter((n) => nodeIds.includes(n.id));
    if (selected.length <= 2) return nodes;

    const sorted = [...selected].sort((a, b) => (horizontal ? a.x - b.x : a.y - b.y));
    const first = sorted[0];
    const last = sorted[sorted.length - 1];

    if (horizontal) {
      const totalSpan = (last.x + last.width) - first.x;
      const totalNodesWidth = sorted.reduce((sum, n) => sum + n.width, 0);
      const gap = (totalSpan - totalNodesWidth) / (sorted.length - 1);

      let currentX = first.x;
      const posMap = new Map<string, number>();
      for (const n of sorted) {
        posMap.set(n.id, currentX);
        currentX += n.width + gap;
      }
      return nodes.map((n) => (posMap.has(n.id) ? { ...n, x: posMap.get(n.id)! } : n));
    } else {
      const totalSpan = (last.y + last.height) - first.y;
      const totalNodesHeight = sorted.reduce((sum, n) => sum + n.height, 0);
      const gap = (totalSpan - totalNodesHeight) / (sorted.length - 1);

      let currentY = first.y;
      const posMap = new Map<string, number>();
      for (const n of sorted) {
        posMap.set(n.id, currentY);
        currentY += n.height + gap;
      }
      return nodes.map((n) => (posMap.has(n.id) ? { ...n, y: posMap.get(n.id)! } : n));
    }
  }
}
