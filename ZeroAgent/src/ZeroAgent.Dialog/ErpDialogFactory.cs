using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroAgent.Core.Reasoning.Cognitive;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Dialog
{
    /// <summary>
    /// Factory providing pre-configured Enterprise Resource Planning (ERP) Chatbot dialog engines
    /// with built-in Modular Mixture-of-Experts (MoE) domain routing and Vietnamese business intents:
    /// 1. Inventory / Kho vận (Tồn kho, Chuyển kho, Thẻ kho, SKU)
    /// 2. Sales / Bán hàng (Đơn hàng SO, Công nợ khách hàng, Báo giá)
    /// 3. Purchasing / Mua hàng (Đơn mua PO, Nhà cung cấp, Đề xuất mua)
    /// 4. Production / Sản xuất (Lệnh sản xuất MO, Định mức BOM, Tiến độ)
    /// 5. Finance / Kế toán (Sổ quỹ, Hóa đơn VAT, Thu chi)
    /// </summary>
    public static class ErpDialogFactory
    {
        public static ZeroDialogEngine CreateErpBot(
            AgentToolRegistry? registry = null,
            HitlSafetyGate? safetyGate = null,
            int dimension = 128)
        {
            safetyGate ??= new HitlSafetyGate();
            registry ??= new AgentToolRegistry();

            var engine = new ZeroDialogEngine(registry, safetyGate, dimension);
            var embedder = engine.Memory.Embedder;

            // Initialize MoE Reflex Suite
            var moe = new ZabMixtureOfReflexes();

            // 1. INVENTORY DOMAIN EXPERT
            var invExpert = new DomainExpertModule(
                "inventory",
                "Quản lý Kho Vận",
                "Chuyên gia xử lý tồn kho, nhập xuất, vị trí lưu kho và định mức tồn SKU",
                new[] { "CHECK_INVENTORY", "TRANSFER_STOCK" },
                ComputeCentroid(embedder, "tồn kho kiểm tra số lượng tồn hàng trong kho vị trí thẻ kho xuất nhập vật tư sku")
            );
            moe.RegisterDomainExpert(invExpert);

            var checkInvIntent = new DialogueIntent("CHECK_INVENTORY", "Kiểm tra số lượng tồn kho của mặt hàng")
                .WithDomain("inventory")
                .AddSamples(
                    "kiểm tra tồn kho",
                    "xem tồn kho",
                    "tồn kho",
                    "còn bao nhiêu hàng",
                    "hàng còn trong kho không",
                    "tra cứu tồn kho mã",
                    "kiểm tra số lượng tồn",
                    "check inventory",
                    "xem số lượng còn lại")
                .RequireSlot("item_code", "Vui lòng cho biết mã sản phẩm hoặc SKU cần kiểm tra tồn kho (ví dụ: SKU-STEEL-01, VT-100):")
                .AddOptionalSlot("warehouse_id")
                .AddTemplates(
                    "Sản phẩm {{item_code}} tại {{warehouse_id}} hiện có {{output}}.",
                    "Hệ thống kho ghi nhận mã hàng {{item_code}}: {{output}}.",
                    "Báo cáo tồn kho: {{item_code}} có trạng thái: {{output}}.");

            checkInvIntent.ActionHandler = session =>
            {
                session.TryGetSlot("item_code", out var sku);
                session.TryGetSlot("warehouse_id", out var wh);
                string whName = string.IsNullOrWhiteSpace(wh) ? "Kho Tổng" : wh;
                return Task.FromResult($"500 cái (Tồn khả dụng: 420 cái, Đang giữ chỗ: 80 cái tại {whName})");
            };
            engine.Dst.RegisterIntent(checkInvIntent);

            // 2. SALES DOMAIN EXPERT
            var salesExpert = new DomainExpertModule(
                "sales",
                "Quản lý Bán Hàng & Phân Phối",
                "Chuyên gia theo dõi đơn bán SO, tiến độ giao hàng, báo giá và công nợ khách hàng",
                new[] { "CHECK_SO_STATUS", "GET_CUSTOMER_BALANCE" },
                ComputeCentroid(embedder, "bán hàng đơn hàng so khách hàng báo giá giao hàng doanh số công nợ phải thu")
            );
            moe.RegisterDomainExpert(salesExpert);

            var checkSoIntent = new DialogueIntent("CHECK_SO_STATUS", "Kiểm tra tiến độ đơn hàng bán (SO)")
                .WithDomain("sales")
                .AddSamples(
                    "kiểm tra đơn bán hàng",
                    "tiến độ đơn hàng",
                    "tình trạng đơn so",
                    "xem đơn hàng so",
                    "đơn hàng bán tới đâu rồi",
                    "đơn so đã giao chưa",
                    "check so status",
                    "tình trạng đơn hàng")
                .RequireSlot("so_number", "Vui lòng nhập mã đơn hàng SO cần tra cứu (ví dụ: SO-2026-001):")
                .AddTemplates(
                    "Đơn hàng {{so_number}} hiện tại: {{output}}.",
                    "Thông tin đơn bán {{so_number}}: {{output}}.");

            checkSoIntent.ActionHandler = session =>
            {
                session.TryGetSlot("so_number", out var so);
                return Task.FromResult($"Đã xuất kho 80%, dự kiến giao hàng vào 15:30 chiều nay.");
            };
            engine.Dst.RegisterIntent(checkSoIntent);

            var customerBalanceIntent = new DialogueIntent("GET_CUSTOMER_BALANCE", "Tra cứu công nợ của khách hàng")
                .WithDomain("sales")
                .AddSamples(
                    "công nợ khách hàng",
                    "khách hàng còn nợ bao nhiêu",
                    "tra cứu công nợ",
                    "số tiền khách nợ",
                    "công nợ của cty",
                    "hạn mức công nợ khách")
                .RequireSlot("customer_id", "Bạn muốn kiểm tra công nợ của khách hàng hoặc đối tác nào?")
                .AddTemplates(
                    "Công nợ của {{customer_id}}: {{output}}.",
                    "Số dư công nợ ghi nhận cho {{customer_id}} là {{output}}.");

            customerBalanceIntent.ActionHandler = session =>
            {
                session.TryGetSlot("customer_id", out var cust);
                return Task.FromResult($"145.000.000 VNĐ (Trong hạn: 120.000.000 VNĐ, Quá hạn 15 ngày: 25.000.000 VNĐ)");
            };
            engine.Dst.RegisterIntent(customerBalanceIntent);

            // 3. PURCHASING DOMAIN EXPERT
            var purchExpert = new DomainExpertModule(
                "purchasing",
                "Quản lý Mua Hàng & Cung Ứng",
                "Chuyên gia quản lý đơn đặt mua PO, nhà cung cấp và tiến độ giao vật tư",
                new[] { "CHECK_PO_STATUS" },
                ComputeCentroid(embedder, "mua hàng đơn mua hàng po nhà cung cấp đề xuất mua hợp đồng nhập khẩu giao hàng")
            );
            moe.RegisterDomainExpert(purchExpert);

            var checkPoIntent = new DialogueIntent("CHECK_PO_STATUS", "Kiểm tra tình trạng đơn mua hàng (PO)")
                .WithDomain("purchasing")
                .AddSamples(
                    "kiểm tra đơn mua hàng",
                    "tiến độ po",
                    "đơn mua hàng po",
                    "nhà cung cấp đã giao hàng chưa",
                    "đơn đặt hàng po tới đâu rồi",
                    "xem đơn po",
                    "check po status")
                .RequireSlot("po_number", "Vui lòng cho biết mã đơn mua PO (ví dụ: PO-2026-882):")
                .AddTemplates(
                    "Đơn đặt hàng {{po_number}} hiện có tình trạng: {{output}}.",
                    "Báo cáo đơn mua {{po_number}}: {{output}}.");

            checkPoIntent.ActionHandler = session =>
            {
                session.TryGetSlot("po_number", out var po);
                return Task.FromResult($"Nhà cung cấp đã xác nhận, hàng đang vận chuyển, dự kiến về kho ngày mai.");
            };
            engine.Dst.RegisterIntent(checkPoIntent);

            // 4. PRODUCTION DOMAIN EXPERT
            var prodExpert = new DomainExpertModule(
                "production",
                "Quản lý Sản Xuất & Chế Tạo",
                "Chuyên gia theo dõi lệnh sản xuất MO/WO, định mức BOM và năng suất chuyền",
                new[] { "CHECK_MO_PROGRESS", "QUERY_BOM" },
                ComputeCentroid(embedder, "sản xuất lệnh sản xuất mo wo định mức vật tư bom tiến độ chuyền may xưởng ca làm")
            );
            moe.RegisterDomainExpert(prodExpert);

            var checkMoIntent = new DialogueIntent("CHECK_MO_PROGRESS", "Kiểm tra tiến độ lệnh sản xuất (MO)")
                .WithDomain("production")
                .AddSamples(
                    "tiến độ sản xuất",
                    "lệnh sản xuất tới đâu rồi",
                    "kiểm tra lệnh sản xuất",
                    "tiến độ mo",
                    "tiến độ lệnh mo",
                    "tình hình chuyền sản xuất",
                    "check mo progress")
                .RequireSlot("mo_number", "Vui lòng cung cấp mã lệnh sản xuất MO (ví dụ: MO-2026-01):")
                .AddTemplates(
                    "Lệnh sản xuất {{mo_number}}: {{output}}.",
                    "Tiến độ sản xuất {{mo_number}} ghi nhận: {{output}}.");

            checkMoIntent.ActionHandler = session =>
            {
                session.TryGetSlot("mo_number", out var mo);
                return Task.FromResult($"Đã hoàn thành 65% kế hoạch (1.300 / 2.000 sản phẩm), đạt chuẩn chất lượng QC.");
            };
            engine.Dst.RegisterIntent(checkMoIntent);

            // 5. FINANCE DOMAIN EXPERT
            var finExpert = new DomainExpertModule(
                "finance",
                "Tài Chính & Kế Toán Doanh Nghiệp",
                "Chuyên gia sổ cái kế toán, sổ quỹ tiền mặt, tài khoản ngân hàng và hóa đơn VAT",
                new[] { "CHECK_CASH_BALANCE" },
                ComputeCentroid(embedder, "tài chính kế toán sổ quỹ tiền mặt ngân hàng hóa đơn vat phiếu thu phiếu chi số dư")
            );
            moe.RegisterDomainExpert(finExpert);

            var checkCashIntent = new DialogueIntent("CHECK_CASH_BALANCE", "Tra cứu số dư quỹ tiền mặt hoặc tài khoản ngân hàng")
                .WithDomain("finance")
                .AddSamples(
                    "kiểm tra số dư quỹ",
                    "số dư tiền mặt",
                    "tiền trong tài khoản còn bao nhiêu",
                    "xem số dư ngân hàng",
                    "số quỹ hiện tại",
                    "báo cáo tiền mặt",
                    "check cash balance")
                .AddTemplates(
                    "Báo cáo số dư tài chính: {{output}}.",
                    "Số dư quỹ hiện thời: {{output}}.");

            checkCashIntent.ActionHandler = session =>
            {
                return Task.FromResult($"Quỹ tiền mặt: 42.500.000 VNĐ | Tài khoản VCB: 1.850.320.000 VNĐ | Tài khoản TCB: 620.000.000 VNĐ");
            };
            engine.Dst.RegisterIntent(checkCashIntent);

            // Attach MoE router to dialog engine
            engine.MoEReflexSuite = moe;

            return engine;
        }

        private static float[] ComputeCentroid(ZeroAgent.Dialog.Embedding.LexicalSemanticEmbedder embedder, string text)
        {
            return embedder.Embed(text);
        }
    }
}
