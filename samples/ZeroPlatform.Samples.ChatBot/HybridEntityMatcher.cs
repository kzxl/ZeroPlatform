using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroAgent.Dialog.Learning;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class MasterCustomer
    {
        public string Code { get; }
        public string Name { get; }
        public string DefaultWarehouse { get; }
        public decimal CreditLimit { get; set; }
        public decimal CurrentDebt { get; set; }

        public MasterCustomer(string code, string name, string defaultWarehouse = "Kho 01", decimal creditLimit = 500_000_000, decimal currentDebt = 0)
        {
            Code = code;
            Name = name;
            DefaultWarehouse = defaultWarehouse;
            CreditLimit = creditLimit;
            CurrentDebt = currentDebt;
        }
    }

    public sealed class MasterProduct
    {
        public string Sku { get; }
        public string Name { get; }
        public decimal Price { get; set; }
        public string Unit { get; }
        public string PreferredWarehouse { get; }

        public MasterProduct(string sku, string name, decimal price, string unit = "Cái", string preferredWarehouse = "Kho 01")
        {
            Sku = sku;
            Name = name;
            Price = price;
            Unit = unit;
            PreferredWarehouse = preferredWarehouse;
        }
    }

    /// <summary>
    /// Dual-Engine Self-Learning Entity Matcher combining:
    /// 1. Exact Learned Alias Cache (Sub-0.1ms)
    /// 2. Normalized Token Similarity
    /// 3. Continuous Episodic Feedback Store
    /// </summary>
    public sealed class HybridEntityMatcher : IKnowledgeValidator
    {
        private readonly List<MasterCustomer> _customers = new();
        private readonly List<MasterProduct> _products = new();

        private readonly ConcurrentDictionary<string, string> _customerAliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _productAliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly VerifiedKnowledgeArbiter _arbiter;

        public VerifiedKnowledgeArbiter Arbiter => _arbiter;

        public HybridEntityMatcher()
        {
            _arbiter = new VerifiedKnowledgeArbiter(this);
            SeedMasterData();
            SeedInitialLearnedAliases();
        }

        private void SeedMasterData()
        {
            _customers.Add(new MasterCustomer("CUS-001", "Công ty TNHH nội thất Không Gian Mới", "Kho 01", creditLimit: 500_000_000, currentDebt: 142_500_000));
            _customers.Add(new MasterCustomer("CUS-002", "Công ty cổ phần Hoàng Bảo", "Kho 01", creditLimit: 300_000_000, currentDebt: 45_000_000));
            _customers.Add(new MasterCustomer("CUS-003", "Công ty cổ phần Hoàng Gia", "Kho 02", creditLimit: 1_000_000_000, currentDebt: 210_000_000));
            _customers.Add(new MasterCustomer("CUS-004", "Công ty TNHH MTV Phú Hưng", "Kho 01", creditLimit: 200_000_000, currentDebt: 185_000_000));
            _customers.Add(new MasterCustomer("CUS-005", "Công ty cổ phần Bình Minh", "Kho 01", creditLimit: 500_000_000, currentDebt: 85_000_000));
            _customers.Add(new MasterCustomer("CUS-006", "Tập đoàn Vinamilk", "Kho Tổng", creditLimit: 5_000_000_000, currentDebt: 1_200_000_000));
            _customers.Add(new MasterCustomer("CUS-007", "Chuỗi cửa hàng Bách Hóa Xanh", "Kho 01", creditLimit: 800_000_000, currentDebt: 320_000_000));
            _customers.Add(new MasterCustomer("CUS-008", "Công ty Cơ Khí Tân Tạo", "Kho 02", creditLimit: 600_000_000, currentDebt: 275_000_000));

            _products.Add(new MasterProduct("SP-GX01", "Ghế xoay văn phòng lưới cao cấp", 1_250_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-MO02", "Chuột máy tính không dây Logitech M185", 250_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-NC03", "Nồi chiên không dầu Lock&Lock 5.2L", 1_890_000, "Cái", "Kho 02"));
            _products.Add(new MasterProduct("SP-MC04", "Máy chiếu thông minh Full HD Mini", 3_450_000, "Bộ", "Kho 01"));
            _products.Add(new MasterProduct("IPRO-001", "Máy in phun nhiệt RYNAN i-PRO B1040H", 18_500_000, "Máy", "Kho 01"));
            _products.Add(new MasterProduct("SP-BLV05", "Bàn làm việc chân sắt chữ L", 2_450_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-MH06", "Màn hình máy tính Dell 24 inch IPS", 3_890_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-KB07", "Bàn phím cơ không dây Bluetooth", 1_450_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-QT08", "Quạt đứng công nghiệp Senko", 680_000, "Cây", "Kho 02"));
            _products.Add(new MasterProduct("SP-GIA09", "Thùng giấy in A4 Double A 70gsm", 380_000, "Thùng", "Kho Tổng"));
        }

        private void SeedInitialLearnedAliases()
        {
            // Seed customer aliases
            _customerAliases["Bình Minh"] = "CUS-005";
            _customerAliases["Cty Bình Minh"] = "CUS-005";
            _customerAliases["Không Gian Mới"] = "CUS-001";
            _customerAliases["Hoàng Bảo"] = "CUS-002";
            _customerAliases["Hoàng Gia"] = "CUS-003";
            _customerAliases["Phú Hưng"] = "CUS-004";
            _customerAliases["Vinamilk"] = "CUS-006";
            _customerAliases["Bách Hóa Xanh"] = "CUS-007";
            _customerAliases["Tân Tạo"] = "CUS-008";

            // Seed product aliases
            _productAliases["Ghế xoay"] = "SP-GX01";
            _productAliases["Chuột máy tính"] = "SP-MO02";
            _productAliases["Chuột"] = "SP-MO02";
            _productAliases["Nồi chiên không dầu"] = "SP-NC03";
            _productAliases["Nồi chiên"] = "SP-NC03";
            _productAliases["Máy chiếu"] = "SP-MC04";
            _productAliases["IPRO001_RYNAN i-PRO"] = "IPRO-001";
            _productAliases["RYNAN i-PRO"] = "IPRO-001";
            _productAliases["IPRO001"] = "IPRO-001";
            _productAliases["Bàn làm việc"] = "SP-BLV05";
            _productAliases["Màn hình Dell"] = "SP-MH06";
            _productAliases["Màn hình"] = "SP-MH06";
            _productAliases["Bàn phím cơ"] = "SP-KB07";
            _productAliases["Bàn phím"] = "SP-KB07";
            _productAliases["Quạt công nghiệp"] = "SP-QT08";
            _productAliases["Quạt"] = "SP-QT08";
            _productAliases["Giấy in"] = "SP-GIA09";
            _productAliases["Giấy A4"] = "SP-GIA09";
        }

        public (MasterCustomer? Customer, float Confidence) MatchCustomer(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return (null, 0f);
            string clean = query.Trim();

            // 1. Direct Verified Alias Resolution (Arbiter or local cache)
            if (_arbiter.TryResolveAlias(clean, "Customer", out var arbCode) && !string.IsNullOrEmpty(arbCode))
            {
                var match = _customers.FirstOrDefault(c => c.Code.Equals(arbCode, StringComparison.OrdinalIgnoreCase));
                if (match != null) return (match, 1.0f);
            }
            if (_customerAliases.TryGetValue(clean, out var code))
            {
                var match = _customers.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (match != null) return (match, 1.0f);
            }

            // 2. Exact or substring match in Master Data
            foreach (var cus in _customers)
            {
                if (cus.Name.IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf(cus.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LearnCustomerAlias(clean, cus.Code);
                    return (cus, 0.95f);
                }
            }

            // 3. Fallback: Word overlap
            var queryWords = clean.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            MasterCustomer? best = null;
            int maxOverlap = 0;

            foreach (var cus in _customers)
            {
                var nameWords = cus.Name.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
                int overlap = queryWords.Count(w => nameWords.Contains(w));
                if (overlap > maxOverlap)
                {
                    maxOverlap = overlap;
                    best = cus;
                }
            }

            if (best != null && maxOverlap >= 2)
            {
                float conf = Math.Min(0.9f, 0.6f + (maxOverlap * 0.1f));
                return (best, conf);
            }

            return (null, 0f);
        }

        public (MasterProduct? Product, float Confidence) MatchProduct(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return (null, 0f);
            string clean = query.Trim();

            // 1. Direct Verified Alias Resolution (Arbiter or local cache)
            if (_arbiter.TryResolveAlias(clean, "Product", out var arbSku) && !string.IsNullOrEmpty(arbSku))
            {
                var match = _products.FirstOrDefault(p => p.Sku.Equals(arbSku, StringComparison.OrdinalIgnoreCase));
                if (match != null) return (match, 1.0f);
            }
            if (_productAliases.TryGetValue(clean, out var sku))
            {
                var match = _products.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
                if (match != null) return (match, 1.0f);
            }

            // 2. Substring or code match
            foreach (var prod in _products)
            {
                if (prod.Sku.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    prod.Name.IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf(prod.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LearnProductAlias(clean, prod.Sku);
                    return (prod, 0.95f);
                }
            }

            // 3. Word overlap similarity
            var queryWords = clean.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            MasterProduct? best = null;
            int maxOverlap = 0;

            foreach (var prod in _products)
            {
                var nameWords = (prod.Sku + " " + prod.Name).ToLowerInvariant().Split(new[] { ' ', ',', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                int overlap = queryWords.Count(w => nameWords.Contains(w));
                if (overlap > maxOverlap)
                {
                    maxOverlap = overlap;
                    best = prod;
                }
            }

            if (best != null && maxOverlap >= 1)
            {
                float conf = Math.Min(0.92f, 0.5f + (maxOverlap * 0.2f));
                return (best, conf);
            }

            return (null, 0f);
        }

        private readonly ConcurrentDictionary<string, Dictionary<string, int>> _stockBalances = new(StringComparer.OrdinalIgnoreCase);

        public bool LearnCustomerAlias(string rawInput, string customerCode, string userId = "Operator", string role = "Operator")
        {
            if (string.IsNullOrWhiteSpace(rawInput) || string.IsNullOrWhiteSpace(customerCode)) return false;

            var candidate = _arbiter.ProposeAlias(rawInput, customerCode, "Customer", userId, role);
            if (candidate.Status == VerificationStatus.Quarantined)
            {
                // Defense activated: reject poisoned / non-existent / conflicting alias
                return false;
            }

            _customerAliases[rawInput.Trim()] = customerCode.Trim();
            return true;
        }

        public bool LearnProductAlias(string rawInput, string masterSku, string userId = "Operator", string role = "Operator")
        {
            if (string.IsNullOrWhiteSpace(rawInput) || string.IsNullOrWhiteSpace(masterSku)) return false;

            var candidate = _arbiter.ProposeAlias(rawInput, masterSku, "Product", userId, role);
            if (candidate.Status == VerificationStatus.Quarantined)
            {
                // Defense activated: reject poisoned / non-existent / conflicting alias
                return false;
            }

            _productAliases[rawInput.Trim()] = masterSku.Trim();
            return true;
        }

        public void AddCustomer(MasterCustomer customer)
        {
            if (customer == null) return;
            lock (_customers)
            {
                if (!_customers.Any(c => c.Code.Equals(customer.Code, StringComparison.OrdinalIgnoreCase)))
                {
                    _customers.Add(customer);
                    LearnCustomerAlias(customer.Name, customer.Code);
                }
            }
        }

        public void AddProduct(MasterProduct product)
        {
            if (product == null) return;
            lock (_products)
            {
                if (!_products.Any(p => p.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase)))
                {
                    _products.Add(product);
                    LearnProductAlias(product.Name, product.Sku);
                    LearnProductAlias(product.Sku, product.Sku);
                }
            }
        }

        public MasterCustomer RegisterOrGetCustomer(string name, string defaultWarehouse = "Kho 01", decimal creditLimit = 300_000_000)
        {
            var (existing, _) = MatchCustomer(name);
            if (existing != null) return existing;

            string clean = name.Trim();
            string code = "CUS-" + GenerateSlugCode(clean);
            var newCus = new MasterCustomer(code, clean, defaultWarehouse, creditLimit, currentDebt: 0);

            AddCustomer(newCus);
            return newCus;
        }

        public MasterProduct RegisterOrGetProduct(string name, decimal price = 0, string unit = "Cái", string warehouse = "Kho 01")
        {
            var (existing, _) = MatchProduct(name);
            if (existing != null)
            {
                if (price > 0 && existing.Price == 0) existing.Price = price;
                return existing;
            }

            string clean = name.Trim();
            string sku = "SP-" + GenerateSlugCode(clean);
            var newProd = new MasterProduct(sku, clean, price, unit, warehouse);

            AddProduct(newProd);
            return newProd;
        }

        public (int Wh01, int Wh02, int WhTong, int Available) GetProductStock(string sku)
        {
            if (_stockBalances.TryGetValue(sku, out var dict))
            {
                int w1 = dict.TryGetValue("Kho 01", out int v1) ? v1 : 0;
                int w2 = dict.TryGetValue("Kho 02", out int v2) ? v2 : 0;
                int wT = dict.TryGetValue("Kho Tổng", out int vT) ? vT : 0;
                int total = w1 + w2 + wT;
                int reserved = Math.Min(total, (int)(total * 0.1f));
                return (w1, w2, wT, total - reserved);
            }

            // Dynamic realistic stock calculation per SKU
            int hash = Math.Abs(sku.GetHashCode());
            int k1 = 15 + (hash % 65);
            int k2 = 5 + ((hash / 3) % 45);
            int kT = 25 + ((hash / 7) % 85);
            int tot = k1 + k2 + kT;
            int res = Math.Min(tot, 4 + (hash % 6));
            return (k1, k2, kT, tot - res);
        }

        public void AdjustStock(string sku, string warehouse, int delta)
        {
            var dict = _stockBalances.GetOrAdd(sku, _ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kho 01"] = 40,
                ["Kho 02"] = 20,
                ["Kho Tổng"] = 60
            });

            lock (dict)
            {
                int current = dict.TryGetValue(warehouse, out int val) ? val : 20;
                dict[warehouse] = Math.Max(0, current + delta);
            }
        }

        private static string GenerateSlugCode(string input)
        {
            var words = input.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1 && words[0].Length <= 6) return words[0].ToUpperInvariant();

            string initials = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            if (initials.Length < 3 && words.Length > 0 && words[0].Length >= 3)
                initials = words[0].Substring(0, 3).ToUpperInvariant();

            return (initials + DateTime.Now.ToString("ff")).Substring(0, Math.Min(8, initials.Length + 2));
        }

        public IReadOnlyList<MasterCustomer> Customers => _customers;
        public IReadOnlyList<MasterProduct> Products => _products;

        #region IKnowledgeValidator Implementation

        public bool ValidateEntityExists(string entityCategory, string entityCode)
        {
            if (string.IsNullOrWhiteSpace(entityCode)) return false;

            if (entityCategory.Equals("Product", StringComparison.OrdinalIgnoreCase))
            {
                return _products.Any(p => p.Sku.Equals(entityCode.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (entityCategory.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            {
                return _customers.Any(c => c.Code.Equals(entityCode.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            return true;
        }

        public bool ValidateSafetyBounds(string category, string ruleKey, string proposedValue, out string? violationReason)
        {
            violationReason = null;
            if (ruleKey.IndexOf("Discount", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (decimal.TryParse(proposedValue, out decimal discount) && (discount < 0 || discount > 0.50m))
                {
                    violationReason = "Tỷ lệ chiết khấu không được vượt quá 50% hoặc nhỏ hơn 0%.";
                    return false;
                }
            }
            if (ruleKey.IndexOf("Price", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (decimal.TryParse(proposedValue, out decimal price) && price <= 0)
                {
                    violationReason = "Đơn giá phải lớn hơn 0.";
                    return false;
                }
            }
            return true;
        }

        public bool CanCommitDirectly(string operatorRole)
        {
            return operatorRole.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) ||
                   operatorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
