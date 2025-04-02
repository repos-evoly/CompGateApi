using AutoMapper;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Context;
using BlockingApi.Core.Abstractions;

namespace BlockingApi.Core.Repositories
{
  public class StaticDataRepository : IStaticDataRepository
  {
    private readonly BlockingApiDbContext _db;
    private readonly IMapper _mapper;

    public StaticDataRepository(BlockingApiDbContext db, IMapper mapper)
    {
      _db = db;
      _mapper = mapper;
    }


  }
}