using AngryCatalogEditor.GUI.Pages;
using ImageMagick.Drawing;
using LibGit2Sharp;

namespace AngryCatalogEditor.GUI.IO
{
	public static class GitHandler
	{
		public static string? username;
		public static string? email;

		public static readonly string MainBranchName = "release";

		public static readonly string[] CatalogFiles = new string[]
		{
			"Levels/",
			"V2/",
			"Scripts/",
			"ScriptCatalog.json",
			"ScriptCatalogHash.txt",
		};

		private static Repository? _repository;
		public static Repository? Repository
		{
			get
			{
				if (_repository != null)
					return _repository;

				_repository = new Repository(ProjectPaths.rootPath);
				return _repository;
			}
		}

		public static void Fetch()
		{
			if (Repository == null)
				return;

			var remote = Repository.Network.Remotes["origin"];
			if (remote == null)
				return;

			Commands.Fetch(Repository, "origin", remote.RefSpecs.Select(r => r.Specification), new FetchOptions(), "fetch");
		}

		public static void Pull()
		{
			if (Repository == null)
				return;

			Commands.Pull(Repository, new Signature("none", "none", DateTimeOffset.Now), new PullOptions() { MergeOptions = new MergeOptions() { FastForwardStrategy = FastForwardStrategy.FastForwardOnly } });
		}

		public static bool Checkout()
		{
			if (Repository == null)
				return false;

			Branch mainBranch = Repository.Branches[MainBranchName];
			if (mainBranch == null)
				return false;

			if (mainBranch.IsCurrentRepositoryHead)
				return true;

			var status = Repository.RetrieveStatus();
			if (status.IsDirty)
				return false;

			Commands.Checkout(Repository, mainBranch);
			return true;
		}

		public static bool Synced()
		{
			if (Repository == null)
				return false;

			Branch mainBranch = Repository.Branches[MainBranchName];
			if (mainBranch == null)
				return false;

			if (!mainBranch.IsCurrentRepositoryHead)
			{
				if (!Checkout())
					return false;
			}

			Fetch();

			var trackDetails = mainBranch.TrackingDetails;
			if (trackDetails.AheadBy > 0 && trackDetails.BehindBy > 0)
				return false;

			return true;
		}

		public static void ForceSync()
		{
			if (Repository == null)
				return;

			Fetch();
			Repository.Reset(ResetMode.Hard);

			var status = Repository.RetrieveStatus();
			foreach (var untrackedFile in status.Untracked)
			{
				string path = Path.Combine(Repository.Info.WorkingDirectory, untrackedFile.FilePath);
				if (File.Exists(path))
					File.Delete(path);
			}

			Checkout();
			Repository.Reset(ResetMode.Hard, Repository.Branches[$"origin/{MainBranchName}"].Tip);
		}

		public static void HardResetCatalogs()
		{
			if (Repository == null)
				return;

			var status = Repository.RetrieveStatus(new StatusOptions()
			{
				IncludeUntracked = true,
				RecurseUntrackedDirs = true,
			});

			// Clean new files

			foreach (var entry in status.Where(e => e.State.HasFlag(FileStatus.NewInWorkdir)))
			{
				if (!CatalogFiles.Any(path => entry.FilePath.StartsWith(path)))
					continue;

				string fullPath = Path.Combine(Repository.Info.WorkingDirectory, entry.FilePath);
				if (File.Exists(fullPath))
					File.Delete(fullPath);
				else if (Directory.Exists(fullPath))
					Directory.Delete(fullPath, true);
			}

			// Reset to last commit

			Repository.CheckoutPaths(Repository.Head.FriendlyName, CatalogFiles, new CheckoutOptions()
			{
				CheckoutModifiers = CheckoutModifiers.Force
			});
		}

		public static bool Commit(string message)
		{
			if (Repository == null)
				return false;

			if (Repository.Index.Count != 0)
			{
				Repository.Reset(ResetMode.Mixed, Repository.Head.Tip);
			}

			Commands.Stage(Repository, CatalogFiles);

			try
			{
				Repository.Commit(message, new Signature(username, email, DateTimeOffset.Now), new Signature(username, email, DateTimeOffset.Now));
			}
			catch (EmptyCommitException)
			{
				Console.WriteLine("Could not commit, no files were in the stage area");
				return false;
			}

			return true;
		}

		public static List<string> Changes()
		{
			if (Repository == null)
				return new List<string>();

			Branch mainBranch = Repository.Branches[MainBranchName];
			if (mainBranch == null)
				return new List<string>();

			var trackingDetails = mainBranch.TrackingDetails;
			List<string> result = new List<string>();

			var currentCommit = mainBranch.Tip;
			for (int i = 0; i < trackingDetails.AheadBy && currentCommit != null; i++)
			{
				result.Add(currentCommit.Message);
				currentCommit = currentCommit.Parents.Where(p => mainBranch.Commits.Contains(p)).FirstOrDefault();
			}

			return result;
		}

		public static int NumberOfChanges
		{
			get
			{
				if (Repository == null)
					return -1;

				Branch mainBranch = Repository.Branches[MainBranchName];
				if (mainBranch == null)
					return -1;

				var trackingDetails = mainBranch.TrackingDetails;
				return (int) trackingDetails.AheadBy;
			}
		}

		public static bool Push()
		{
			if (Repository == null)
				return false;

			Branch mainBranch = Repository.Branches[MainBranchName];
			if (mainBranch == null)
				return false;

			try
			{
				Repository.Network.Push(mainBranch, new PushOptions()
				{
					CredentialsProvider = (url, user, type) => new UsernamePasswordCredentials() { Username = username, Password = AuthorizeModel.Token }
				});
			}
			catch (Exception e)
			{
				Console.WriteLine($"{e.GetType().Name}: {e.Message}");
				Console.WriteLine(e.StackTrace);
				return false;
			}

			Fetch();
			var trackingDetails = mainBranch.TrackingDetails;

			return trackingDetails.AheadBy == 0;
		}

		public static bool RevokeLastCommit()
		{
			if (Repository == null)
				return false;

			Branch mainBranch = Repository.Branches[MainBranchName];
			if (mainBranch == null)
				return false;

			if (!Checkout())
				return false;

			Repository.Reset(ResetMode.Hard, mainBranch.Tip.Parents.Where(p => mainBranch.Commits.Contains(p)).First());
			return true;
		}
	}
}
