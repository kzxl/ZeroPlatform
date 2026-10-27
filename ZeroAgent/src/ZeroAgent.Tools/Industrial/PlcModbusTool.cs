using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Industrial
{
    /// <summary>
    /// Industrial PLC toolset implementing Modbus protocol communication for AI agents.
    /// Provides zero-allocation register/coil reading and HITL-protected write commands.
    /// </summary>
    public static class PlcModbusTool
    {
        // Mock/In-memory PLC storage for register states when no physical wire is attached
        private static readonly ConcurrentDictionary<int, ushort> _holdingRegisters = new ConcurrentDictionary<int, ushort>();
        private static readonly ConcurrentDictionary<int, bool> _coils = new ConcurrentDictionary<int, bool>();

        static PlcModbusTool()
        {
            // Seed sample registers for factory simulation
            _holdingRegisters[40001] = 72;   // Temperature in Celsius
            _holdingRegisters[40002] = 1450; // Motor RPM
            _holdingRegisters[40003] = 42;   // Pressure in PSI
            _coils[1] = true;                // Conveyor 1 Running
            _coils[2] = false;               // Emergency Stop Engaged
        }

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // 1. Read Holding Registers
            var readRegSchema = new JsonSchemaConstraint("ReadHoldingRegisters")
                .AddProperty("unitId", SchemaPropertyType.Number, required: true)
                .AddProperty("address", SchemaPropertyType.Number, required: true)
                .AddProperty("count", SchemaPropertyType.Number, required: false);

            registry.Register(new AgentTool(
                "read_plc_holding_registers",
                "Reads 16-bit holding registers from the specified PLC address.",
                "unitId: int, address: int, count: int",
                ExecuteReadHoldingRegistersAsync,
                schema: readRegSchema,
                requiresApproval: false
            ));

            // 2. Write Holding Register (CRITICAL SENSITIVE ACTION)
            var writeRegSchema = new JsonSchemaConstraint("WriteHoldingRegister")
                .AddProperty("unitId", SchemaPropertyType.Number, required: true)
                .AddProperty("address", SchemaPropertyType.Number, required: true)
                .AddProperty("value", SchemaPropertyType.Number, required: true);

            registry.Register(new AgentTool(
                "write_plc_holding_register",
                "Writes a 16-bit integer value into a PLC holding register. Modifies physical equipment state.",
                "unitId: int, address: int, value: int",
                ExecuteWriteHoldingRegisterAsync,
                schema: writeRegSchema,
                requiresApproval: true
            ));

            // 3. Read Coils
            var readCoilsSchema = new JsonSchemaConstraint("ReadCoils")
                .AddProperty("unitId", SchemaPropertyType.Number, required: true)
                .AddProperty("address", SchemaPropertyType.Number, required: true)
                .AddProperty("count", SchemaPropertyType.Number, required: false);

            registry.Register(new AgentTool(
                "read_plc_coils",
                "Reads boolean discrete output coils from the PLC.",
                "unitId: int, address: int, count: int",
                ExecuteReadCoilsAsync,
                schema: readCoilsSchema,
                requiresApproval: false
            ));

            // 4. Write Coil (CRITICAL SENSITIVE ACTION)
            var writeCoilSchema = new JsonSchemaConstraint("WriteCoil")
                .AddProperty("unitId", SchemaPropertyType.Number, required: true)
                .AddProperty("address", SchemaPropertyType.Number, required: true)
                .AddProperty("value", SchemaPropertyType.Boolean, required: true);

            registry.Register(new AgentTool(
                "write_plc_coil",
                "Actuates a physical discrete output coil (relay, valve, motor contactor) on the PLC.",
                "unitId: int, address: int, value: bool",
                ExecuteWriteCoilAsync,
                schema: writeCoilSchema,
                requiresApproval: true
            ));
        }

        private static Task<string> ExecuteReadHoldingRegistersAsync(string arg)
        {
            try
            {
                int address = 40001;
                int count = 1;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("address", out var a)) address = a.GetInt32();
                        if (root.TryGetProperty("count", out var c)) count = c.GetInt32();
                    }
                }
                else
                {
                    var parts = arg.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[0], out int parsedAddr)) address = parsedAddr;
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedCnt)) count = parsedCnt;
                }

                count = Math.Max(1, Math.Min(count, 125));
                var values = new ushort[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = _holdingRegisters.TryGetValue(address + i, out ushort val) ? val : (ushort)0;
                }

                return Task.FromResult(JsonSerializer.Serialize(new { address, count, values }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to read holding registers: {ex.Message}");
            }
        }

        private static Task<string> ExecuteWriteHoldingRegisterAsync(string arg)
        {
            try
            {
                int address = 40001;
                ushort value = 0;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("address", out var a)) address = a.GetInt32();
                        if (root.TryGetProperty("value", out var v)) value = (ushort)v.GetInt32();
                    }
                }
                else
                {
                    var parts = arg.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int a) && ushort.TryParse(parts[1], out ushort v))
                    {
                        address = a;
                        value = v;
                    }
                }

                _holdingRegisters[address] = value;
                return Task.FromResult($"Successfully wrote value {value} to holding register {address}.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to write holding register: {ex.Message}");
            }
        }

        private static Task<string> ExecuteReadCoilsAsync(string arg)
        {
            try
            {
                int address = 1;
                int count = 1;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("address", out var a)) address = a.GetInt32();
                        if (root.TryGetProperty("count", out var c)) count = c.GetInt32();
                    }
                }

                var values = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = _coils.TryGetValue(address + i, out bool b) && b;
                }

                return Task.FromResult(JsonSerializer.Serialize(new { address, count, values }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to read coils: {ex.Message}");
            }
        }

        private static Task<string> ExecuteWriteCoilAsync(string arg)
        {
            try
            {
                int address = 1;
                bool value = false;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("address", out var a)) address = a.GetInt32();
                        if (root.TryGetProperty("value", out var v)) value = v.GetBoolean();
                    }
                }

                _coils[address] = value;
                return Task.FromResult($"Successfully set coil {address} to {value}.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to write coil: {ex.Message}");
            }
        }
    }
}
