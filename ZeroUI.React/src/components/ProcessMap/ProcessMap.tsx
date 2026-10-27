import React, { useState, useRef, useCallback } from 'react';
import {
  ProcessFlowNode,
  ProcessFlowLane,
  ProcessFlowConnection,
  ProcessMapEngine,
} from '../../core/models/ProcessMapModel';
import { ButtonGroup } from '../ButtonGroup/ButtonGroup';
import { ContextMenu, ContextMenuItem } from '../ContextMenu/ContextMenu';
import { MenuIcons } from '../../core/icons/MenuIcons';
import './ProcessMap.css';

export interface ProcessMapProps {
  initialNodes: ProcessFlowNode[];
  initialLanes: ProcessFlowLane[];
  initialConnections: ProcessFlowConnection[];
  title?: string;
  onSave?: (data: {
    nodes: ProcessFlowNode[];
    lanes: ProcessFlowLane[];
    connections: ProcessFlowConnection[];
  }) => void;
  className?: string;
}

type ResizeHandleType = 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w';

export const ProcessMap: React.FC<ProcessMapProps> = ({
  initialNodes,
  initialLanes,
  initialConnections,
  title = 'Industrial DAG Workflow Map',
  onSave,
  className = '',
}) => {
  const [nodes, setNodes] = useState<ProcessFlowNode[]>(initialNodes);
  const [lanes, setLanes] = useState<ProcessFlowLane[]>(initialLanes);
  const [connections] = useState<ProcessFlowConnection[]>(initialConnections);

  const [selectedNodeIds, setSelectedNodeIds] = useState<string[]>([]);
  const [selectedLaneId, setSelectedLaneId] = useState<string | null>(null);
  const [isDesignMode, setIsDesignMode] = useState<boolean>(true);
  const [isDirty, setIsDirty] = useState<boolean>(false);

  // Mouse interaction state
  const svgRef = useRef<SVGSVGElement>(null);
  const [dragState, setDragState] = useState<{
    type: 'none' | 'node' | 'lane_header' | 'lane_resize';
    targetId: string;
    handle?: ResizeHandleType;
    startX: number;
    startY: number;
    initialX: number;
    initialY: number;
    initialW?: number;
    initialH?: number;
    initialChildNodes?: { id: string; x: number; y: number }[];
  }>({
    type: 'none',
    targetId: '',
    startX: 0,
    startY: 0,
    initialX: 0,
    initialY: 0,
  });

  // Context Menu state
  const [contextMenu, setContextMenu] = useState<{
    isOpen: boolean;
    x: number;
    y: number;
    targetType: 'canvas' | 'node' | 'lane';
    targetId?: string;
  }>({
    isOpen: false,
    x: 0,
    y: 0,
    targetType: 'canvas',
  });

  // Convert client coordinates to SVG coordinates
  const getSvgCoordinates = useCallback((clientX: number, clientY: number) => {
    if (!svgRef.current) return { x: 0, y: 0 };
    const rect = svgRef.current.getBoundingClientRect();
    return {
      x: clientX - rect.left,
      y: clientY - rect.top,
    };
  }, []);

  // --- Handlers for Toolbar ---
  const handleAutoArrange = () => {
    const arranged = ProcessMapEngine.autoArrangeLayout(nodes, connections, lanes, true, 80, 50);
    setNodes(arranged.nodes);
    setLanes(arranged.lanes);
    setIsDirty(true);
  };

  const handleFitLanes = () => {
    const fitted = ProcessMapEngine.fitLanesToNodes(lanes, nodes);
    setLanes(fitted);
    setIsDirty(true);
  };

  const handleReset = () => {
    setNodes(initialNodes.map((n) => ({ ...n })));
    setLanes(initialLanes.map((l) => ({ ...l })));
    setIsDirty(false);
  };

  const handleSave = () => {
    onSave?.({ nodes, lanes, connections });
    setIsDirty(false);
  };

  const handleCreateLaneFromSelection = () => {
    if (selectedNodeIds.length === 0) return;
    const res = ProcessMapEngine.createLaneFromSelection(nodes, selectedNodeIds, 'New Swimlane Frame');
    if (res) {
      setLanes((prev) => [...prev, res.newLane]);
      setNodes(res.updatedNodes);
      setSelectedLaneId(res.newLane.id);
      setIsDirty(true);
    }
  };

  // --- Mouse Down Handlers ---
  const handleNodeMouseDown = (e: React.MouseEvent, node: ProcessFlowNode) => {
    if (!isDesignMode) return;
    e.stopPropagation();
    const { x, y } = getSvgCoordinates(e.clientX, e.clientY);

    if (e.shiftKey) {
      setSelectedNodeIds((prev) =>
        prev.includes(node.id) ? prev.filter((id) => id !== node.id) : [...prev, node.id]
      );
    } else {
      setSelectedNodeIds([node.id]);
    }
    setSelectedLaneId(null);

    setDragState({
      type: 'node',
      targetId: node.id,
      startX: x,
      startY: y,
      initialX: node.x,
      initialY: node.y,
    });
  };

  const handleLaneHeaderMouseDown = (e: React.MouseEvent, lane: ProcessFlowLane) => {
    if (!isDesignMode) return;
    e.stopPropagation();
    const { x, y } = getSvgCoordinates(e.clientX, e.clientY);

    setSelectedLaneId(lane.id);
    setSelectedNodeIds([]);

    // Find all nodes in this lane to move together
    const childNodes = nodes
      .filter((n) => n.laneId === lane.id || (
        n.x >= lane.x && n.x <= lane.x + lane.width &&
        n.y >= lane.y && n.y <= lane.y + lane.height
      ))
      .map((n) => ({ id: n.id, x: n.x, y: n.y }));

    setDragState({
      type: 'lane_header',
      targetId: lane.id,
      startX: x,
      startY: y,
      initialX: lane.x,
      initialY: lane.y,
      initialChildNodes: childNodes,
    });
  };

  const handleResizeHandleMouseDown = (
    e: React.MouseEvent,
    lane: ProcessFlowLane,
    handle: ResizeHandleType
  ) => {
    if (!isDesignMode) return;
    e.stopPropagation();
    const { x, y } = getSvgCoordinates(e.clientX, e.clientY);

    setDragState({
      type: 'lane_resize',
      targetId: lane.id,
      handle,
      startX: x,
      startY: y,
      initialX: lane.x,
      initialY: lane.y,
      initialW: lane.width,
      initialH: lane.height,
    });
  };

  // --- Global Mouse Move & Up on SVG ---
  const handleMouseMove = (e: React.MouseEvent) => {
    if (dragState.type === 'none') return;
    const { x, y } = getSvgCoordinates(e.clientX, e.clientY);
    const dx = x - dragState.startX;
    const dy = y - dragState.startY;

    if (dragState.type === 'node') {
      setNodes((prev) =>
        prev.map((n) =>
          n.id === dragState.targetId
            ? { ...n, x: Math.max(10, dragState.initialX + dx), y: Math.max(10, dragState.initialY + dy) }
            : n
        )
      );
      setIsDirty(true);
    } else if (dragState.type === 'lane_header') {
      const newLaneX = Math.max(10, dragState.initialX + dx);
      const newLaneY = Math.max(10, dragState.initialY + dy);
      const actualDx = newLaneX - dragState.initialX;
      const actualDy = newLaneY - dragState.initialY;

      setLanes((prev) =>
        prev.map((l) => (l.id === dragState.targetId ? { ...l, x: newLaneX, y: newLaneY } : l))
      );

      // Move child nodes along with lane
      if (dragState.initialChildNodes) {
        const childMap = new Map(dragState.initialChildNodes.map((c) => [c.id, c]));
        setNodes((prev) =>
          prev.map((n) => {
            if (childMap.has(n.id)) {
              const init = childMap.get(n.id)!;
              return { ...n, x: init.x + actualDx, y: init.y + actualDy };
            }
            return n;
          })
        );
      }
      setIsDirty(true);
    } else if (dragState.type === 'lane_resize' && dragState.handle) {
      const minW = 200;
      const minH = 120;
      const initX = dragState.initialX;
      const initY = dragState.initialY;
      const initW = dragState.initialW!;
      const initH = dragState.initialH!;

      let newX = initX;
      let newY = initY;
      let newW = initW;
      let newH = initH;

      switch (dragState.handle) {
        case 'e':
          newW = Math.max(minW, initW + dx);
          break;
        case 's':
          newH = Math.max(minH, initH + dy);
          break;
        case 'se':
          newW = Math.max(minW, initW + dx);
          newH = Math.max(minH, initH + dy);
          break;
        case 'w': {
          const w = Math.max(minW, initW - dx);
          newX = initX + (initW - w);
          newW = w;
          break;
        }
        case 'n': {
          const h = Math.max(minH, initH - dy);
          newY = initY + (initH - h);
          newH = h;
          break;
        }
        case 'nw': {
          const w = Math.max(minW, initW - dx);
          const h = Math.max(minH, initH - dy);
          newX = initX + (initW - w);
          newY = initY + (initH - h);
          newW = w;
          newH = h;
          break;
        }
        case 'ne': {
          const h = Math.max(minH, initH - dy);
          newY = initY + (initH - h);
          newW = Math.max(minW, initW + dx);
          newH = h;
          break;
        }
        case 'sw': {
          const w = Math.max(minW, initW - dx);
          newX = initX + (initW - w);
          newW = w;
          newH = Math.max(minH, initH + dy);
          break;
        }
      }

      setLanes((prev) =>
        prev.map((l) => (l.id === dragState.targetId ? { ...l, x: newX, y: newY, width: newW, height: newH } : l))
      );
      setIsDirty(true);
    }
  };

  const handleMouseUp = () => {
    if (dragState.type !== 'none') {
      setDragState({ type: 'none', targetId: '', startX: 0, startY: 0, initialX: 0, initialY: 0 });
    }
  };

  // --- Context Menu Handlers ---
  const handleContextMenu = (
    e: React.MouseEvent,
    targetType: 'canvas' | 'node' | 'lane',
    targetId?: string
  ) => {
    e.preventDefault();
    e.stopPropagation();
    setContextMenu({
      isOpen: true,
      x: e.clientX,
      y: e.clientY,
      targetType,
      targetId,
    });
  };

  const contextMenuItems: ContextMenuItem[] = [
    ...(selectedNodeIds.length > 0
      ? [
          {
            id: 'create_lane',
            label: 'Create Frame from Selection',
            icon: MenuIcons.Frame,
            onClick: handleCreateLaneFromSelection,
          },
        ]
      : []),
    {
      id: 'auto_arrange',
      label: 'Auto-Arrange DAG Flow',
      icon: MenuIcons.AutoLayout,
      onClick: handleAutoArrange,
    },
    {
      id: 'fit_lanes',
      label: 'Fit All Frames to Nodes',
      icon: MenuIcons.FitToContent,
      onClick: handleFitLanes,
    },
    ...(contextMenu.targetType === 'lane' && contextMenu.targetId
      ? [
          {
            id: 'delete_lane',
            label: 'Delete Swimlane Frame',
            icon: MenuIcons.Delete,
            danger: true,
            onClick: () => {
              setLanes((prev) => prev.filter((l) => l.id !== contextMenu.targetId));
              setSelectedLaneId(null);
              setIsDirty(true);
            },
          },
        ]
      : []),
    ...(contextMenu.targetType === 'node' && contextMenu.targetId
      ? [
          {
            id: 'delete_node',
            label: 'Delete Node',
            icon: MenuIcons.Delete,
            danger: true,
            onClick: () => {
              setNodes((prev) => prev.filter((n) => n.id !== contextMenu.targetId));
              setSelectedNodeIds((prev) => prev.filter((id) => id !== contextMenu.targetId));
              setIsDirty(true);
            },
          },
        ]
      : []),
  ];

  return (
    <div className={`zero-processmap-container ${className}`}>
      {/* Embedded Top Control Bar */}
      <div className="zero-processmap-toolbar">
        <span className="zero-processmap-title-badge">{title}</span>

        <ButtonGroup
          sizeMode="AutoFit"
          items={[
            {
              id: 'mode_view',
              label: 'View',
              icon: MenuIcons.View,
              isSelected: !isDesignMode,
              onClick: () => setIsDesignMode(false),
            },
            {
              id: 'mode_design',
              label: 'Design',
              icon: MenuIcons.Edit,
              isSelected: isDesignMode,
              onClick: () => setIsDesignMode(true),
            },
            {
              id: 'btn_save',
              label: 'Save',
              icon: MenuIcons.Save,
              showBadgeDot: isDirty,
              badgeColor: '#ef4444',
              onClick: handleSave,
            },
            {
              id: 'btn_auto_layout',
              label: 'Auto-Layout',
              icon: MenuIcons.AutoLayout,
              onClick: handleAutoArrange,
            },
            {
              id: 'btn_reset',
              label: 'Reset',
              icon: MenuIcons.Refresh,
              onClick: handleReset,
            },
          ]}
        />
      </div>

      {/* SVG Process Canvas */}
      <svg
        ref={svgRef}
        className={`zero-processmap-canvas ${dragState.type !== 'none' ? 'is-dragging' : ''}`}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onContextMenu={(e) => handleContextMenu(e, 'canvas')}
        onClick={() => {
          setSelectedNodeIds([]);
          setSelectedLaneId(null);
        }}
      >
        <defs>
          <marker
            id="zero-arrow"
            viewBox="0 0 10 10"
            refX="6"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path d="M 0 1 L 10 5 L 0 9 z" fill="var(--zero-primary)" />
          </marker>
        </defs>

        {/* 1. Swimlanes */}
        {lanes.map((lane) => {
          const isSelected = selectedLaneId === lane.id;
          const handleSize = 8;
          const half = handleSize / 2;

          return (
            <g
              key={lane.id}
              className={`zero-processmap-lane ${isSelected ? 'is-selected' : ''}`}
              onClick={(e) => {
                e.stopPropagation();
                setSelectedLaneId(lane.id);
                setSelectedNodeIds([]);
              }}
              onContextMenu={(e) => handleContextMenu(e, 'lane', lane.id)}
            >
              <rect
                className="zero-processmap-lane-rect"
                x={lane.x}
                y={lane.y}
                width={lane.width}
                height={lane.height}
              />

              {/* Lane Header */}
              <g
                className="zero-processmap-lane-header"
                onMouseDown={(e) => handleLaneHeaderMouseDown(e, lane)}
              >
                <rect
                  className="zero-processmap-lane-header-bg"
                  x={lane.x + 8}
                  y={lane.y + 8}
                  width={lane.width - 16}
                  height={28}
                />
                <text
                  className="zero-processmap-lane-header-title"
                  x={lane.x + 20}
                  y={lane.y + 26}
                >
                  {lane.title}
                </text>
              </g>

              {/* 8 Resize Handles when selected in Design Mode */}
              {isSelected && isDesignMode && (
                <g className="zero-processmap-lane-handles">
                  <rect
                    className="zero-processmap-resize-handle handle-nw"
                    x={lane.x - half}
                    y={lane.y - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'nw')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-n"
                    x={lane.x + lane.width / 2 - half}
                    y={lane.y - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'n')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-ne"
                    x={lane.x + lane.width - half}
                    y={lane.y - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'ne')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-e"
                    x={lane.x + lane.width - half}
                    y={lane.y + lane.height / 2 - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'e')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-se"
                    x={lane.x + lane.width - half}
                    y={lane.y + lane.height - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'se')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-s"
                    x={lane.x + lane.width / 2 - half}
                    y={lane.y + lane.height - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 's')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-sw"
                    x={lane.x - half}
                    y={lane.y + lane.height - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'sw')}
                  />
                  <rect
                    className="zero-processmap-resize-handle handle-w"
                    x={lane.x - half}
                    y={lane.y + lane.height / 2 - half}
                    width={handleSize}
                    height={handleSize}
                    onMouseDown={(e) => handleResizeHandleMouseDown(e, lane, 'w')}
                  />
                </g>
              )}
            </g>
          );
        })}

        {/* 2. Connections */}
        {connections.map((conn) => {
          const source = nodes.find((n) => n.id === conn.sourceNodeId);
          const target = nodes.find((n) => n.id === conn.targetNodeId);
          if (!source || !target) return null;

          const startX = source.x + source.width;
          const startY = source.y + source.height / 2;
          const endX = target.x;
          const endY = target.y + target.height / 2;
          const midX = (startX + endX) / 2;

          const pathD = `M ${startX} ${startY} C ${midX} ${startY}, ${midX} ${endY}, ${endX} ${endY}`;

          return (
            <g key={conn.id}>
              <path
                className={`zero-processmap-connection ${conn.isDashed ? 'is-dashed' : ''}`}
                d={pathD}
                markerEnd="url(#zero-arrow)"
              />
              {conn.label && (
                <g>
                  <rect
                    className="zero-processmap-conn-label-bg"
                    x={midX - 45}
                    y={(startY + endY) / 2 - 10}
                    width={90}
                    height={20}
                  />
                  <text
                    className="zero-processmap-conn-label"
                    x={midX}
                    y={(startY + endY) / 2 + 4}
                  >
                    {conn.label}
                  </text>
                </g>
              )}
            </g>
          );
        })}

        {/* 3. Nodes */}
        {nodes.map((node) => {
          const isSelected = selectedNodeIds.includes(node.id);

          return (
            <g
              key={node.id}
              className={`zero-processmap-node node-status-${node.status} ${
                isSelected ? 'is-selected' : ''
              }`}
              onMouseDown={(e) => handleNodeMouseDown(e, node)}
              onContextMenu={(e) => handleContextMenu(e, 'node', node.id)}
            >
              <rect
                className="zero-processmap-node-card"
                x={node.x}
                y={node.y}
                width={node.width}
                height={node.height}
              />

              {/* Status Badge Top Right */}
              <rect
                className="status-bg"
                x={node.x + node.width - 64}
                y={node.y + 10}
                width={54}
                height={18}
                rx="4"
              />
              <text
                className="zero-processmap-node-status-badge status-text"
                x={node.x + node.width - 37}
                y={node.y + 22}
                textAnchor="middle"
              >
                {node.status}
              </text>

              {/* Icon & Label */}
              <text
                className="zero-processmap-node-title"
                x={node.x + 14}
                y={node.y + 26}
              >
                {node.icon ? `${node.icon} ` : ''}
                {node.label}
              </text>

              {node.subLabel && (
                <text
                  className="zero-processmap-node-sub"
                  x={node.x + 14}
                  y={node.y + 44}
                >
                  {node.subLabel}
                </text>
              )}
            </g>
          );
        })}
      </svg>

      {/* Context Menu Component */}
      <ContextMenu
        isOpen={contextMenu.isOpen}
        x={contextMenu.x}
        y={contextMenu.y}
        items={contextMenuItems}
        onClose={() => setContextMenu((prev) => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
};
