# ZeroPlatform Autonomous Industrial AI Chatbot & Copilot Demo

An enterprise-grade, standalone industrial AI Chatbot and Copilot studio showcasing the end-to-end integration of **ZeroAgent**, **ZeroUI**, **ZeroPrompt**, and **ZeroData** running 100% in pure C# with sub-5ms latency and zero GPU dependencies.

---

## 🌟 Architectural Highlights

1. **Dual-Tier Dialogue Engine**:
   - **Tier 1 (Reflex NLU & DST)**: Ultra low-latency (< 5ms) deterministic intent classification, slot extraction, anaphora/coreference resolution ("*Nó có nóng không?*"), and semantic cache.
   - **Tier 2 (Cognitive Deliberation)**: Multi-turn ReAct reasoning loop with tool calling and dynamic analytical execution.
2. **Modern Industrial UI (`ZeroUI`)**:
   - Powered by `ZAiChatBox` with real-time token streaming animations, prompt suggestion chips, responsive message bubbles, and slate dark theme.
   - Live telemetry dashboard side-by-side displaying NLU intent recognition, confidence scores, extracted slots, and active entity tracking.
3. **Human-in-the-Loop (HITL) Safety Gate**:
   - Intercepts sensitive operational actuators (e.g. `STOP_MACHINE`, `write_plc_coil`).
   - Surfaces instant amber hazard alerts with interactive operator approval/rejection buttons and compliance audit logging.
4. **Agentic Memory Engine (4-Tier)**:
   - **Working Memory**: In-flight conversation state, active subject tracking, and turn history.
   - **Semantic Memory**: SOP Standard Operating Procedure manuals (e.g. furnace overheating mitigation, CNC spindle lubrication).
   - **Episodic Memory**: Vectorized historical incident reports and past resolution records.
   - **Response Cache**: High-density semantic cache short-circuiting identical queries in sub-millisecond time.
5. **Columnar DataFrame & NL-to-SQL Analytics**:
   - Real-time schema introspection and query execution across `factory_machines` and operational telemetry tables.

---

## 🚀 How to Run

### 1. Launch WinForms Desktop GUI

From the root directory or sample directory:

```bash
dotnet run --project samples/ZeroPlatform.Samples.ChatBot/ZeroPlatform.Samples.ChatBot.csproj -f net8.0-windows -c Release
```

Or launch it directly from the main showcase application:

```bash
dotnet run --project samples/ZeroPlatform.Samples.Showcase/ZeroPlatform.Samples.Showcase.csproj -f net8.0-windows -c Release
```
*(Click the top-right button: `🤖 Launch AI ChatBot`)*

### 2. Launch Interactive Terminal CLI Mode

For headless servers, SSH terminals, or developer quick testing:

```bash
dotnet run --project samples/ZeroPlatform.Samples.ChatBot/ZeroPlatform.Samples.ChatBot.csproj -f net8.0-windows -c Release -- --cli
```

---

## 💬 Sample Prompts to Try

| Category | Sample User Prompt | Expected Agent Action |
| :--- | :--- | :--- |
| **Telemetry & Sensors** | `Kiểm tra nhiệt độ máy CNC-01` | Queries TSDB/PLC tool and returns `73.5°C [BÌNH THƯỜNG]`. |
| **Anaphora Coreference** | `Nhiệt độ nó giờ sao?` | Resolves pronoun *"nó"* to `CNC-01` via Working Memory without asking again. |
| **SOP Retrieval** | `Quy trình xử lý quá nhiệt Lò nung F-01` | Retrieves SOP instructions from Semantic Memory without LLM hallucination. |
| **Episodic Recall** | `Sự cố quá nhiệt máy CNC-01 trước đây xử lý thế nào?` | Recalls past maintenance episodes and repair steps from Episodic Memory. |
| **HITL Safety Gate** | `Yêu cầu dừng khẩn cấp máy CNC-01` | Intercepts `write_plc_coil` and requests explicit Chief-Operator approval. |
| **Dynamic SQL** | `Cho tôi xem bảng máy móc sản xuất` | Executes `db_query_table` on columnar DataFrame and displays JSON record set. |

---

## 📁 Project Structure

```
samples/ZeroPlatform.Samples.ChatBot/
├── ChatBotCli.cs            # Terminal interactive REPL runner
├── ChatBotEnvironment.cs    # Runtime wiring (DialogEngine, HITL, DataFrames, Memory, Tools)
├── ChatBotForm.cs           # Dual-pane WinForms Studio (ZAiChatBox + Telemetry Tabs)
├── Program.cs               # Entrypoint with CLI vs GUI dispatch
└── ZeroPlatform.Samples.ChatBot.csproj
```
