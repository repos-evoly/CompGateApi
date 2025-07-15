// CompGateApi.Core.Abstractions/IAttachmentRepository.cs
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Core.Dtos;

public interface IAttachmentRepository
{
  Task<IEnumerable<AttachmentDto>> GetByCompany(int companyId);
  Task<AttachmentDto> Upload(
         IFormFile file,
         int companyId,
         string subject,
         string description,
         string createdBy
     ); Task<AttachmentDto> Delete(Guid id);

  Task LinkToVisaRequestAsync(Guid attachmentId, int visaRequestId);

  Task LinkToCblRequestAsync(Guid attachmentId, int cblRequestId);


}
