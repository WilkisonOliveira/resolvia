using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resolvia.Application.DTOs.Ticket;
using Resolvia.Application.Services;
using Resolvia.Domain.Enums;
using Resolvia.Application.DTOs.Comment;

namespace Resolvia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketController : ControllerBase
{
    private readonly TicketService _ticketService;

    public TicketController(TicketService ticketService)
    {
        _ticketService = ticketService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private UserRole CurrentUserRole =>
        Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tickets = await _ticketService.GetVisibleTicketsAsync(CurrentUserId, CurrentUserRole);
        return Ok(tickets);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var ticket = await _ticketService.GetByIdAsync(id, CurrentUserId, CurrentUserRole);
            if (ticket == null) return NotFound();
            return Ok(ticket);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        try
        {
            var history = await _ticketService.GetHistoryAsync(id, CurrentUserId, CurrentUserRole);
            if (history == null) return NotFound();
            return Ok(history);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(TicketCreateRequest request)
    {
        try
        {
            var ticket = await _ticketService.CreateAsync(request, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(Guid id, CommentCreateRequest request)
    {
        try
        {
            var comment = await _ticketService.AddCommentAsync(id, request, CurrentUserId, CurrentUserRole);
            if (comment == null) return NotFound();
            return CreatedAtAction(nameof(GetComments), new { id }, comment);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(Guid id)
    {
        try
        {
            var comments = await _ticketService.GetCommentsAsync(id, CurrentUserId, CurrentUserRole);
            if (comments == null) return NotFound();
            return Ok(comments);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, TicketUpdateStatusRequest request)
    {
        var updated = await _ticketService.UpdateStatusAsync(id, request.NewStatus, CurrentUserId);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id}/assign")]
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> Assign(Guid id, TicketAssignRequest request)
    {
        var assigned = await _ticketService.AssignAsync(id, request.AtendenteId, CurrentUserId);
        if (!assigned) return NotFound();
        return NoContent();
    }
    [HttpPost("{id}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var attachment = await _ticketService.AddAttachmentAsync(
                id, stream, file.FileName, file.ContentType, CurrentUserId, CurrentUserRole);

            if (attachment == null) return NotFound();
            return CreatedAtAction(nameof(GetById), new { id }, attachment);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}