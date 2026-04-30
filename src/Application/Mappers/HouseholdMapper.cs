using Finance.Application.Contracts;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class HouseholdMapper
{
    public static HouseholdResponse ToResponse(Household household) => new(
        household.Id.Value,
        household.Name,
        household.Description,
        household.OwnerId.Value,
        household.CurrencyCode,
        household.IsActive,
        household.CreatedAt,
        household.UpdatedAt);
}
