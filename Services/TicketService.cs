using DoAnTotNghiep.Data;
using DoAnTotNghiep.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnTotNghiep.Services
{
    public class TicketService
    {
        private readonly ApplicationDbContext _context;

        public event Action? OnTicketsReadChanged;

        public void NotifyTicketsReadChanged()
        {
            try { OnTicketsReadChanged?.Invoke(); }
            catch { }
        }

        public TicketService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ticket>> GetUserTicketsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<Ticket>();

            return await _context.Tickets
                .Where(t => t.RequesterId == userId || t.AssigneeId == userId)
                .Include(t => t.TicketCategory)
                .Include(t => t.Requester)
                .Include(t => t.Assignee)
                .OrderByDescending(t => t.LastUpdatedAt)
                .ToListAsync();
        }

        public async Task<List<Ticket>> GetAllTicketsAsync()
        {
            return await _context.Tickets
                .Include(t => t.TicketCategory)
                .Include(t => t.Requester)
                .Include(t => t.Assignee)
                .OrderByDescending(t => t.LastUpdatedAt)
                .ToListAsync();
        }

        public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
                .Include(t => t.TicketCategory)
                .Include(t => t.Requester)
                .Include(t => t.Assignee)
                .Include(t => t.Comments).ThenInclude(c => c.Author)
                .Include(t => t.Attachments).ThenInclude(a => a.Uploader)
                .FirstOrDefaultAsync(t => t.Id == ticketId);
        }

        public async Task<List<TicketCategory>> GetTicketCategoriesAsync()
        {
            return await _context.TicketCategories.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task CreateTicketAsync(Ticket ticket)
        {
            if (ticket == null)
                throw new ArgumentNullException(nameof(ticket));

            ticket.Status = TicketStatus.Open;
            ticket.CreatedAt = DateTime.UtcNow;
            ticket.LastUpdatedAt = DateTime.UtcNow;

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
        }

        public async Task AddCommentAsync(TicketComment comment)
        {
            if (comment == null)
                throw new ArgumentNullException(nameof(comment));

            comment.CreatedAt = DateTime.UtcNow;

            _context.TicketComments.Add(comment);

            var parent = await _context.Tickets.FindAsync(comment.TicketId);
            if (parent != null)
                parent.LastUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task AssignTicketAsync(int ticketId, string? assigneeId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket != null)
            {
                ticket.AssigneeId = string.IsNullOrEmpty(assigneeId) ? null : assigneeId;
                ticket.LastUpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> UpdateTicketStatusAsync(int ticketId, TicketStatus newStatus)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return false;

            bool canChange = ticket.Status switch
            {
                TicketStatus.Open => true,
                TicketStatus.InProgress => newStatus is TicketStatus.Resolved or TicketStatus.Closed,
                TicketStatus.Resolved => newStatus == TicketStatus.Closed,
                TicketStatus.Closed => false,
                _ => false
            };

            if (!canChange) return false;

            ticket.Status = newStatus;
            ticket.LastUpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task AddAttachmentAsync(TicketAttachment attachment, Stream fileContentStream)
        {
            if (attachment == null || fileContentStream == null)
                throw new ArgumentNullException("Attachment or file content cannot be null.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "attachments");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{attachment.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await fileContentStream.CopyToAsync(fileStream);
            }

            attachment.FilePath = $"/attachments/{uniqueFileName}";
            attachment.UploadedAt = DateTime.UtcNow;

            _context.TicketAttachments.Add(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTicketAsync(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .Include(t => t.Watchers)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                return;

            foreach (var file in ticket.Attachments)
            {
                try
                {
                    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
                    if (File.Exists(physicalPath))
                    {
                        File.Delete(physicalPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Không thể xóa file: {ex.Message}");
                }
            }

            _context.TicketComments.RemoveRange(ticket.Comments);
            _context.TicketAttachments.RemoveRange(ticket.Attachments);
            _context.TicketWatchers.RemoveRange(ticket.Watchers);
            _context.Tickets.Remove(ticket);

            await _context.SaveChangesAsync();
        }

    }
}
