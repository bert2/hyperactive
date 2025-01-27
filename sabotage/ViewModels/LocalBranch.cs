﻿namespace sabotage {
    using System.Windows.Input;

    using LibGit2Sharp;

    public abstract class LocalBranch: ViewModel {
        protected readonly Repository repo;

        public Repo Parent { get; }

        public Branch LibGitBranch => repo.Branches[Name].NotNull();

        private string name;
        public string Name { get => name; set => SetProp(ref name, value); }

        public bool IsHead { get; }

        private IDirectoryItem[] currentDirectory = null!;
        public IDirectoryItem[] CurrentDirectory { get => currentDirectory; protected set => SetProp(ref currentDirectory, value); }

        private IDirectoryItem? selectedItem;
        public IDirectoryItem? SelectedItem { get => selectedItem; set => SetProp(ref selectedItem, value); }

        public ICommand BranchOffCmd => new Command(BranchOff);

        public ICommand RenameCmd => new Command(Rename);

        public abstract ICommand CheckoutCmd { get; }

        public abstract ICommand CommitCmd { get; }

        public abstract ICommand DeleteCmd { get; }

        public abstract ICommand NavigateCmd { get; }

        public abstract ICommand CreateFolderCmd { get; }

        public abstract ICommand CreateFileCmd { get; }

        public abstract ICommand RenameItemCmd { get; }

        public abstract ICommand DeleteItemCmd { get; }

        protected LocalBranch(Repo parent, Branch branch) {
            repo = parent.LibGitRepo;
            Parent = parent;
            name = branch.FriendlyName;
            IsHead = branch.IsCurrentRepositoryHead;
        }

        private async void BranchOff() {
            var (ok, target) = await Dialog.Show(new EnterNewBranchName(owner: Parent), vm => vm.NewName);
            if (!ok) return;

            var created = repo.CreateBranch(branchName: target, target: LibGitBranch.Tip);

            Snackbar.Show("branch created");

            Events.RaiseBranchCreated(created);
        }

        private async void Rename() {
            var (ok, newName) = await Dialog.Show(
                new EnterNewBranchName(owner: Parent, oldName: name),
                vm => vm.NewName);
            if (!ok) return;

            _ = repo.Branches.Rename(LibGitBranch, newName);
            Name = newName.NotNull();

            Snackbar.Show("branch renamed");
        }

        public override string ToString() => Name;
    }
}
