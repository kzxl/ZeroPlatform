using System;
using System.Collections.Generic;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Core.Reasoning.Cognitive
{
    /// <summary>
    /// Multi-expert decision result synthesized from the Mixture of Reflexes (MoR).
    /// </summary>
    public sealed class ZabReflexSuiteResult
    {
        public string? RoutedAction { get; set; }
        public float RoutingConfidence { get; set; }
        public float SecurityRiskScore { get; set; }
        public bool IsBlockedBySecurity => SecurityRiskScore >= 0.80f;
        public float CacheAffinityScore { get; set; }
        public bool ShouldCheckCache => CacheAffinityScore >= 0.70f;
        public bool IsConfident => !IsBlockedBySecurity && RoutingConfidence >= 0.85f;

        public string? MatchedDomainId { get; set; }
        public float DomainAffinityScore { get; set; }
        public IDomainExpertModule? MatchedExpert { get; set; }
    }

    /// <summary>
    /// Mixture of Reflexes (MoR) Engine composed of gated domain-specific neural policies and modular domain experts.
    /// Distributes cognitive load across specialized INT8 micro-experts and plug-and-play domain clusters:
    /// 1. Action Routing Expert (determines optimal tool or action)
    /// 2. Security Guardrail Expert (detects dangerous commands and prompt injections)
    /// 3. Memory Retrieval Affinity Expert (predicts cache hits)
    /// 4. Modular Domain Experts (pluggable ERP/Industrial clusters e.g. Inventory, Finance, Production)
    /// </summary>
    public sealed class ZabMixtureOfReflexes
    {
        private ZabNeuralPolicy? _routingExpert;
        private ZabNeuralPolicy? _securityExpert;
        private ZabNeuralPolicy? _cacheExpert;
        private readonly Dictionary<string, IDomainExpertModule> _domainExperts = new Dictionary<string, IDomainExpertModule>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public ZabNeuralPolicy? RoutingExpert => _routingExpert;
        public ZabNeuralPolicy? SecurityExpert => _securityExpert;
        public ZabNeuralPolicy? CacheExpert => _cacheExpert;

        public IReadOnlyCollection<IDomainExpertModule> DomainExperts
        {
            get
            {
                lock (_lock)
                {
                    return new List<IDomainExpertModule>(_domainExperts.Values);
                }
            }
        }

        public ZabMixtureOfReflexes(
            ZabNeuralPolicy? routingExpert = null,
            ZabNeuralPolicy? securityExpert = null,
            ZabNeuralPolicy? cacheExpert = null)
        {
            _routingExpert = routingExpert;
            _securityExpert = securityExpert;
            _cacheExpert = cacheExpert;
        }

        public void RegisterRoutingExpert(ZabNeuralPolicy policy)
        {
            lock (_lock) _routingExpert = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void RegisterSecurityExpert(ZabNeuralPolicy policy)
        {
            lock (_lock) _securityExpert = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void RegisterCacheExpert(ZabNeuralPolicy policy)
        {
            lock (_lock) _cacheExpert = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void RegisterDomainExpert(IDomainExpertModule expert)
        {
            if (expert == null) throw new ArgumentNullException(nameof(expert));
            lock (_lock)
            {
                _domainExperts[expert.DomainId] = expert;
            }
        }

        public bool UnregisterDomainExpert(string domainId)
        {
            if (string.IsNullOrWhiteSpace(domainId)) return false;
            lock (_lock)
            {
                return _domainExperts.Remove(domainId);
            }
        }

        public bool TryGetDomainExpert(string domainId, out IDomainExpertModule? expert)
        {
            lock (_lock)
            {
                return _domainExperts.TryGetValue(domainId, out expert);
            }
        }

        /// <summary>
        /// Evaluates all specialized experts concurrently in sub-0.05ms reflex time.
        /// </summary>
        public ZabReflexSuiteResult Evaluate(ReadOnlySpan<float> inputEmbedding)
        {
            var result = new ZabReflexSuiteResult();
            if (inputEmbedding.IsEmpty) return result;

            lock (_lock)
            {
                // 1. Security Guardrail Evaluation
                if (_securityExpert != null && _securityExpert.InputDim == inputEmbedding.Length)
                {
                    result.SecurityRiskScore = _securityExpert.PredictScore(inputEmbedding, "Risk");
                    if (result.IsBlockedBySecurity)
                    {
                        return result; // Early exit on critical security violation
                    }
                }

                // 2. Action Routing Evaluation
                if (_routingExpert != null && _routingExpert.InputDim == inputEmbedding.Length)
                {
                    result.RoutedAction = _routingExpert.PredictChoice(inputEmbedding, out float conf);
                    result.RoutingConfidence = conf;
                }

                // 3. Cache Affinity Evaluation
                if (_cacheExpert != null && _cacheExpert.InputDim == inputEmbedding.Length)
                {
                    result.CacheAffinityScore = _cacheExpert.PredictScore(inputEmbedding, "CacheAffinity");
                }

                // 4. Modular MoE Domain Expert Matching
                if (_domainExperts.Count > 0)
                {
                    float bestScore = 0f;
                    IDomainExpertModule? bestExpert = null;

                    foreach (var expert in _domainExperts.Values)
                    {
                        float score = expert.EvaluateAffinity(inputEmbedding);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestExpert = expert;
                        }
                    }

                    if (bestExpert != null)
                    {
                        result.MatchedDomainId = bestExpert.DomainId;
                        result.DomainAffinityScore = bestScore;
                        result.MatchedExpert = bestExpert;
                    }
                }
            }

            return result;
        }
    }
}
