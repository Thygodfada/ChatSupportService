using ChatSupportService.Data.Interfaces;
using ChatSupportService.Data.Repository;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
// {
//     options.UseSqlServer("ChatSupportService");
// });
var app = builder.Build();

app.Run();
