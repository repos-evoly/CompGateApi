using BlockingApi.Core.Dtos;
using BlockingApi.Data.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlockingApi.Core.Abstractions
{
    public interface IDocumentRepository
    {
        Task<IEnumerable<DocumentResponseDto>> GetDocuments(string documentType);
        Task<DocumentResponseDto?> GetDocumentById(Guid id);
        Task<bool> UploadDocument(IFormFile file, DocumentDto documentDto);
        Task<bool> DeleteDocument(Guid id);
        Task<IEnumerable<DocumentResponseDto>> SearchDocuments(string searchBy, string query);

    }
}
