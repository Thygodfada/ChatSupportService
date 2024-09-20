using System;
using ChatSupportService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatSupportService.Data
{
    public class ApplicationDbContext : DbContext
    {

        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<Agent> Agents { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
    }
}
