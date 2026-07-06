namespace Resolvia.Application.DTOs.Comment;

public class CommentResponse
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}