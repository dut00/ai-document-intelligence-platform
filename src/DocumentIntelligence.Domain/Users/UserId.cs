namespace DocumentIntelligence.Domain.Users;

/// <summary>
/// Identifies a user account. Accounts themselves are managed by ASP.NET Core Identity;
/// the domain only refers to them by id.
/// </summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
