using Resolvia.Application.DTOs.Attachment;
using Resolvia.Application.DTOs.Comment;
using Resolvia.Application.DTOs.Ticket;
using Resolvia.Application.Interfaces;
using Resolvia.Domain.Entities;
using Resolvia.Domain.Enums;

namespace Resolvia.Application.Services;

public class TicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFileStorageService _fileStorageService;

    public TicketService(
        ITicketRepository ticketRepository,
        ICategoryRepository categoryRepository,
        IFileStorageService fileStorageService)
    {
        _ticketRepository = ticketRepository;
        _categoryRepository = categoryRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<CommentResponse?> AddCommentAsync(Guid ticketId, CommentCreateRequest request, Guid authorId, UserRole authorRole)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return null;

        if (authorRole == UserRole.Cliente && ticket.RequesterId != authorId)
            throw new UnauthorizedAccessException("Você não tem permissão para comentar neste chamado.");

        var isInternal = authorRole != UserRole.Cliente && request.IsInternal;

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = authorId,
            Message = request.Message,
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow
        };

        await _ticketRepository.AddCommentAsync(comment);
        await _ticketRepository.SaveChangesAsync();

        // Recarrega os comentários do chamado e pega o que acabamos de criar, já com o nome do autor
        var allComments = await _ticketRepository.GetCommentsByTicketIdAsync(ticketId, includeInternal: true);
        var created = allComments.First(c => c.Id == comment.Id);

        return new CommentResponse
        {
            Id = created.Id,
            Message = created.Message,
            IsInternal = created.IsInternal,
            AuthorName = created.User?.Name ?? string.Empty,
            CreatedAt = created.CreatedAt
        };
    }
    public async Task<AttachmentResponse?> AddAttachmentAsync(Guid ticketId, Stream fileStream, string fileName, string contentType, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return null;

        if (role == UserRole.Cliente && ticket.RequesterId != userId)
            throw new UnauthorizedAccessException("Você não tem permissão para anexar arquivos neste chamado.");

        const long maxSizeBytes = 5 * 1024 * 1024; // 5 MB
        if (fileStream.Length > maxSizeBytes)
            throw new InvalidOperationException("O arquivo excede o tamanho máximo permitido (5 MB).");

        var fileUrl = await _fileStorageService.UploadFileAsync(fileStream, fileName, contentType);

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            FileName = fileName,
            FileUrl = fileUrl,
            UploadedAt = DateTime.UtcNow
        };

        await _ticketRepository.AddAttachmentAsync(attachment);
        await _ticketRepository.SaveChangesAsync();

        return new AttachmentResponse
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            FileUrl = attachment.FileUrl,
            UploadedAt = attachment.UploadedAt
        };
    }
    public async Task<List<CommentResponse>?> GetCommentsAsync(Guid ticketId, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return null;

        if (role == UserRole.Cliente && ticket.RequesterId != userId)
            throw new UnauthorizedAccessException("Você não tem permissão para ver os comentários deste chamado.");

        // Cliente nunca vê notas internas, mesmo sendo o dono do chamado
        var includeInternal = role != UserRole.Cliente;

        var comments = await _ticketRepository.GetCommentsByTicketIdAsync(ticketId, includeInternal);

        return comments.Select(c => new CommentResponse
        {
            Id = c.Id,
            Message = c.Message,
            IsInternal = c.IsInternal,
            AuthorName = c.User?.Name ?? string.Empty,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<List<TicketResponse>> GetVisibleTicketsAsync(Guid userId, UserRole role)
    {
        var tickets = role == UserRole.Cliente
            ? await _ticketRepository.GetByRequesterIdAsync(userId)
            : await _ticketRepository.GetAllAsync();

        return tickets.Select(MapToResponse).ToList();
    }
    public async Task<List<TicketHistoryResponse>?> GetHistoryAsync(Guid ticketId, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return null;

        // Mesma regra de visibilidade: Cliente só vê histórico dos próprios chamados
        if (role == UserRole.Cliente && ticket.RequesterId != userId)
            throw new UnauthorizedAccessException("Você não tem permissão para ver este histórico.");

        var history = await _ticketRepository.GetHistoryByTicketIdAsync(ticketId);

        return history.Select(h => new TicketHistoryResponse
        {
            FieldChanged = h.FieldChanged,
            OldValue = h.OldValue,
            NewValue = h.NewValue,
            ChangedByName = h.ChangedByUser?.Name ?? string.Empty,
            ChangedAt = h.ChangedAt
        }).ToList();
    }

    public async Task<TicketResponse?> GetByIdAsync(Guid id, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null) return null;

        // Cliente só pode ver o próprio chamado
        if (role == UserRole.Cliente && ticket.RequesterId != userId)
            throw new UnauthorizedAccessException("Você não tem permissão para ver este chamado.");

        return MapToResponse(ticket);
    }

    public async Task<TicketResponse> CreateAsync(TicketCreateRequest request, Guid requesterId)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        var slaHours = CalculateSlaHours(category.DefaultSlaHours, request.Priority);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Priority = request.Priority,
            Status = TicketStatus.Aberto,
            RequesterId = requesterId,
            CreatedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddHours(slaHours)
        };

        await _ticketRepository.AddAsync(ticket);
        await _ticketRepository.SaveChangesAsync();

        // Recarrega com os relacionamentos (Category, Requester) para montar a resposta
        var created = await _ticketRepository.GetByIdAsync(ticket.Id);
        return MapToResponse(created!);
    }

    public async Task<bool> UpdateStatusAsync(Guid id, TicketStatus newStatus, Guid changedByUserId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null) return false;

        var oldStatus = ticket.Status;
        if (oldStatus == newStatus) return true; // nada a fazer

        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (newStatus == TicketStatus.Resolvido)
            ticket.ResolvedAt = DateTime.UtcNow;

        _ticketRepository.Update(ticket);

        await _ticketRepository.AddHistoryAsync(new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = changedByUserId,
            FieldChanged = "Status",
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString(),
            ChangedAt = DateTime.UtcNow
        });

        await _ticketRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignAsync(Guid id, Guid atendenteId, Guid changedByUserId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null) return false;

        var oldAssignee = ticket.AssignedToId?.ToString() ?? "Nenhum";
        ticket.AssignedToId = atendenteId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _ticketRepository.Update(ticket);

        await _ticketRepository.AddHistoryAsync(new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = changedByUserId,
            FieldChanged = "AssignedTo",
            OldValue = oldAssignee,
            NewValue = atendenteId.ToString(),
            ChangedAt = DateTime.UtcNow
        });

        await _ticketRepository.SaveChangesAsync();
        return true;
    }

    private static double CalculateSlaHours(int categoryDefaultHours, TicketPriority priority)
    {
        return priority switch
        {
            TicketPriority.Urgente => categoryDefaultHours * 0.25,
            TicketPriority.Alta => categoryDefaultHours * 0.5,
            TicketPriority.Media => categoryDefaultHours * 1.0,
            TicketPriority.Baixa => categoryDefaultHours * 1.5,
            _ => categoryDefaultHours
        };
    }

    private static TicketResponse MapToResponse(Ticket ticket)
    {
        return new TicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            CategoryName = ticket.Category?.Name ?? string.Empty,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            RequesterName = ticket.Requester?.Name ?? string.Empty,
            AssignedToName = ticket.AssignedTo?.Name,
            CreatedAt = ticket.CreatedAt,
            DueDate = ticket.DueDate,
            IsOverdue = ticket.Status != TicketStatus.Resolvido
                && ticket.Status != TicketStatus.Fechado
                && DateTime.UtcNow > ticket.DueDate
        };
    }
}