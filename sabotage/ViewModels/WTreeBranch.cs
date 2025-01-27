﻿namespace sabotage {
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;

    using LibGit2Sharp;

    using MoreLinq;

    public class WTreeBranch : LocalBranch {
        private readonly string repoRootPath;

        public string CurrentPath { get; private set; }

        public override ICommand CheckoutCmd => new Command(Checkout);

        public override ICommand CommitCmd => new Command(Commit);

        public override ICommand DeleteCmd => new InvalidCommand();

        public override ICommand NavigateCmd => new Command(Navigate);

        public override ICommand CreateFolderCmd => new Command(CreateFolder);

        public override ICommand CreateFileCmd => new Command(CreateFile);

        public override ICommand RenameItemCmd => new Command(RenameItem);

        public override ICommand DeleteItemCmd => new Command(DeleteItem);

        public WTreeBranch(Repo parent, Branch branch) : base(parent, branch) {
            repoRootPath = Path.TrimEndingDirectorySeparator(parent.Path);
            CurrentPath = repoRootPath;
            OpenFolder(repoRootPath);
        }

        public void ReloadCurrentFolder(bool preserveSelection = true) {
            var selectedItem = SelectedItem?.Name;

            CurrentDirectory = LoadFolder(new DirectoryInfo(CurrentPath));

            if (preserveSelection)
                SelectedItem = CurrentDirectory.SingleOrDefault(item => item.Name == selectedItem);
        }

        private void OpenFolder(string path) {
            CurrentDirectory = LoadFolder(new DirectoryInfo(path));
            CurrentPath = path;
        }

        private WTreeItem[] LoadFolder(DirectoryInfo folder) => folder
            .EnumerateFileSystemInfos()
            .Where(item => item.Name != ".git")
            .OrderBy(item => item, Comparer<FileSystemInfo>.Create(DirectoriesFirst))
            .Select(item => new WTreeItem(this, item))
            .Insert(
                folder.FullName.IsSubPathOf(repoRootPath)
                    ? new[] { new WTreeItem(this, "[ .. ]", folder.Parent!.FullName) }
                    : Enumerable.Empty<WTreeItem>(),
                index: 0)
            .ToArray();

        private async void Checkout() {
            if (Parent.Status.WTreeStatus != WTreeStatus.Clean
                && !await Dialog.Show(new Confirm("discard all changes and restore", subject: Name))) {
                return;
            }

            repo.Reset(ResetMode.Hard);
            Commands.Checkout(repo, LibGitBranch, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
            repo.RemoveUntrackedFiles();

            Snackbar.Show("branch restored");

            ReloadCurrentFolder();
            Events.RaiseWTreeCleaned();
        }

        private async void Commit() {
            var (ok, message) = await Dialog.Show(new EnterCommitMessage(), vm => vm.CommitMessage);
            if (!ok) return;

            Commands.Stage(repo, "*");

            var sig = repo.CreateSignature();
            repo.Commit(message, sig, sig);

            Snackbar.Show("local changes committed");

            Events.RaiseWTreeCleaned();
            CurrentDirectory.OfType<WTreeItem>().ForEach(item => item.ResetStatus());
        }

        private void Navigate() {
            if (SelectedItem?.Type == ItemType.Folder)
                OpenFolder(((WTreeItem)SelectedItem).Path);
        }

        private async void CreateFolder() {
            var (ok, folderName) = await Dialog.Show(new EnterNewItemName(owner: this, ItemType.Folder), vm => vm.NewName);
            if (!ok) return;

            _ = Directory.CreateDirectory(Path.Join(CurrentPath, folderName.NotNull()));

            Snackbar.Show("folder created");

            ReloadCurrentFolder(preserveSelection: false);
            SelectedItem = CurrentDirectory.Single(item => item.Name == folderName);
        }

        private async void CreateFile() {
            var (ok, fileName) = await Dialog.Show(new EnterNewItemName(owner: this, ItemType.File), vm => vm.NewName);
            if (!ok) return;

            File.Open(Path.Join(CurrentPath, fileName.NotNull()), FileMode.CreateNew)
                .Dispose();

            Snackbar.Show("file created");

            Events.RaiseWTreeModified();
            ReloadCurrentFolder(preserveSelection: false);
            SelectedItem = CurrentDirectory.Single(item => item.Name == fileName);
        }

        private async void RenameItem() {
            Debug.Assert(SelectedItem is not null);

            var type = SelectedItem.Type.ToString().ToLower();

            var (ok, newName) = await Dialog.Show(
                new EnterNewItemName(owner: this, SelectedItem.Type, oldName: SelectedItem.Name),
                vm => vm.NewName);
            if (!ok) return;

            var oldPath = Path.Join(CurrentPath, SelectedItem.Name);
            var newPath = Path.Join(CurrentPath, newName.NotNull());

            if (SelectedItem.Type == ItemType.Folder)
                Directory.Move(oldPath, newPath);
            else
                File.Move(oldPath, newPath);

            Snackbar.Show($"{type} renamed");

            Events.RaiseWTreeModified();
            ReloadCurrentFolder(preserveSelection: false);
            SelectedItem = CurrentDirectory.SingleOrDefault(item => item.Name == newName);
        }

        private async void DeleteItem() {
            Debug.Assert(SelectedItem is not null);

            var type = SelectedItem.Type.ToString().ToLower();

            if (!await Dialog.Show(new Confirm($"delete {type}", SelectedItem.Name)))
                return;

            var path = Path.Join(CurrentPath, SelectedItem.Name);

            if (SelectedItem.Type == ItemType.Folder)
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);

            Snackbar.Show($"{type} deleted");

            Events.RaiseWTreeModified();
            ReloadCurrentFolder(preserveSelection: false);
        }

        private static int DirectoriesFirst(FileSystemInfo a, FileSystemInfo b) {
            var aIsDir = (a.Attributes & FileAttributes.Directory) != 0;
            var bIsDir = (b.Attributes & FileAttributes.Directory) != 0;

            return (aIsDir, bIsDir) switch {
                (true , true ) => a.Name.CompareTo(b.Name),
                (true , false) => -1,
                (false, true ) =>  1,
                (false, false) => a.Name.CompareTo(b.Name)
            };
        }
    }
}
