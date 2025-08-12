// CompGateApi.Data.Repositories/AttachmentRepository.cs
using AutoMapper;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class AttachmentRepository : IAttachmentRepository
    {
        private readonly CompGateApiDbContext _db;
        private readonly IMapper _mapper;

        public AttachmentRepository(CompGateApiDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        // CompGateApi.Infrastructure/Repositories/AttachmentRepository.cs
        public async Task<IEnumerable<AttachmentDto>> GetByCompany(int companyId, string? subject = null)
        {
            var query = _db.Attachments.Where(a => a.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(subject))
                query = query.Where(a => a.AttSubject.Contains(subject));

            var list = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return list.Select(_mapper.Map<AttachmentDto>);
        }

        // ← New overload:
        public async Task<AttachmentDto> Upload(
     IFormFile file,
     int companyId,
     string subject,
     string description,
     string createdBy)
        {


            // ensure folder
            var dir = Path.Combine("Attachments", companyId.ToString());
            Directory.CreateDirectory(dir);

            // unique filename
            var fn = $"{Guid.NewGuid()}-{Path.GetFileName(file.FileName).Replace(" ", "_")}";
            var full = Path.Combine(dir, fn);

            using var stream = File.Create(full);
            await file.CopyToAsync(stream);

            var att = new Attachment
            {
                CompanyId = companyId,
                AttSubject = subject,
                AttOriginalFileName = file.FileName,
                AttFileName = fn,
                AttMime = file.ContentType,
                AttSize = (int)file.Length,
                AttUrl = full,
                Description = description,
                CreatedBy = createdBy
            };

            _db.Attachments.Add(att);
            await _db.SaveChangesAsync();

            return _mapper.Map<AttachmentDto>(att);
        }

        public async Task<AttachmentDto> Delete(Guid id)
        {
            var att = await _db.Attachments.FindAsync(id);
            if (att == null)
                throw new InvalidOperationException($"Attachment with id {id} not found.");

            if (File.Exists(att.AttUrl))
                File.Delete(att.AttUrl);

            _db.Attachments.Remove(att);
            await _db.SaveChangesAsync();

            return _mapper.Map<AttachmentDto>(att);
        }

        public async Task LinkToVisaRequestAsync(Guid attachmentId, int visaRequestId)
        {
            var att = await _db.Attachments.FindAsync(attachmentId);
            if (att == null)
                throw new InvalidOperationException($"Attachment {attachmentId} not found.");

            att.VisaRequestId = visaRequestId;
            await _db.SaveChangesAsync();
        }

        public async Task LinkToCblRequestAsync(Guid attachmentId, int cblRequestId)
        {
            var att = await _db.Attachments.FindAsync(attachmentId);
            if (att == null)
                throw new InvalidOperationException($"Attachment {attachmentId} not found.");

            att.CblRequestId = cblRequestId;
            await _db.SaveChangesAsync();
        }
    }
}
