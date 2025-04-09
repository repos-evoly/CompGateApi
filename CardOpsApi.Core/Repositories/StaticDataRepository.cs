using AutoMapper;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Context;
using CardOpsApi.Core.Abstractions;

namespace CardOpsApi.Core.Repositories
{
  public class StaticDataRepository : IStaticDataRepository
  {
    private readonly CardOpsApiDbContext _db;
    private readonly IMapper _mapper;

    public StaticDataRepository(CardOpsApiDbContext db, IMapper mapper)
    {
      _db = db;
      _mapper = mapper;
    }


  }
}