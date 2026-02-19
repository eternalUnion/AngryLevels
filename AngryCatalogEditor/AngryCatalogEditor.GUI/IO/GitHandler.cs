using AngryCatalogEditor.GUI.Pages;
using LibGit2Sharp;

namespace AngryCatalogEditor.GUI.IO
{
	public static class GitHandler
	{
		public static string? username;
		public static string? email;

		public static readonly string MainBranchName = "web";

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

		public static bool Commit(string message)
		{
			if (Repository == null)
				return false;

			if (Repository.Index.Count != 0)
			{
				Repository.Reset(ResetMode.Mixed, Repository.Head.Tip);
			}

			Commands.Stage(Repository, new string[]
			{
				"Levels/",
				"V2/"
			});

			if (Repository.Index.Count == 0)
				return true;

			Repository.Commit(message, new Signature(username, email, DateTimeOffset.Now), new Signature(username, email, DateTimeOffset.Now));
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

			Repository.Network.Push(mainBranch, new PushOptions()
			{
				CredentialsProvider = (url, user, type) => new UsernamePasswordCredentials() { Username = username, Password = AuthorizeModel.Token }
			});

			Fetch();
			var trackingDetails = mainBranch.TrackingDetails;

			return trackingDetails.AheadBy == 0;
		}
	}
}
