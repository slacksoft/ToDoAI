using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5293");

builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

_ = Task.Run(async () =>
{
    await Task.Delay(1000);
    try
    {
        Process.Start(new ProcessStartInfo { FileName = "http://localhost:5293", UseShellExecute = true });
    }
    catch { }
});

Console.WriteLine("ToDoWebUI started at http://localhost:5293");
Console.WriteLine("Opening browser...");

app.Run();
