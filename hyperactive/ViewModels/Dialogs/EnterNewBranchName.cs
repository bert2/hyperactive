﻿namespace hyperactive {
    using System.Linq;

    using LibGit2Sharp;

    public class EnterNewBranchName : ValidatableViewModel {
        private readonly Repository repo;

        public string? OldName { get; }

        private string? newName;
        public string? NewName { get => newName; set => SetProp(ref newName, value); }

        public EnterNewBranchName(LocalBranch owner, string? oldName = null)
            => (repo, OldName) = (owner.Parent.LibGitRepo, oldName);

        protected override string? Validate(string property) => property switch {
            nameof(NewName) when string.IsNullOrWhiteSpace(NewName) => "cannot be empty",
            nameof(NewName) when BranchExists(repo, NewName) => "already exists",
            _ => null
        };

        private static bool BranchExists(Repository repo, string? name)
            => repo.Branches.Any(b => b.FriendlyName == name);
    }
}
