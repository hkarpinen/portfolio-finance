namespace Finance.Domain.ValueObjects;

/// <summary>
/// Which of two id spaces an owner id came from. NOT a switch anything is allowed to branch on:
/// a ledger records money in and out and does not care whose it is, so the chart, the journalLine
/// engine and every balance are identical either way.
///
/// It exists because a household id and a person id are both bare Guids, and without saying which
/// one you are holding, "is this mine?" could answer yes for a house.
/// </summary>
public enum EntityKind
{
    Household,
    Person
}

/// <summary>
/// Whose books something belongs to: an id, and which space it came from.
///
/// This replaces the pairs that used to say the same thing twice — a nullable GroupId doubling as
/// a discriminator with a UserId that meant the owner in one case and the author in another, and a
/// ledger's OwnerType beside its OwnerId. A expense, a schedule and a ledger all belong to an
/// entity; nothing else about them turns on which kind it is.
/// </summary>
public readonly record struct AccountingEntity(EntityKind Kind, Guid Id)
{
    public static AccountingEntity Household(Guid groupId) => new(EntityKind.Household, groupId);
    public static AccountingEntity Household(GroupId groupId) => new(EntityKind.Household, groupId.Value);
    public static AccountingEntity Person(Guid userId) => new(EntityKind.Person, userId);
    public static AccountingEntity Person(UserId userId) => new(EntityKind.Person, userId.Value);

    public bool IsHousehold => Kind == EntityKind.Household;
    public bool IsPerson => Kind == EntityKind.Person;

    /// <summary>The household id when this is one, null otherwise — for the places that still speak
    /// in group ids, chiefly the events other services consume.</summary>
    public GroupId? AsGroup => IsHousehold ? ValueObjects.GroupId.Create(Id) : null;

    /// <summary>The person's id when this is one, null otherwise.</summary>
    public UserId? AsPerson => IsPerson ? UserId.Create(Id) : null;

    /// <summary>True when this entity is that person — false for a household, whoever is in it.
    /// Being in a house does not make its bills yours.</summary>
    public bool Is(Guid userId) => IsPerson && Id == userId;

    public override string ToString() => $"{Kind}:{Id:N}";
}
