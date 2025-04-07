using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly BlockingApiDbContext _context;
        private const string UploadDirectory = "Attachments";

        public DocumentRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DocumentResponseDto>> GetDocuments(string documentType)
        {
            return await _context.Documents
                .Where(d => d.DocumentType == documentType)
                .Include(d => d.UploadedBy)
                .Select(d => new DocumentResponseDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    Description = d.Description,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    OriginalFileName = d.OriginalFileName,
                    FileMimeType = d.FileMimeType,
                    FileSize = d.FileSize,
                    FilePath = d.FilePath,
                    UploadedAt = d.UploadedAt,
                    UploadedByUserId = d.UploadedByUserId,
                    UploadedBy = d.UploadedBy.FirstName + " " + d.UploadedBy.LastName
                })
                .ToListAsync();
        }

        public async Task<DocumentResponseDto?> GetDocumentById(Guid id)
        {
            return await _context.Documents
                .Where(d => d.Id == id)
                .Include(d => d.UploadedBy)
                .Select(d => new DocumentResponseDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    Description = d.Description,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    OriginalFileName = d.OriginalFileName,
                    FileMimeType = d.FileMimeType,
                    FileSize = d.FileSize,
                    FilePath = d.FilePath,
                    UploadedAt = d.UploadedAt,
                    UploadedByUserId = d.UploadedByUserId,
                    UploadedBy = d.UploadedBy.FirstName + " " + d.UploadedBy.LastName
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UploadDocument(IFormFile file, DocumentDto documentDto)
        {
            var filePath = Path.Combine(UploadDirectory, $"{Guid.NewGuid()}_{file.FileName}");
            Directory.CreateDirectory(UploadDirectory);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var document = new Document
            {
                Title = documentDto.Title,
                Description = documentDto.Description,
                DocumentType = documentDto.DocumentType,
                FileName = Path.GetFileName(filePath),
                OriginalFileName = file.FileName,
                FileMimeType = file.ContentType,
                FileSize = (int)file.Length,
                FilePath = filePath,
                UploadedByUserId = documentDto.UploadedByUserId
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteDocument(Guid id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null) return false;

            if (File.Exists(document.FilePath))
            {
                File.Delete(document.FilePath);
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<DocumentResponseDto>> SearchDocuments(string searchBy, string query)
        {
            // Start with the documents set including the UploadedBy navigation property.
            var docs = _context.Documents.Include(d => d.UploadedBy).AsQueryable();

            switch (searchBy.ToLower())
            {
                case "title":
                    docs = docs.Where(d => d.Title.Contains(query));
                    break;
                case "description":
                    docs = docs.Where(d => d.Description != null && d.Description.Contains(query));
                    break;
                case "filename":
                    docs = docs.Where(d => d.FileName.Contains(query));
                    break;
                default:
                    // If searchBy is not recognized, return an empty list.
                    return new List<DocumentResponseDto>();
            }

            return await docs.Select(d => new DocumentResponseDto
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                OriginalFileName = d.OriginalFileName,
                FileMimeType = d.FileMimeType,
                FileSize = d.FileSize,
                FilePath = d.FilePath,
                UploadedAt = d.UploadedAt,
                UploadedByUserId = d.UploadedByUserId,
                UploadedBy = d.UploadedBy.FirstName + " " + d.UploadedBy.LastName
            }).ToListAsync();
        }

    }
}
