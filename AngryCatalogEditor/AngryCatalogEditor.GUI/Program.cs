using AngryCatalogEditor.GUI;
using AngryCatalogEditor.GUI.IO;
using Newtonsoft.Json;
using System.Diagnostics;

if (ProjectPaths.rootPath == null)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"Project path not found. Make sure that the application is either run inside the AngryLevels repository or the repository is located at {Path.Combine(Directory.GetCurrentDirectory(), "AngryLevels")}");
	Console.ForegroundColor = ConsoleColor.White;

	Console.WriteLine("Press any key to close the application");
	Console.ReadKey();
	return -1;
}

if (!GitHandler.Checkout())
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"Failed to checkout the '{GitHandler.MainBranchName}' branch.");
	Console.ForegroundColor = ConsoleColor.White;

	Console.WriteLine("Press any key to close the application");
	Console.ReadKey();
	return -1;
}

try
{
	GitHandler.Fetch();

	if (GitHandler.Synced())
		GitHandler.Pull();
}
catch (Exception e)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"{e.GetType().Name}: {e.Message}");
	Console.WriteLine(e.StackTrace);
	Console.ForegroundColor = ConsoleColor.White;

	Console.WriteLine("Press any key to close the application");
	Console.ReadKey();
	return -1;
}

AngryLevelsVersion versionObj = JsonConvert.DeserializeObject<AngryLevelsVersion>(File.ReadAllText(Path.Combine(ProjectPaths.rootPath, "AngryLevelsVersion.json")));
if (versionObj.VersionObj < AppConfig.AngryLevelsVersion)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"This project is made for an older version of Angry Levels. App is {AppConfig.AngryLevelsName} ({AppConfig.AngryLevelsVersion}), repository is {versionObj.Name} ({versionObj.Version}). If you are running the project from Visual Studio, restart the application. Otherwise, please download the latest binaries or build the project from latest source.");
	Console.ForegroundColor = ConsoleColor.White;

	Console.WriteLine("Press any key to close the application");
	Console.ReadKey();
	return -1;
}

if (versionObj.VersionObj < AppConfig.AngryLevelsVersion)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"This project is made for an earlier version of Angry Levels. App is {AppConfig.AngryLevelsName} ({AppConfig.AngryLevelsVersion}), repository is {versionObj.Name} ({versionObj.Version}). Please download the latest binaries or build the project from latest source.");
	Console.ForegroundColor = ConsoleColor.White;

	Console.WriteLine("Press any key to close the application");
	Console.ReadKey();
	return -1;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseExceptionHandler("/Error");
// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

#if !DEBUG
Process.Start(new ProcessStartInfo()
{
	FileName = AppConfig.RootURL,
	UseShellExecute = true,
});
#endif

app.Run();
Console.WriteLine("Press any key to close the application");
Console.ReadKey();

return 0;
