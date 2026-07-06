namespace Resolvia.Application.DTOs.Comment;

public class CommentCreateRequest
{
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
}