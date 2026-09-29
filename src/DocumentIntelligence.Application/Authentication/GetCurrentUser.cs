using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Authentication;

public sealed record GetCurrentUserQuery(UserId UserId) : IQuery<UserResponse>;

internal sealed class GetCurrentUserQueryHandler(IUserAccountService accounts)
    : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(query.UserId, cancellationToken);

        return account is null
            ? AuthErrors.UserNotFound
            : new UserResponse(account.Id.Value, account.Email);
    }
}
