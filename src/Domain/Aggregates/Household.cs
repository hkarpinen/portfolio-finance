using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Bills.Domain.Aggregates;

/// <summary>
/// Household aggregate root representing a group of members sharing expenses.
/// </summary>
public class Household
{
    private readonly List<DomainEvent> _domainEvents = new();

    public HouseholdId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public UserId OwnerId { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private Household()
    {
    }

    public static Household Create(string name, UserId ownerId, string currencyCode = "USD", string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        var household = new Household
        {
            Id = HouseholdId.New(),
            Name = name,
            Description = description,
            OwnerId = ownerId,
            CurrencyCode = currencyCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        household._domainEvents.Add(new HouseholdCreated(household.Id, name, ownerId, currencyCode));

        return household;
    }

    public void Update(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new HouseholdUpdated(Id, name));
    }

    public void TransferOwnership(UserId newOwnerId)
    {
        if (newOwnerId.Value == Guid.Empty)
            throw new ArgumentException("Invalid user ID.", nameof(newOwnerId));

        OwnerId = newOwnerId;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new HouseholdOwnershipTransferred(Id, newOwnerId));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Household is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new HouseholdDeleted(Id));
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Household is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
