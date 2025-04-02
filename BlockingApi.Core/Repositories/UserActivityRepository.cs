using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class UserActivityRepository : IUserActivityRepository
    {
        private readonly BlockingApiDbContext _context;

        public UserActivityRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        public async Task<bool> UpdateUserStatus(int userId, string status)
        {
            var activity = await _context.UserActivities.FirstOrDefaultAsync(u => u.UserId == userId);
            if (activity == null)
            {
                activity = new UserActivity
                {
                    UserId = userId,
                    Status = status,
                    LastActivityTime = DateTime.UtcNow
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                activity.Status = status;
                activity.LastActivityTime = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserActivity?> GetUserActivity(int userId)
        {
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<List<UserActivity>> GetAllUserActivitiesForUser(int userId)
        {
            // Fetch the current user's branch first.
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.BranchId })
                .FirstOrDefaultAsync();

            if (user == null)
                return new List<UserActivity>();

            int branchId = user.BranchId;

            // Return activities filtered by the branch.
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .Where(u => u.User.BranchId == branchId)
                .ToListAsync();
        }

        public async Task<List<UserActivity>> GetAllActivities()
        {
            // Return all user activities with related user and branch info.
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .ToListAsync();
        }

        public async Task<List<UserActivity>> GetActivitiesByArea(int areaId)
        {
            // Return activities for users whose branch belongs to the specified area.
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .Where(u => u.User.Branch.AreaId == areaId)
                .ToListAsync();
        }
    }
}
