using MedInsuranceHelper.Api.Models;

namespace MedInsuranceHelper.Api.Services;

/// <summary>In-memory repository for InsuranceOffer records (v1 — no persistence layer).</summary>
public interface IOfferRepository
{
    void Add(InsuranceOffer offer);
    InsuranceOffer? Get(string id);
    IReadOnlyList<InsuranceOffer> GetAll();
    void Update(InsuranceOffer offer);
}

/// <summary>Thread-safe in-memory store for InsuranceOffer.</summary>
public class InMemoryOfferRepository : IOfferRepository
{
    private readonly Dictionary<string, InsuranceOffer> _store = new();
    private readonly object _lock = new();

    public void Add(InsuranceOffer offer)
    {
        lock (_lock) _store[offer.Id] = offer;
    }

    public InsuranceOffer? Get(string id)
    {
        lock (_lock) return _store.TryGetValue(id, out var o) ? o : null;
    }

    public IReadOnlyList<InsuranceOffer> GetAll()
    {
        lock (_lock) return _store.Values.ToList();
    }

    public void Update(InsuranceOffer offer)
    {
        lock (_lock) _store[offer.Id] = offer;
    }
}
