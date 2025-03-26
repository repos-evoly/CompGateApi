using System.Collections.Generic;

namespace BlockingApi.Core.Dtos
{
    public class AreaDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public UserDto? HeadOfSection { get; set; }

        public List<BranchDto> Branches { get; set; } = new();
    }


    public class EditAreaDto
    {
        public string Name { get; set; } = string.Empty;

        public int? HeadOfSectionId { get; set; }

    }
}
