// CompGateApi.Core.Abstractions/IAttachmentRepository.cs
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Core.Dtos;

public interface IAttachmentRepository
{
  Task<IEnumerable<AttachmentDto>> GetByCompany(int companyId, string? subject = null);  // 🔸 add param
  Task<AttachmentDto> Upload(
         IFormFile file,
         int companyId,
         string subject,
         string description,
         string createdBy
     ); Task<AttachmentDto> Delete(Guid id);

  Task LinkToVisaRequestAsync(Guid attachmentId, int visaRequestId);

  Task LinkToCblRequestAsync(Guid attachmentId, int cblRequestId);
  Task LinkToEdfaaliRequestAsync(Guid attachmentId, int edfaaliRequestId);

  Task<IEnumerable<AttachmentDto>> GetByVisa(int visaId);
  Task<AttachmentDto> UploadForVisa(IFormFile file, int visaId, string subject, string description, string createdBy);
  Task<IReadOnlyList<AttachmentDto>> UploadForVisaBatch(IFormFileCollection files, int visaId, string[] subjects, string[] descriptions, string createdBy);
  Task LinkToVisaAsync(Guid attachmentId, int visaId);
  Task UnlinkFromVisaAsync(Guid attachmentId);



}
