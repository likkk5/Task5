using MusicStoreShowcase.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<DataGenerator>();
builder.Services.AddSingleton<ICoverGenerator, CoverGenerator>();
builder.Services.AddSingleton<MusicGenerator>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
