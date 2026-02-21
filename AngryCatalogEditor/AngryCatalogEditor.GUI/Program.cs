using AngryCatalogEditor.GUI;
using AngryCatalogEditor.GUI.IO;
using Newtonsoft.Json;

if (ProjectPaths.rootPath == null)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine("Project path not found. Make sure that the application is run inside the AngryLevels repository clone.");
	Console.ForegroundColor = ConsoleColor.White;
	return -1;
}

AngryLevelsVersion versionObj = JsonConvert.DeserializeObject<AngryLevelsVersion>(File.ReadAllText(Path.Combine(ProjectPaths.rootPath, "AngryLevelsVersion.json")));
if (versionObj.Version < AppConfig.AngryLevelsVersion)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"This project is made for an older version of Angry Levels. App is {AppConfig.AngryLevelsName} ({AppConfig.AngryLevelsVersion}), repository is {versionObj.Name} ({versionObj.Version})");
	Console.ForegroundColor = ConsoleColor.White;
	return -1;
}

if (versionObj.Version < AppConfig.AngryLevelsVersion)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"This project is made for an earlier version of Angry Levels. App is {AppConfig.AngryLevelsName} ({AppConfig.AngryLevelsVersion}), repository is {versionObj.Name} ({versionObj.Version})");
	Console.ForegroundColor = ConsoleColor.White;
	return -1;
}

if (!GitHandler.Checkout())
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"Failed to checkout the '{GitHandler.MainBranchName}' branch.");
	Console.ForegroundColor = ConsoleColor.White;
	return -1;
}

if (GitHandler.Synced())
	GitHandler.Pull();

GitHandler.Fetch();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
return 0;
