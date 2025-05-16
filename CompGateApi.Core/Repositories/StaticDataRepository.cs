using AutoMapper;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Core.Abstractions;

namespace CompGateApi.Core.Repositories
{
  public class StaticDataRepository : IStaticDataRepository
  {
    private readonly CompGateApiDbContext _db;
    private readonly IMapper _mapper;

    public StaticDataRepository(CompGateApiDbContext db, IMapper mapper)
    {
      _db = db;
      _mapper = mapper;
    }


  }
}