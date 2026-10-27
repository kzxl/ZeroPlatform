import React, { useState } from 'react';
import { ThemeProvider, useZeroTheme } from '../core/theme/ThemeProvider';
import { ToastProvider, useToast } from '../core/notifications/ToastContext';
import { ToastContainer } from '../components/Toast/ToastContainer';
import { MenuIcons } from '../core/icons/MenuIcons';
import { ButtonGroup } from '../components/ButtonGroup/ButtonGroup';
import { ContextMenu, ContextMenuItem } from '../components/ContextMenu/ContextMenu';
import { DateRangePicker } from '../components/DateRangePicker/DateRangePicker';
import { DateRange } from '../core/models/DateRangeModel';
import { ProcessMap } from '../components/ProcessMap/ProcessMap';
import {
  ProcessFlowNode,
  ProcessFlowLane,
  ProcessFlowConnection,
} from '../core/models/ProcessMapModel';
import { WorkflowCard } from '../components/WorkflowCard/WorkflowCard';
import { WorkflowStage } from '../core/models/WorkflowModel';
import { StatusBadge } from '../components/StatusBadge/StatusBadge';
import { DataGridLite } from '../components/DataGrid/DataGridLite';
import { ColumnDef } from '../core/models/DataGridModel';
import { RangeSlider } from '../components/RangeSlider/RangeSlider';
import './App.css';

// Sample Process Flow Data
const initialDemoLanes: ProcessFlowLane[] = [
  {
    id: 'lane_smt',
    title: 'SMT Assembly Line (SMT-01)',
    x: 40,
    y: 70,
    width: 620,
    height: 190,
  },
  {
    id: 'lane_qc',
    title: 'Quality & Final Inspection',
    x: 40,
    y: 290,
    width: 620,
    height: 190,
  },
];

const initialDemoNodes: ProcessFlowNode[] = [
  {
    id: 'node_pcb',
    laneId: 'lane_smt',
    label: 'PCB Loading',
    subLabel: 'Auto Magazine Feeder',
    type: 'Step',
    status: 'Completed',
    x: 70,
    y: 120,
    width: 150,
    height: 64,
    icon: '📦',
  },
  {
    id: 'node_paste',
    laneId: 'lane_smt',
    label: 'Solder Printing',
    subLabel: 'Stencil 0.12mm DEK',
    type: 'Step',
    status: 'Completed',
    x: 270,
    y: 120,
    width: 150,
    height: 64,
    icon: '⚙️',
  },
  {
    id: 'node_reflow',
    laneId: 'lane_smt',
    label: 'Reflow Oven',
    subLabel: 'Zone 1-10 Profile B',
    type: 'Step',
    status: 'Active',
    x: 470,
    y: 120,
    width: 150,
    height: 64,
    icon: '🔥',
  },
  {
    id: 'node_aoi',
    laneId: 'lane_qc',
    label: 'AOI Inspection',
    subLabel: '3D Optical Check',
    type: 'Decision',
    status: 'Warning',
    x: 170,
    y: 350,
    width: 160,
    height: 64,
    icon: '🔍',
  },
  {
    id: 'node_pack',
    laneId: 'lane_qc',
    label: 'Final Boxing',
    subLabel: 'ESD Protective Carton',
    type: 'End',
    status: 'Default',
    x: 420,
    y: 350,
    width: 150,
    height: 64,
    icon: '🏷️',
  },
];

const initialDemoConnections: ProcessFlowConnection[] = [
  {
    id: 'c1',
    sourceNodeId: 'node_pcb',
    targetNodeId: 'node_paste',
    label: 'PASS',
  },
  {
    id: 'c2',
    sourceNodeId: 'node_paste',
    targetNodeId: 'node_reflow',
    label: 'AUTO',
  },
  {
    id: 'c3',
    sourceNodeId: 'node_reflow',
    targetNodeId: 'node_aoi',
    label: 'COOLING',
    isDashed: true,
  },
  {
    id: 'c4',
    sourceNodeId: 'node_aoi',
    targetNodeId: 'node_pack',
    label: 'QC PASS',
  },
];

// Sample Workflow Card Stages
const sampleWorkflowStages: WorkflowStage[] = [
  {
    key: 'stage_smt',
    title: 'Surface Mount (SMT)',
    quantity: 12400,
    updatedTime: '10:45 AM',
    status: 'Completed',
    glyph: 'Gear',
  },
  {
    key: 'stage_qc',
    title: 'AOI & Visual QC',
    quantity: 9800,
    updatedTime: '11:20 AM',
    status: 'InProgress',
    glyph: 'Checkmark',
  },
  {
    key: 'stage_packing',
    title: 'ESD Packaging',
    quantity: 5200,
    updatedTime: '01:15 PM',
    status: 'Warning',
    glyph: 'Warehouse',
  },
  {
    key: 'stage_logistics',
    title: 'Warehouse Dispatch',
    quantity: 0,
    updatedTime: '--',
    status: 'Waiting',
    glyph: 'Truck',
  },
];

// Sample DataGrid Records
interface MachineTelemetry {
  id: string;
  name: string;
  line: string;
  temperature: number;
  pressure: number;
  status: 'running' | 'warning' | 'critical' | 'offline';
  uptime: string;
}

const sampleTelemetry: MachineTelemetry[] = [
  { id: 'M-01', name: 'Reflow Furnace Alpha', line: 'SMT Line 01', temperature: 245.8, pressure: 5.2, status: 'running', uptime: '99.4%' },
  { id: 'M-02', name: 'Chip Mounter HighSpeed', line: 'SMT Line 01', temperature: 48.2, pressure: 6.1, status: 'running', uptime: '98.9%' },
  { id: 'M-03', name: 'Screen Printer DEK-2', line: 'SMT Line 01', temperature: 32.1, pressure: 4.8, status: 'warning', uptime: '94.2%' },
  { id: 'M-04', name: 'Wave Soldering Bath', line: 'DIP Line 02', temperature: 280.5, pressure: 5.9, status: 'critical', uptime: '86.5%' },
  { id: 'M-05', name: 'Automated 3D AOI', line: 'QC Section', temperature: 29.4, pressure: 0.0, status: 'running', uptime: '99.8%' },
  { id: 'M-06', name: 'Conveyor Buffer Unit', line: 'Packaging', temperature: 24.1, pressure: 3.4, status: 'offline', uptime: '72.1%' },
  { id: 'M-07', name: 'Laser Marker Fiber-30W', line: 'Packaging', temperature: 38.0, pressure: 5.0, status: 'running', uptime: '99.1%' },
];

const telemetryColumns: ColumnDef<MachineTelemetry>[] = [
  { key: 'id', title: 'Machine ID', width: 110, sortable: true },
  { key: 'name', title: 'Equipment Name', width: 220, sortable: true },
  { key: 'line', title: 'Production Line', width: 150, sortable: true },
  {
    key: 'temperature',
    title: 'Temp (°C)',
    width: 110,
    sortable: true,
    align: 'right',
    render: (val: number) => <span style={{ fontWeight: 600 }}>{val.toFixed(1)} °C</span>,
  },
  {
    key: 'pressure',
    title: 'Pressure (bar)',
    width: 120,
    sortable: true,
    align: 'right',
    render: (val: number) => <span>{val.toFixed(1)} bar</span>,
  },
  {
    key: 'status',
    title: 'Live Status',
    width: 130,
    sortable: true,
    align: 'center',
    render: (val: any) => (
      <StatusBadge
        variant={val}
        label={val.toUpperCase()}
        pulse={val === 'running'}
        styleType="pill"
      />
    ),
  },
  { key: 'uptime', title: 'OEE Uptime', width: 100, sortable: true, align: 'right' },
];

const ShowcaseContent: React.FC = () => {
  const { isDark, toggleTheme } = useZeroTheme();
  const { toast } = useToast();

  const [activeTab, setActiveTab] = useState<'flow' | 'milestones' | 'data' | 'catalog'>('flow');

  // Date Range state
  const [dateRange, setDateRange] = useState<DateRange>({
    start: new Date(2026, 8, 1),
    end: new Date(2026, 8, 14),
  });

  // Range Slider state
  const [pressureRange, setPressureRange] = useState({ min: 2, max: 8 });

  // Standalone Context Menu state
  const [menuPos, setMenuPos] = useState<{ isOpen: boolean; x: number; y: number }>({
    isOpen: false,
    x: 0,
    y: 0,
  });

  const standaloneMenuItems: ContextMenuItem[] = [
    {
      id: 'cmd_start',
      label: 'Start SCADA Routine',
      icon: MenuIcons.Play,
      shortcut: 'F5',
      onClick: () => toast.success('SCADA Routine Started', 'Line 01 batch initialization initiated.'),
    },
    {
      id: 'cmd_refresh',
      label: 'Sync Telemetry Feed',
      icon: MenuIcons.Refresh,
      shortcut: 'Ctrl+R',
      onClick: () => toast.info('Telemetry Synced', 'Live PLC parameters fetched.'),
    },
    {
      id: 'cmd_export',
      label: 'Export Sensor Log (.csv)',
      icon: MenuIcons.Export,
      onClick: () => toast.success('Export Ready', 'Telemetry CSV generated.'),
    },
    {
      id: 'cmd_emergency',
      label: 'Emergency Interlock Halt',
      icon: MenuIcons.Stop,
      danger: true,
      onClick: () => toast.error('Emergency Stop Triggered', 'All actuators safely halted.'),
    },
  ];

  return (
    <div className="showcase-container">
      {/* Header */}
      <header className="showcase-header">
        <div className="showcase-title-area">
          <h1 className="showcase-title">
            <span>ZeroUI.React</span>
            <span className="showcase-badge">v1.0 Industrial</span>
          </h1>
          <p className="showcase-subtitle">
            Synchronized Design Tokens, Headless DAG Engine, Milestone Pipelines & SCADA Controls.
          </p>
        </div>

        <div className="showcase-header-actions">
          <StatusBadge variant="running" label="PLC CLUSTER ONLINE" pulse styleType="pill" />
          <button
            type="button"
            className="zero-daterange-trigger"
            onClick={toggleTheme}
            title="Toggle Dark / Light Theme"
          >
            {isDark ? '☀️ Switch to Light Theme' : '🌙 Switch to Dark Theme'}
          </button>
        </div>
      </header>

      {/* Navigation Tabs */}
      <nav className="showcase-nav-tabs">
        <button
          type="button"
          className={`showcase-nav-tab ${activeTab === 'flow' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('flow')}
        >
          {MenuIcons.Flow} DAG Process Studio
        </button>
        <button
          type="button"
          className={`showcase-nav-tab ${activeTab === 'milestones' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('milestones')}
        >
          {MenuIcons.Workflow} Milestone Pipelines
        </button>
        <button
          type="button"
          className={`showcase-nav-tab ${activeTab === 'data' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('data')}
        >
          {MenuIcons.Data} Enterprise Data & Filtering
        </button>
        <button
          type="button"
          className={`showcase-nav-tab ${activeTab === 'catalog' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('catalog')}
        >
          {MenuIcons.Frame} Action Strips & Badges
        </button>
      </nav>

      {/* TAB 1: DAG PROCESS STUDIO */}
      {activeTab === 'flow' && (
        <section className="showcase-section">
          <h2 className="showcase-section-title">Industrial DAG ProcessMap</h2>
          <p className="showcase-section-desc">
            Direct port of <code>ProcessMap.cs</code>. Features SVG rendering, 8-point Swimlane
            resize handles, move lane with child nodes, automated DAG layered topological sort,
            Save button dirty badge, and right-click ContextMenu.
          </p>

          <ProcessMap
            initialLanes={initialDemoLanes}
            initialNodes={initialDemoNodes}
            initialConnections={initialDemoConnections}
            title="SMT-01 • Process Flow Engine"
            onSave={(data) => {
              toast.success(
                'Process Flow Saved',
                `Persisted ${data.nodes.length} nodes and ${data.lanes.length} swimlanes to cloud.`
              );
            }}
          />
        </section>
      )}

      {/* TAB 2: MILESTONES PIPELINE */}
      {activeTab === 'milestones' && (
        <section className="showcase-section">
          <h2 className="showcase-section-title">Production Milestone Pipeline (WorkflowCard)</h2>
          <p className="showcase-section-desc">
            Direct port of <code>WorkflowCard.cs</code>. Combines step badge numbering, stage
            status indicators, directional vector transitions, and vector glyph icons (Gear,
            Checkmark, Warehouse, Truck).
          </p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
            <WorkflowCard
              stepNumber={3}
              badgeColor="#2563eb"
              title="Manufacturing Order Pipeline (MO-2026-0901)"
              subtitle="PCB Assembly Lot #55480 • Workstation Cluster Alpha"
              statusTag="Operating"
              statusTagColor="#10b981"
              stages={sampleWorkflowStages}
              onStageClick={(stage, index) => {
                toast.info(
                  `Stage Selected: ${stage.title}`,
                  `Status: ${stage.status} | Qty: ${stage.quantity} units (Stage index ${index})`
                );
              }}
            />

            <WorkflowCard
              stepNumber={4}
              badgeColor="#f59e0b"
              title="High-Voltage Inverter Batch Inspection"
              subtitle="Quality Certification & Environmental Stress Screening (ESS)"
              statusTag="Attention Required"
              statusTagColor="#f59e0b"
              stages={[
                { key: 's1', title: 'Hi-Pot Test', quantity: 450, status: 'Completed', glyph: 'Checkmark' },
                { key: 's2', title: 'Burn-In Chamber', quantity: 450, status: 'Warning', glyph: 'Gear' },
                { key: 's3', title: 'Pallet Packaging', quantity: 0, status: 'Waiting', glyph: 'Warehouse' },
              ]}
              onStageClick={(stage) => toast.warning('Stage Clicked', `${stage.title} selected`)}
            />
          </div>
        </section>
      )}

      {/* TAB 3: ENTERPRISE DATA & FILTERING */}
      {activeTab === 'data' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          {/* Filter Bar with DateRangePicker and RangeSlider */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Date & Numeric Range Controls</h2>
            <p className="showcase-section-desc">
              Integrated with <code>DateRangeModel</code> and <code>RangeSliderModel</code> for
              resolution-aware queries.
            </p>

            <div className="showcase-grid-2">
              <div className="showcase-demo-item">
                <span className="showcase-demo-label">DateRangePicker (Day / Month / Year Viewmodes)</span>
                <DateRangePicker
                  value={dateRange}
                  onChange={(range) => {
                    setDateRange(range);
                    toast.info('Date Filter Applied', `${range.start.toLocaleDateString()} - ${range.end.toLocaleDateString()}`);
                  }}
                />
              </div>

              <div className="showcase-demo-item">
                <span className="showcase-demo-label">Dual-Thumb Numeric Range Slider</span>
                <RangeSlider
                  min={0}
                  max={12}
                  step={0.5}
                  value={pressureRange}
                  onChange={setPressureRange}
                  formatLabel={(v) => `${v.toFixed(1)} bar`}
                />
              </div>
            </div>
          </section>

          {/* High-density DataGrid */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Telemetry Sensor Grid (DataGridLite)</h2>
            <p className="showcase-section-desc">
              High-density SCADA telemetry monitor with client-side multi-column sorting, search
              filtering, pagination, and checkbox multi-selection.
            </p>

            <DataGridLite
              data={sampleTelemetry}
              columns={telemetryColumns}
              rowKey="id"
              pageSize={5}
              onSelectionChange={(keys) => {
                if (keys.length > 0) {
                  toast.info('Telemetry Selection', `${keys.length} machine units selected.`);
                }
              }}
            />
          </section>
        </div>
      )}

      {/* TAB 4: ACTION STRIPS, BADGES & CATALOG */}
      {activeTab === 'catalog' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          {/* Status Badges */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Industrial Status Badges</h2>
            <p className="showcase-section-desc">
              Pulse indicators and state tags for SCADA devices, machine status, and alarms.
            </p>

            <div className="showcase-badge-row">
              <StatusBadge variant="running" label="RUNNING" pulse styleType="pill" />
              <StatusBadge variant="normal" label="NORMAL" styleType="tag" />
              <StatusBadge variant="warning" label="TEMPERATURE WARNING" pulse styleType="pill" />
              <StatusBadge variant="critical" label="EMERGENCY TRIP" pulse styleType="pill" />
              <StatusBadge variant="offline" label="OFFLINE / DISCONNECTED" styleType="tag" />
              <StatusBadge variant="maintenance" label="INSPECTION ROUTINE" styleType="outline" />
              <StatusBadge variant="info" label="SYNCHRONIZED" count={42} styleType="pill" />
            </div>
          </section>

          {/* Connected ButtonGroup */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Connected ButtonGroup & Action Strips</h2>
            <p className="showcase-section-desc">
              Zero-space connected action strip with size modes and notification badges.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className="showcase-demo-item">
                <span className="showcase-demo-label">Pill Segmented Action Strip</span>
                <ButtonGroup
                  sizeMode="AutoFit"
                  items={[
                    { id: '1', label: 'Create Order', icon: MenuIcons.Add, onClick: () => toast.success('Order Created') },
                    { id: '2', label: 'Modify Parameters', icon: MenuIcons.Edit, onClick: () => toast.info('Edit Mode Active') },
                    { id: '3', label: 'Save Work', icon: MenuIcons.Save, showBadgeDot: true, badgeColor: '#ef4444', onClick: () => toast.success('All Changes Saved') },
                    { id: '4', label: 'Print Label', icon: MenuIcons.Print, onClick: () => toast.info('Sending to Barcode Printer...') },
                  ]}
                />
              </div>

              <div className="showcase-demo-item">
                <span className="showcase-demo-label">Equal-Width Operational Mode Bar</span>
                <ButtonGroup
                  sizeMode="EqualWidth"
                  selectionMode="SingleSelect"
                  items={[
                    { id: 'm_auto', label: 'Automatic Line', icon: '🤖', isSelected: true },
                    { id: 'm_semi', label: 'Semi-Automatic', icon: '⚡' },
                    { id: 'm_manual', label: 'Manual Calibrate', icon: '🛠️' },
                  ]}
                />
              </div>
            </div>
          </section>

          {/* Context Menu Playground */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Enterprise ContextMenu Trigger</h2>
            <p className="showcase-section-desc">
              Viewport-safe popover positioning, keyboard shortcuts, and danger variants.
            </p>

            <div
              className="context-zone"
              onContextMenu={(e) => {
                e.preventDefault();
                setMenuPos({ isOpen: true, x: e.clientX, y: e.clientY });
              }}
            >
              🖱️ Right-click inside this target zone to open the ZeroUI ContextMenu
            </div>

            <ContextMenu
              isOpen={menuPos.isOpen}
              x={menuPos.x}
              y={menuPos.y}
              items={standaloneMenuItems}
              onClose={() => setMenuPos((p) => ({ ...p, isOpen: false }))}
            />
          </section>

          {/* MenuIcons Catalog */}
          <section className="showcase-section">
            <h2 className="showcase-section-title">Standardized MenuIcons Catalog</h2>
            <p className="showcase-section-desc">
              Synchronized 100% with <code>ZeroUI.Core.Icons.MenuIcons.cs</code>.
            </p>

            <div className="icons-grid">
              {Object.entries(MenuIcons)
                .filter(([_, glyph]) => typeof glyph === 'string')
                .map(([name, glyph]) => (
                  <div key={name} className="icon-card">
                    <span className="icon-glyph">{glyph as string}</span>
                    <span>{name}</span>
                  </div>
                ))}
            </div>
          </section>
        </div>
      )}

      {/* Floating Toast Container */}
      <ToastContainer />
    </div>
  );
};

export const App: React.FC = () => {
  return (
    <ThemeProvider>
      <ToastProvider>
        <ShowcaseContent />
      </ToastProvider>
    </ThemeProvider>
  );
};

export default App;
