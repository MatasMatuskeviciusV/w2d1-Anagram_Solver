using AnagramSolver.BusinessLogic;
using AnagramSolver.BusinessLogic.Decorators;
using AnagramSolver.Contracts;
using AnagramSolver.EF.CodeFirst.Data;
using AnagramSolver.EF.CodeFirst.Repositories;
using AnagramSolver.WebApp.GraphQL;
using AnagramSolver.WebApp.Plugins;
using AnagramSolver.WebApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

//transient singleton ir scoped

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddKernel();
builder.Services.AddOpenAIChatCompletion(
    modelId: builder.Configuration["OpenAI:Model"]!,
    apiKey: builder.Configuration["OpenAI:ApiKey"]!);

builder.Services.AddScoped<AnagramPlugin>();
builder.Services.AddScoped<TimePlugin>();
builder.Services.AddSingleton<IChatHistoryRepository, InMemoryChatHistoryRepository>();
builder.Services.AddScoped<IAiChatService, AiChatService>();

builder.Services.AddDbContext<AnagramDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("AnagramDb");
    options.UseSqlServer(cs);
});

//builder.Services.AddScoped<IWordRepository, EfWordRepository>();
//builder.Services.AddScoped<ISearchLogRepository, EfSearchLogRepository>();


builder.Services.AddScoped<IWordRepository>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("AnagramDb");
    return new DapperWordRepository(cs);
});

builder.Services.AddScoped<ISearchLogRepository>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("AnagramDb");
    return new DapperSearchLogRepository(cs);
});

//builder.Services.AddScoped<IWordRepository>(sp =>
//{
//    var cfg = sp.GetRequiredService<IConfiguration>();
//    var env = sp.GetRequiredService<IWebHostEnvironment>();

//    var relative = cfg["Dictionary:WordFilePath"];
//    var fullPath = System.IO.Path.Combine(env.ContentRootPath, relative);

//    return new FileWordRepository(fullPath);
//});

builder.Services.AddScoped<IAnagramSolver>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var repo = sp.GetRequiredService<IWordRepository>();
    var logRepo = sp.GetRequiredService<ISearchLogRepository>();

    int maxResults = int.Parse(cfg["Settings:MaxResults"]);
    int maxWords = int.Parse(cfg["Settings:MaxWordsInAnagram"]);

    IAnagramSolver core = new DefaultAnagramSolver(repo, maxResults, maxWords);
    IAnagramSolver cached = new CachingAnagramSolver(core, maxResults, maxWords);

    return new LoggingAnagramSolver(cached, logRepo);
});

builder.Services.AddSingleton<UserProcessor>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    int minLen = int.Parse(cfg["Settings:MinUserWordLength"]);
    return new UserProcessor(minLen);
});

builder.Services.AddGraphQLServer().AddQueryType<Query>();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//builder.Services.AddTransient(IAnagramSolver, typeof(DefaultAnagramSolver))

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGraphQL("/graphql");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// No-op hosted service used only to trigger kernel plugin initialization during startup.
/// </summary>
internal class NoOpHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
