using CourseRegistration.Api.Services;
using CourseRegistration.Api.Store;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Singleton: the in-memory store IS the application's shared state and must persist
// across requests for the lifetime of the process. RegistrationService is a stateless
// delegation layer, so singleton is simplest there too.
builder.Services.AddSingleton<ICourseStore, InMemoryCourseStore>();
builder.Services.AddSingleton<IRegistrationService, RegistrationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
