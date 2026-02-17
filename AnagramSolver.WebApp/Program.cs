using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using AnagramSolver.WebApp.GraphQL;
using AnagramSolver.BusinessLogic.Decorators;

var builder = WebApplication.CreateBuilder(args);

//transient singleton ir scoped

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IWordRepository>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();

    var relative = cfg["Dictionary:WordFilePath"];
    var fullPath = System.IO.Path.Combine(env.ContentRootPath, relative);

    return new FileWordRepository(fullPath);
});

builder.Services.AddScoped<IAnagramSolver>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var repo = sp.GetRequiredService<IWordRepository>();

    int maxResults = int.Parse(cfg["Settings:MaxResults"]);
    int maxWords = int.Parse(cfg["Settings:MaxWordsInAnagram"]);

    IAnagramSolver core = new DefaultAnagramSolver(repo, maxResults, maxWords);
    return new DefaultAnagramSolver(repo, maxResults, maxWords);
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

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
