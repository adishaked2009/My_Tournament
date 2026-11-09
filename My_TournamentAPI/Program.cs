using DAL;

var builder = WebApplication.CreateBuilder(args);

// הוספת שירותים ל-Container
builder.Services.AddControllers();

// קריאת מחרוזת החיבור ורישום ה-UserDB ב-Dependency Injection
string serverConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
string dbConnectionString = serverConnectionString + "Database=My_TournamentDB;";
builder.Services.AddSingleton<UserDB>(new UserDB(dbConnectionString));

var app = builder.Build();

// הפעלת אתחול בסיס הנתונים והטבלאות לפני שהשרת מתחיל לקבל בקשות
await DatabaseInitializer.InitializeAsync(serverConnectionString);

if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();