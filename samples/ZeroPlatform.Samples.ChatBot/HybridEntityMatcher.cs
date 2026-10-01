using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class MasterCustomer
    {
        public string Code { get; }
        public string Name { get; }
        public string DefaultWarehouse { get; }
        public decimal CreditLimit { get; }

        public MasterCustomer(string code, string name, string defaultWarehouse = "Kho 01", decimal creditLimit = 500_000_000)
        {
            Code = code;
            Name = name;
            DefaultWarehouse = defaultWarehouse;
            CreditLimit = creditLimit;
        }
    }

    public sealed class MasterProduct
    {
        public string Sku { get; }
        public string Name { get; }
        public decimal Price { get; }
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
    public sealed class HybridEntityMatcher
    {
        private readonly List<MasterCustomer> _customers = new();
        private readonly List<MasterProduct> _products = new();

        private readonly ConcurrentDictionary<string, string> _customerAliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _productAliases = new(StringComparer.OrdinalIgnoreCase);

        public HybridEntityMatcher()
        {
            SeedMasterData();
            SeedInitialLearnedAliases();
        }

        private void SeedMasterData()
        {
            _customers.Add(new MasterCustomer("CUS-001", "Công ty TNHH nội thất Không Gian Mới", "Kho 01"));
            _customers.Add(new MasterCustomer("CUS-002", "Công ty cổ phần Hoàng Bảo", "Kho 01"));
            _customers.Add(new MasterCustomer("CUS-003", "Công ty cổ phần Hoàng Gia", "Kho 02"));
            _customers.Add(new MasterCustomer("CUS-004", "Công ty TNHH MTV Phú Hưng", "Kho 01"));
            _customers.Add(new MasterCustomer("CUS-005", "Công ty cổ phần Bình Minh", "Kho 01"));

            _products.Add(new MasterProduct("SP-GX01", "Ghế xoay văn phòng lưới cao cấp", 1_250_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-MO02", "Chuột máy tính không dây Logitech M185", 250_000, "Cái", "Kho 01"));
            _products.Add(new MasterProduct("SP-NC03", "Nồi chiên không dầu Lock&Lock 5.2L", 1_890_000, "Cái", "Kho 02"));
            _products.Add(new MasterProduct("SP-MC04", "Máy chiếu thông minh Full HD Mini", 3_450_000, "Bộ", "Kho 01"));
            _products.Add(new MasterProduct("IPRO-001", "Máy in phun nhiệt RYNAN i-PRO B1040H", 18_500_000, "Máy", "Kho 01"));
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
        }

        public (MasterCustomer? Customer, float Confidence) MatchCustomer(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return (null, 0f);
            string clean = query.Trim();

            // 1. Direct Alias Cache Hit (100% confidence)
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

            // 1. Direct Alias Cache Hit
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

        public void LearnCustomerAlias(string rawInput, string customerCode)
        {
            if (!string.IsNullOrWhiteSpace(rawInput) && !string.IsNullOrWhiteSpace(customerCode))
            {
                _customerAliases[rawInput.Trim()] = customerCode.Trim();
            }
        }

        public void LearnProductAlias(string rawInput, string masterSku)
        {
            if (!string.IsNullOrWhiteSpace(rawInput) && !string.IsNullOrWhiteSpace(masterSku))
            {
                _productAliases[rawInput.Trim()] = masterSku.Trim();
            }
        }

        public IReadOnlyList<MasterCustomer> Customers => _customers;
        public IReadOnlyList<MasterProduct> Products => _products;
    }
}
