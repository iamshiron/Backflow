using Shiron.Backflow.Ext.AspNetCore;
using Shiron.Backflow.Samples.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackflow((_, catalog) => catalog.Add<AddNode>("math.add"), options => {
    options.AllowAnonymous = true;
    options.MaximumNodes = 64;
    options.MaximumEdges = 128;
});

var app = builder.Build();
app.MapBackflow("/api/graphs");
app.Run();
