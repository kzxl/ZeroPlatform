using System;

namespace ZeroUI.Demo.Data
{
    public static class MockDataGenerator
    {
        private struct ItemTemplate
        {
            public string Category;
            public string CodePrefix;
            public string Name;
            public double BasePrice;
            public int MinQty;
            public int MaxQty;

            public ItemTemplate(string category, string prefix, string name, double basePrice, int minQty, int maxQty)
            {
                Category = category;
                CodePrefix = prefix;
                Name = name;
                BasePrice = basePrice;
                MinQty = minQty;
                MaxQty = maxQty;
            }
        }

        private static readonly ItemTemplate[] RealCatalog = new[]
        {
            // Active Semiconductors & ICs
            new ItemTemplate("Microcontroller", "IC-MCU", "ARM Cortex-M4 STM32F407VGT6 LQFP100 MCU", 14.50, 100, 2500),
            new ItemTemplate("Power Management", "IC-PWR", "TI TPS54302DDCR Buck Converter 28V 3A Power IC", 1.85, 500, 10000),
            new ItemTemplate("Memory", "IC-MEM", "Winbond W25Q128JVS 128Mb SPI Flash SOIC-8", 2.40, 300, 5000),
            new ItemTemplate("Wireless & IoT", "IC-IOT", "ESP32-WROOM-32E Wi-Fi/BLE Module PCB Ant", 7.80, 200, 4000),
            new ItemTemplate("Processor", "IC-MPU", "NXP i.MX RT1062 Crossover Processor 600MHz", 26.00, 50, 1200),
            new ItemTemplate("Motor Driver", "IC-DRV", "TMC2209 Ultra-Silent Stepper Motor Driver", 4.50, 150, 3000),
            new ItemTemplate("Data Acquisition", "IC-ADC", "TI ADS1256 24-Bit High Precision ADC IC", 19.50, 40, 800),
            new ItemTemplate("Networking", "IC-PHY", "Microchip LAN8720A 10/100 Ethernet PHY QFN-24", 3.20, 200, 3500),

            // Passive SMT Components
            new ItemTemplate("Capacitors", "SMD-CAP", "Murata 0805 10uF 25V X7R SMD Ceramic Capacitor", 0.12, 5000, 50000),
            new ItemTemplate("Resistors", "SMD-RES", "Yageo 0603 10kΩ 1% Precision Thin Film Resistor", 0.045, 10000, 80000),
            new ItemTemplate("Inductors", "SMD-IND", "Coilcraft 10uH 3.5A Shielded Power Inductor SMD", 0.85, 1000, 15000),
            new ItemTemplate("Oscillators", "SMD-XTAL", "TXC 3225 16.000MHz 10ppm SMD Crystal Oscillator", 0.62, 500, 8000),
            new ItemTemplate("Diodes", "SMD-DIO", "Panjit SS34 40V 3A SMC Schottky Rectifier Diode", 0.28, 2000, 20000),
            new ItemTemplate("Capacitors", "SMD-POL", "Panasonic 100uF 35V Low ESR Polymer Capacitor SMD", 1.45, 400, 6000),
            new ItemTemplate("Protection", "SMD-TVS", "Semtech SM712 ESD/TVS RS-485 Protection Diode", 0.98, 800, 12000),

            // Connectors & Electromechanical
            new ItemTemplate("Connectors", "CONN-HDR", "Pin Header 2x20P 2.54mm Right-Angle Gold Plated", 0.65, 300, 5000),
            new ItemTemplate("Connectors", "CONN-USBC", "USB Type-C 16-Pin SMT IPX7 Waterproof Receptacle", 1.20, 500, 8000),
            new ItemTemplate("Terminal Blocks", "CONN-TERM", "Phoenix Contact 5.08mm 4-Pin Terminal Block", 1.65, 200, 4000),
            new ItemTemplate("Relays", "CONN-REL", "Omron G3MB-202P 5VDC Solid State Relay (SSR)", 4.20, 80, 1500),
            new ItemTemplate("Cables", "CONN-FFC", "FFC Signal Cable 0.5mm 30-Pin L=150mm Gold Plated", 0.82, 400, 6000),
            new ItemTemplate("Sensors", "SENS-TEMP", "PT100 3-Wire Class A Industrial RTD Sensor", 18.50, 30, 500),

            // Pneumatics & Automation
            new ItemTemplate("Valves", "PNEU-VAL", "Airtac 4V210-08 24VDC 5/2 Pneumatic Solenoid Valve", 24.50, 20, 350),
            new ItemTemplate("Pneumatics", "PNEU-CYL", "SMC MGPM25-50Z Guided Compact Air Cylinder", 145.00, 10, 120),
            new ItemTemplate("Vacuum", "PNEU-VAC", "SMC ZP2-20UM Conductive Vacuum Suction Cup", 8.50, 50, 800),
            new ItemTemplate("Air Prep", "PNEU-FRL", "Festo MS4-LFR-1/4-D7 Filter Regulator Unit", 185.00, 8, 80),
            new ItemTemplate("Optical Sensors", "SENS-PROX", "Keyence PZ-G41N Optical Through-Beam Sensor", 115.00, 15, 200),
            new ItemTemplate("Pressure Sensors", "SENS-PRS", "SMC ISE30A-01-N Digital Pressure Sensor", 168.00, 12, 150),

            // Motion & Mechanical
            new ItemTemplate("Motors", "MEC-STEP", "Nema 23 2.8Nm High-Torque Hybrid Stepper Motor", 48.50, 15, 250),
            new ItemTemplate("Linear Guides", "MEC-RAIL", "HIWIN HGH20CA-1000 Linear Guide Rail & Block", 125.00, 10, 100),
            new ItemTemplate("Ball Screws", "MEC-SCREW", "TBI Motion SFU1605-600mm Precision Ball Screw", 195.00, 6, 80),
            new ItemTemplate("Bearings", "MEC-BRG", "SKF 6205-2RSH/C3 Rubber-Sealed Deep Groove Bearing", 9.50, 40, 600),
            new ItemTemplate("Extrusions", "MEC-ALU", "AL6063 40x80 Anodized Industrial Aluminum Extrusion", 28.00, 20, 300),
            new ItemTemplate("Enclosures", "MEC-ENCL", "CNC Milled Aluminum Enclosure IP67 Waterproof", 68.00, 25, 400),

            // Chemicals & Consumables
            new ItemTemplate("Consumables", "CHEM-SOLD", "Senju M705-GRN360 Lead-Free Solder Paste 500g", 125.00, 10, 150),
            new ItemTemplate("Consumables", "CHEM-GLUE", "ShinEtsu 7783 High-Thermal Compound Paste 100g", 38.00, 20, 300),
            new ItemTemplate("Consumables", "CONS-TAPE", "Kapton Polyimide High-Temp Tape 25mm x 33m", 11.50, 30, 450),
            new ItemTemplate("Packaging", "PKG-ESD", "ESD Conductive Molded Component Tray 400x300mm", 6.50, 100, 1500),
            new ItemTemplate("Consumables", "CONS-WIPE", "Cleanroom Lint-Free Wiper 9x9 Inch (Class 100)", 14.50, 40, 600)
        };

        private static readonly string[] SampleStatuses = new[]
        {
            "Passed OQC",
            "Pending IQC",
            "SMT Feeding",
            "QC Quarantine",
            "Low Stock Warning"
        };

        public static InventoryItem[] Generate(int count)
        {
            var items = new InventoryItem[count];
            var rand = new Random(42);

            int catLen = RealCatalog.Length;
            int statusLen = SampleStatuses.Length;

            for (int i = 0; i < count; i++)
            {
                int id = i + 1;
                ref readonly var tpl = ref RealCatalog[i % catLen];

                string code = $"{tpl.CodePrefix}-{id:D6}";
                int qty = rand.Next(tpl.MinQty, tpl.MaxQty);
                double price = tpl.BasePrice * (1.0 + (rand.Next(-5, 6) * 0.01));
                string lot = $"LOT-{rand.Next(24, 27)}{rand.Next(1, 13):D2}-{rand.Next(100, 999)}";
                string status = SampleStatuses[i % statusLen];
                float yieldRate = (float)(0.90 + (rand.NextDouble() * 0.099));
                bool isActive = (i % 7) != 0;

                items[i] = new InventoryItem(isActive, tpl.Category, id, code, tpl.Name, qty, price, yieldRate, lot, status);
            }

            return items;
        }
    }
}
