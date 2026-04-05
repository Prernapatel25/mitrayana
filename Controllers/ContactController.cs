using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;
using Mitrayana.Api.Services;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ContactController> _logger;
        private readonly IEmailService _emailService;

        public ContactController(ApplicationDbContext db, ILogger<ContactController> logger, IEmailService emailService)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Contact contact)
        {
            contact.DateCreated = DateTime.UtcNow;
            contact.IsResolved = false;
            _db.Contacts.Add(contact);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Contact message submitted by {Name}: {Message}", contact.Name, contact.Message);
            return Ok(contact);
        }

        [HttpGet]
        [Authorize] // Only admin can view
        public async Task<IActionResult> GetAll()
        {
            var contacts = await _db.Contacts
                .OrderByDescending(c => c.DateCreated)
                .ToListAsync();
            return Ok(contacts);
        }

        [HttpPut("{id}/resolve")]
        [Authorize]
        public async Task<IActionResult> Resolve(int id)
        {
            var contact = await _db.Contacts.FindAsync(id);
            if (contact == null) return NotFound();
            
            contact.IsResolved = true;
            await _db.SaveChangesAsync();

            // Send resolution email to the user
            try
            {
                string subject = "Grievance Resolution Notification - Mitrayana";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <img src='http://localhost:5500/images/logo.png' alt='Mitrayana Logo' style='max-width: 200px; height: auto;'>
                        </div>
                        
                        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                            <h2 style='color: #2c3e50; margin: 0 0 10px 0; font-size: 24px;'>Grievance Resolution Notification</h2>
                            <p style='color: #6c757d; margin: 0; font-size: 14px;'>Reference ID: GR-{contact.ContactId:D6}</p>
                        </div>
                        
                        <div style='margin-bottom: 20px;'>
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 15px 0;'>Dear {contact.Name},</p>
                            
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 15px 0;'>
                                We are writing to inform you that the grievance you submitted to Mitrayana has been thoroughly reviewed and successfully resolved.
                            </p>
                            
                            <div style='background-color: #f1f3f4; padding: 15px; border-left: 4px solid #007bff; margin: 20px 0;'>
                                <h3 style='color: #495057; margin: 0 0 10px 0; font-size: 18px;'>Your Original Grievance:</h3>
                                <p style='margin: 0; font-style: italic; color: #6c757d;'>{contact.Message}</p>
                            </div>
                            
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 15px 0;'>
                                Our team has taken the necessary actions to address your concerns. We appreciate your patience during the resolution process and value your feedback in helping us improve our services.
                            </p>
                            
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 15px 0;'>
                                If you have any additional questions, require further clarification, or experience similar issues in the future, please do not hesitate to contact our support team.
                            </p>
                        </div>
                        
                        <div style='border-top: 1px solid #dee2e6; padding-top: 20px; margin-top: 30px;'>
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 10px 0;'>
                                Thank you for your understanding and continued trust in Mitrayana.
                            </p>
                            
                            <p style='font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;'>
                                Best regards,<br>
                                <strong>Mitrayana Customer Support Team</strong>
                            </p>
                            
                            <div style='background-color: #e9ecef; padding: 15px; border-radius: 6px; font-size: 14px; color: #6c757d;'>
                                <p style='margin: 0 0 5px 0;'><strong>Contact Information:</strong></p>
                                <p style='margin: 0 0 3px 0;'>Email: support@mitrayana.com</p>
                                <p style='margin: 0 0 3px 0;'>Phone: +1 (555) 123-4567</p>
                                <p style='margin: 0;'>Website: www.mitrayana.com</p>
                            </div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; font-size: 12px; color: #6c757d;'>
                            <p style='margin: 0;'>This is an automated message. Please do not reply to this email.</p>
                            <p style='margin: 5px 0 0 0;'>© 2026 Mitrayana. All rights reserved.</p>
                        </div>
                    </div>
                ";

                await _emailService.SendAsync(contact.Email, subject, body);
                _logger.LogInformation("Resolution email sent to {Email} for contact {ContactId}", contact.Email, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send resolution email for contact {ContactId}", id);
                // Don't fail the resolution if email fails
            }

            return Ok(contact);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var contact = await _db.Contacts.FindAsync(id);
                if (contact == null)
                {
                    return NotFound(new { message = "Grievance not found" });
                }

                _db.Contacts.Remove(contact);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Grievance {ContactId} deleted by admin", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting grievance {ContactId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}