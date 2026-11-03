using RevEx.Domain.Contracts.Comments;
namespace RevEx.Application.Comments;
public interface ICommentStore
{
    Task<IReadOnlyList<CommentSetDto>> GetSetsAsync(int offset, int limit, CancellationToken token);
    Task<CommentSetDto?> GetSetAsync(Guid id, CancellationToken token);
    Task<CommentSetDto> CreateSetAsync(string name, string authorId, string author, CancellationToken token);
    Task<IReadOnlyList<CommentDto>?> GetCommentsAsync(Guid setId, int offset, int limit, CancellationToken token);
    Task<CommentDto?> AddAsync(Guid setId, string text, string authorId, string author, CancellationToken token);
    Task<CommentDto?> UpdateAsync(Guid setId, Guid id, string text, string authorId, CancellationToken token);
    Task<bool> DeleteAsync(Guid setId, Guid id, string authorId, CancellationToken token);
}
